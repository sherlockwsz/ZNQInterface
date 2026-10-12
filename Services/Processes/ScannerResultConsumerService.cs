#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ZNQInterface.ViewModels.Components.Workpieces;
using ZNQInterface.ViewModels.Dialogs;
using ZNQInterface.Views.Dialogs;
namespace ZNQInterface.Services.Processes
{
    // Only owner of formal scanner ManualSubmit/ACK. Never writes station snapshots.
    public sealed class ScannerResultConsumerService
    {
        private sealed class ManualTask
        {
            public ScannerEvent Event { get; }
            public long Deadline { get; }
            public long Epoch { get; }
            public ManualCodeEntryViewModel ViewModel { get; }
            public ManualCodeEntryWindow? Window; // only read/write on UI thread
            public ManualTask(ScannerEvent value, long deadline, long epoch, WorkpieceTrackingOptions options)
            { Event = value; Deadline = deadline; Epoch = epoch; ViewModel = new ManualCodeEntryViewModel(value, deadline, options); }
        }
        private readonly WorkpieceAdsReader _reader;
        private readonly WorkpieceTrackingOptions _options;
        private readonly WorkpieceStationViewModel _vm;
        private readonly Dictionary<string, ManualTask> _tasks = new();
        private CancellationTokenSource? _cts;
        private Task? _loop;
        private ManualTask? _active;
        private ulong _session;
        private long _readyEpoch = -1;
        private uint _highWater;
        private readonly Dictionary<string, long> _protocolClock = new();
        private ScannerEventJournal? _journal;
        public ScannerResultConsumerService(WorkpieceAdsReader reader, WorkpieceTrackingOptions options, WorkpieceStationViewModel vm)
        { _reader = reader; _options = options; _vm = vm; }
        private static void Ui(Action action) => Application.Current?.Dispatcher.BeginInvoke(action);
        private void Status(string text) => Ui(() => _vm.ConsumerStatus = text);
        private void Close(ManualTask task, string reason) => Ui(() => { task.ViewModel.Status = reason; task.ViewModel.Closed(); task.Window?.Close(); });
        private void OnConnectionChanged()
        {
            var active = Volatile.Read(ref _active);
            if (active != null) Close(active, "ADS连接变化，退出补录；提交状态由协议恢复确认");
            Status("ADS连接变化，等待队列和会话恢复");
        }
        public Task StartAsync()
        {
            if (_loop != null && !_loop.IsCompleted) return Task.CompletedTask;
            _cts = new CancellationTokenSource(); var ct = _cts.Token;
            _reader.ConnectionChanged += OnConnectionChanged; _loop = Task.Run(() => RunAsync(ct));
            return Task.CompletedTask;
        }
        public void Stop()
        {
            _reader.ConnectionChanged -= OnConnectionChanged; _cts?.Cancel();
            var active = Volatile.Read(ref _active); if (active != null) Close(active, "程序退出，未确认事务将在下次恢复");
        }
        private void CancelTasks(string reason)
        {
            foreach (var task in _tasks.Values) Close(task, reason);
            _tasks.Clear(); Volatile.Write(ref _active, null);
        }
        private async Task RunAsync(CancellationToken ct)
        {
            try
            {
                // Local durable event journal and an exclusive consumer lease.
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZNQInterface", "ScannerEvents");
                _journal = new ScannerEventJournal(directory);
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var queue = await _reader.ReadQueueAsync(ct).ConfigureAwait(false);
                        bool recovery = _session != queue.SessionId || _readyEpoch != queue.ConnectionEpoch;
                        if (recovery)
                        {
                            CancelTasks("恢复/历史事件不重新开启补录窗口");
                            _session = queue.SessionId; _readyEpoch = queue.ConnectionEpoch; _highWater = queue.LastPublished;
                            Status("已恢复PLC队列；积压事件只消费不补弹");
                        }
                        else if (queue.LastPublished < _highWater) throw new InvalidDataException("同会话ResultId倒退，停止自动ACK。");
                        Observe(queue, recovery);
                        await _reader.EnsureSessionAsync(queue.SessionId, queue.ConnectionEpoch, ct).ConfigureAwait(false);
                        // Reconcile a committed ACK even when it already removed the former head.
                        bool pending = await ResolvePendingAsync(queue, ct).ConfigureAwait(false);
                        if (!pending && queue.Head != null) await ProcessHeadAsync(queue, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                    catch (Exception e)
                    {
                        _readyEpoch = -1; CancelTasks("补录退出，等待协议状态恢复");
                        Status("扫码消费故障：" + e.Message + " 未确认记录保留，不自动丢弃。");
                    }
                    await Task.Delay(150, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception e) { Status("扫码事件存储/消费者不可用：" + e.Message + "；未进行队列ACK。"); }
            finally { CancelTasks("扫码消费者已停止"); _journal?.Dispose(); _journal = null; }
        }
        private void Observe(ScannerQueueSnapshot queue, bool recovery)
        {
            long receivedAt = Stopwatch.GetTimestamp();
            foreach (var value in queue.Events)
            {
                var entry = _journal!.Find(value);
                if (entry != null)
                {
                    if (entry.Event != value) throw new InvalidDataException("相同事件ID的载荷发生变化，禁止ACK。");
                    continue;
                }
                entry = _journal.Save(new ScannerJournalEntry { Event = value, ReceivedUtc = DateTime.UtcNow });
                if (!recovery && value.ResultId > _highWater && value.IsOriginalSkipped)
                {
                    long deadline = receivedAt + (long)_options.ManualEntrySeconds * Stopwatch.Frequency;
                    _tasks[value.Key] = new ManualTask(value, deadline, queue.ConnectionEpoch, _options);
                }
            }
            _highWater = queue.LastPublished;
        }
        private static uint Next(uint submitted, uint acknowledged)
        {
            uint value = Math.Max(submitted, acknowledged);
            if (value == uint.MaxValue) throw new InvalidOperationException("协议序号耗尽，不回绕或重用。");
            return value + 1;
        }
        private bool Managed(ScannerEvent value, long epoch)
        {
            var snapshot = _vm.CurrentSnapshot;
            return _reader.IsConnected && epoch == _reader.Epoch && snapshot != null
                && snapshot.ConnectionEpoch == epoch && snapshot.IsManaged(value.SessionId, value.RequestId);
        }
        private void Show(ManualTask task)
        {
            Volatile.Write(ref _active, task);
            Ui(() => {
                if (!ReferenceEquals(Volatile.Read(ref _active), task) || !_reader.IsConnected
                    || _reader.Epoch != task.Epoch || Stopwatch.GetTimestamp() >= task.Deadline || task.ViewModel.Decision.IsCompleted) return;
                task.ViewModel.SetAvailability(Managed(task.Event, task.Epoch));
                task.Window = new ManualCodeEntryWindow(task.ViewModel);
                if (Application.Current.MainWindow?.IsVisible == true) task.Window.Owner = Application.Current.MainWindow;
                task.Window.Show(); // nonmodal: ADS consumer never waits for a UI dialog
            });
        }
        private ScannerJournalEntry Processed(ScannerJournalEntry entry, string reason)
            => _journal!.Save(entry with { Processed = true, Handling = reason });
        private void FinishTask(ScannerEvent value, string reason)
        {
            if (_tasks.TryGetValue(value.Key, out var task))
            { Close(task, reason); _tasks.Remove(value.Key); if (ReferenceEquals(Volatile.Read(ref _active), task)) Volatile.Write(ref _active, null); }
        }
        private async Task ProcessHeadAsync(ScannerQueueSnapshot queue, CancellationToken ct)
        {
            ScannerEvent value = queue.Head!;
            var entry = _journal!.Find(value) ?? throw new InvalidDataException("队头未可靠记录，禁止ACK。");
            if (entry.AckOutcome == ScannerQueueAck.Accepted) return; // stale read after the actual dequeue
            if (entry.AckOutcome != null) throw new InvalidDataException("队头ACK已被PLC拒绝：" + entry.AckOutcome);
            if (!entry.Processed)
            {
                if (value.IsOriginalSkipped && _tasks.TryGetValue(value.Key, out var task))
                {
                    if (queue.NearFull) { entry = Processed(entry, "QueuePressureSkip"); FinishTask(value, "队列接近满额，结束本次补录"); }
                    else if (Stopwatch.GetTimestamp() >= task.Deadline) { entry = Processed(entry, "Expired"); FinishTask(value, "补录10秒窗口已结束"); }
                    else
                    {
                        if (!ReferenceEquals(Volatile.Read(ref _active), task)) Show(task);
                        Ui(() => task.ViewModel.SetAvailability(Managed(value, queue.ConnectionEpoch)));
                        if (!task.ViewModel.Decision.IsCompleted) return;
                        string? code = await task.ViewModel.Decision.ConfigureAwait(false);
                        if (code == null) { entry = Processed(entry, "SkippedOrClosed"); FinishTask(value, "操作员跳过或窗口关闭"); }
                        else
                        {
                            await SubmitManualAsync(queue, task, entry, code, ct).ConfigureAwait(false);
                            return; // matching ManualAck required before any head ACK
                        }
                    }
                }
                else entry = Processed(entry, value.IsCorrection ? "ManualCorrectionEventRecorded" : value.IsOriginalSkipped ? "HistoricalSkipped" : "AutomaticEventRecorded");
            }
            await SubmitAckAsync(queue, entry, ct).ConfigureAwait(false);
        }
        private async Task SubmitManualAsync(ScannerQueueSnapshot queue, ManualTask task, ScannerJournalEntry entry, string code, CancellationToken ct)
        {
            var snapshot = await _reader.ReadStationsAsync(ct).ConfigureAwait(false);
            if (Stopwatch.GetTimestamp() >= task.Deadline || !snapshot.IsManaged(entry.Event.SessionId, entry.Event.RequestId))
            { Processed(entry, "ExpiredOrNoManagedIdentity"); FinishTask(entry.Event, "工件已离开可管理状态或补录到期"); return; }
            var protocol = await _reader.ReadProtocolAsync(queue.SessionId, queue.ConnectionEpoch, ct).ConfigureAwait(false);
            if (protocol.ManualSubmit != protocol.ManualAckSeq) throw new InvalidDataException("存在未确认的人工提交；禁止覆盖另一事务。");
            uint seq = Next(protocol.ManualSubmit, protocol.ManualAckSeq);
            var payload = PlcString128.Encode(code, _options.GetEncoding());
            // Durable intent first; partial writes can be recovered without replaying a commit.
            entry = _journal!.Save(entry with { ManualSeq = seq, SubmittedCode = code, ProtocolIntentUtc = DateTime.UtcNow, Handling = "ManualIntent" });
            _protocolClock[entry.Event.Key] = Stopwatch.GetTimestamp();
            await _reader.WriteAsync(WorkpieceAdsReader.Scanner + "stManualInput.uliSessionId", entry.Event.SessionId, queue.SessionId, queue.ConnectionEpoch, ct).ConfigureAwait(false);
            await _reader.WriteAsync(WorkpieceAdsReader.Scanner + "stManualInput.udiRequestId", entry.Event.RequestId, queue.SessionId, queue.ConnectionEpoch, ct).ConfigureAwait(false);
            await _reader.WriteAsync(WorkpieceAdsReader.Scanner + "stManualInput.sQRCode", payload, queue.SessionId, queue.ConnectionEpoch, ct).ConfigureAwait(false);
            snapshot = await _reader.ReadStationsAsync(ct).ConfigureAwait(false);
            if (Stopwatch.GetTimestamp() >= task.Deadline || !snapshot.IsManaged(entry.Event.SessionId, entry.Event.RequestId))
            {
                _journal.Save(entry with { ManualOutcome = ScannerManualAck.None, Processed = true, Handling = "ManualNotCommittedExpiredOrGone" });
                FinishTask(entry.Event, "补录到期或工件离开，未提交序号"); return;
            }
            await _reader.WriteAsync(WorkpieceAdsReader.Scanner + "udiManualSubmitSeq", seq, queue.SessionId, queue.ConnectionEpoch, ct).ConfigureAwait(false);
            Ui(() => task.ViewModel.Status = "已提交，等待PLC当次序号ACK；未显示为接受");
        }
        private async Task SubmitAckAsync(ScannerQueueSnapshot queue, ScannerJournalEntry entry, CancellationToken ct)
        {
            if (!entry.Processed) throw new InvalidDataException("队头处理未可靠完成，禁止ACK。");
            var protocol = await _reader.ReadProtocolAsync(queue.SessionId, queue.ConnectionEpoch, ct).ConfigureAwait(false);
            if (protocol.AckSubmit != protocol.AckProcessed) throw new InvalidDataException("存在未确认ACK；禁止跨队头提交。");
            uint seq = Next(protocol.AckSubmit, protocol.AckProcessed);
            entry = _journal!.Save(entry with { AckSeq = seq, ProtocolIntentUtc = DateTime.UtcNow });
            _protocolClock[entry.Event.Key] = Stopwatch.GetTimestamp();
            await WriteAckPayloadAsync(queue, entry, ct).ConfigureAwait(false);
        }
        private async Task WriteAckPayloadAsync(ScannerQueueSnapshot queue, ScannerJournalEntry entry, CancellationToken ct)
        {
            if (queue.Head?.ResultId != entry.Event.ResultId || queue.SessionId != entry.Event.SessionId)
                throw new InvalidDataException("仅允许确认当前稳定队头。");
            await _reader.WriteAsync(WorkpieceAdsReader.Scanner + "uliAckSessionId", entry.Event.SessionId, queue.SessionId, queue.ConnectionEpoch, ct).ConfigureAwait(false);
            await _reader.WriteAsync(WorkpieceAdsReader.Scanner + "udiAckResultId", entry.Event.ResultId, queue.SessionId, queue.ConnectionEpoch, ct).ConfigureAwait(false);
            await _reader.WriteAsync(WorkpieceAdsReader.Scanner + "udiAckSubmitSeq", entry.AckSeq, queue.SessionId, queue.ConnectionEpoch, ct).ConfigureAwait(false);
        }
        private bool GraceExpired(ScannerJournalEntry entry)
        {
            if (!_protocolClock.TryGetValue(entry.Event.Key, out long start))
            { start = Stopwatch.GetTimestamp(); _protocolClock[entry.Event.Key] = start; }
            return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency >= _options.ProtocolGraceMilliseconds;
        }
        private async Task<bool> ResolvePendingAsync(ScannerQueueSnapshot queue, CancellationToken ct)
        {
            var pending = _journal!.Pending(queue.SessionId).ToArray();
            if (pending.Length == 0) return false;
            var protocol = await _reader.ReadProtocolAsync(queue.SessionId, queue.ConnectionEpoch, ct).ConfigureAwait(false);
            foreach (var old in pending)
            {
                var entry = old;
                if (entry.ManualSeq != 0 && entry.ManualOutcome == null)
                {
                    if (protocol.ManualAckSeq == entry.ManualSeq)
                    {
                        if (protocol.ManualAck == ScannerManualAck.None) throw new InvalidDataException("人工ACK序号已提交但结果None，协议不一致。");
                        if (protocol.ManualAck == ScannerManualAck.Accepted)
                        {
                            var correction = queue.Events.SingleOrDefault(e => e.CorrectsResultId == entry.Event.ResultId
                                && e.SessionId == entry.Event.SessionId && e.RequestId == entry.Event.RequestId
                                && e.CodeSource == ScannerCodeSource.Manual && e.QRCodeValid && e.QRCode == entry.SubmittedCode);
                            if (correction == null)
                            {
                                if (GraceExpired(entry)) throw new InvalidDataException("PLC Accepted但找不到对应实际更正事件，禁止ACK原记录。");
                                return true; // queue snapshot may precede this PLC commit by one scan
                            }
                        }
                        entry = _journal.Save(entry with { ManualOutcome = protocol.ManualAck, Processed = true, Handling = "Manual:" + protocol.ManualAck });
                        string text = protocol.ManualAck == ScannerManualAck.Accepted ? "PLC 已接受人工补录；显示等待权威快照" : "PLC 拒绝补录：" + protocol.ManualAck;
                        FinishTask(entry.Event, text); Status(text);
                        // Accepted -> original head ACK next iteration -> later correction FIFO.
                    }
                    else if (protocol.ManualSubmit < entry.ManualSeq && protocol.ManualAckSeq < entry.ManualSeq)
                    {
                        entry = _journal.Save(entry with { ManualOutcome = ScannerManualAck.None, Processed = true, Handling = "RecoveredUncommittedManual" });
                        FinishTask(entry.Event, "已确认原人工提交未Commit，恢复后不补发");
                    }
                    else if (protocol.ManualSubmit > entry.ManualSeq || protocol.ManualAckSeq > entry.ManualSeq)
                        throw new InvalidDataException("人工提交被其他序号覆盖，结果不明，禁止重复提交或ACK。");
                    else
                    {
                        if (GraceExpired(entry)) { FinishTask(entry.Event, "提交结果不明，等待协议恢复；不重复提交"); Status("人工提交结果不明，保留原队头等待协议确认"); }
                        return true;
                    }
                }
                if (entry.AckSeq != 0 && entry.AckOutcome == null)
                {
                    if (protocol.AckProcessed == entry.AckSeq)
                    {
                        entry = _journal.Save(entry with { AckOutcome = protocol.Ack });
                        if (protocol.Ack != ScannerQueueAck.Accepted) throw new InvalidDataException("PLC 拒绝队头ACK：" + protocol.Ack);
                        Status($"事件 {entry.Event.ResultId} 已可靠记录，PLC确认队头回收");
                    }
                    else if (protocol.AckSubmit < entry.AckSeq && protocol.AckProcessed < entry.AckSeq)
                    {
                        // Commit provably never reached PLC. The same current head is
                        // still durable; finishing its ACK cannot discard unseen events.
                        await WriteAckPayloadAsync(queue, entry, ct).ConfigureAwait(false); return true;
                    }
                    else if (protocol.AckSubmit > entry.AckSeq || protocol.AckProcessed > entry.AckSeq)
                        throw new InvalidDataException("队头ACK序号被覆盖，不能确认回收结果。");
                    else
                    { if (GraceExpired(entry)) Status("队头ACK结果不明，保留检查点等待协议恢复"); return true; }
                }
            }
            return true; // re-read the head after resolving any prior transaction
        }
    }
}
