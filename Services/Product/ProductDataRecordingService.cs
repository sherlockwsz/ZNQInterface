#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Prism.Mvvm;
using ZNQInterface.Models.Product;
using ZNQInterface.Services.Processes;

namespace ZNQInterface.Services.Product
{
    public sealed class ProductDataRecordingService : BindableBase
    {
        private sealed record Pending(ProductCompletion Completion, DateTime ReceivedLocal);
        private readonly ProductCompletionReader _reader;
        private readonly ScannerResultConsumerService _scanner;
        private readonly ProductExcelStore _store;
        private readonly ConcurrentDictionary<string, Pending> _pending = new();
        private ConcurrentDictionary<string, ProductDataItem> _persisted = new();
        private CancellationTokenSource? _cts;
        private Task? _loop;
        private int _reload = 1;
        private string _readStatus = "等待PLC完成快照", _saveStatus = "正在读取产品Excel", _notice = "";
        private string _status = "正在启动产品记录";
        private IReadOnlyList<ProductDataItem> _items = Array.Empty<ProductDataItem>();
        public string Status { get => _status; private set => SetProperty(ref _status, value); }
        public IReadOnlyList<ProductDataItem> Items { get => _items; private set => SetProperty(ref _items, value); }
        public ProductDataRecordingService(ProductCompletionReader reader, ScannerResultConsumerService scanner, ProductExcelStore store)
        { _reader = reader; _scanner = scanner; _store = store; }
        private static void Ui(Action action) => Application.Current?.Dispatcher.BeginInvoke(action);
        private void UpdateStatus()
        {
            string text = $"{_saveStatus}；{_readStatus}；待保存 {_pending.Count} 件" + _notice;
            Ui(() => { if (Status != text) { Trace.WriteLine(text); Status = text; } });
        }
        private void Publish(IReadOnlyList<ProductDataItem> items)
        {
            // Replace the complete mirror only after LoadAll has validated all history.
            var persisted = new ConcurrentDictionary<string, ProductDataItem>(items.ToDictionary(i => i.Key));
            Interlocked.Exchange(ref _persisted, persisted);
            Ui(() => Items = items);
        }
        private void PublishSaved(ProductDataItem item)
        {
            _persisted[item.Key] = item;
            Ui(() =>
            {
                if (Items.Any(i => i.Key == item.Key)) return;
                var items = Items.ToList();
                int index = items.FindIndex(i => i.CompletionTime < item.CompletionTime);
                items.Insert(index < 0 ? items.Count : index, item);
                Items = items.ToArray();
            });
        }
        private static void CheckPersisted(ProductDataItem saved, ProductCompletion value)
        {
            if (saved.CompleteSeq != value.CompleteSeq || saved.ProductNumber != value.QRCode)
                throw new InvalidOperationException("同一工件身份的完成载荷不一致，禁止重复追加。");
        }
        public void RequestHistoryReload() => Interlocked.Exchange(ref _reload, 1);
        public Task StartAsync()
        {
            if (_loop != null && !_loop.IsCompleted) return Task.CompletedTask;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            _loop = Task.WhenAll(Task.Run(() => ReadLoopAsync(ct)), Task.Run(() => SaveLoopAsync(ct)));
            return Task.CompletedTask;
        }
        public void Stop() => _cts?.Cancel();
        private async Task ReadLoopAsync(CancellationToken ct)
        {
            ulong session = 0; uint sequence = 0;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var value = await _reader.ReadAsync(ct).ConfigureAwait(false);
                        if (value != null)
                        {
                            if (session == value.SessionId && sequence != 0 && value.CompleteSeq > sequence
                                && value.CompleteSeq - sequence > 1)
                                _notice = "；完成序号出现缺口，单快照无法补回离线期间的更早记录";
                            if (session == value.SessionId && value.CompleteSeq < sequence)
                                throw new InvalidOperationException("同会话完成序号倒退，未接收。");
                            session = value.SessionId; sequence = value.CompleteSeq;
                            if (_persisted.TryGetValue(value.Key, out var saved))
                            {
                                CheckPersisted(saved, value);
                            }
                            else
                            {
                                var held = _pending.GetOrAdd(value.Key, new Pending(value, DateTime.Now));
                                if (held.Completion != value) throw new InvalidOperationException("同身份完成快照发生变化，保留首次接收数据等待核查。");
                            }
                        }
                        _readStatus = _reader.Diagnostic == 0 ? "完成快照监控正常"
                            : "PLC产品发布诊断：" + _reader.Diagnostic + "（保留上一条有效快照）";
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                    catch (Exception e) { _readStatus = "产品快照读取异常：" + e.Message; }
                    UpdateStatus();
                    await Task.Delay(150, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        }
        private async Task SaveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        if (Interlocked.Exchange(ref _reload, 0) != 0)
                        {
                            try { Publish(_store.LoadAll()); }
                            catch { Interlocked.Exchange(ref _reload, 1); throw; }
                        }
                        bool waiting = false;
                        foreach (var entry in _pending.ToArray().OrderBy(p => p.Value.ReceivedLocal))
                        {
                            ct.ThrowIfCancellationRequested();
                            var p = entry.Value;
                            if (_persisted.TryGetValue(entry.Key, out var saved))
                            {
                                // Startup/reload or an earlier save already verified this identity in Excel.
                                CheckPersisted(saved, p.Completion);
                                _pending.TryRemove(entry.Key, out _);
                                continue;
                            }
                            DateTime? scan = _scanner.FindProductScanTime(p.Completion.SessionId, p.Completion.RequestId,
                                p.Completion.QRCode, p.Completion.CodeSource, p.Completion.OriginalResultId, p.Completion.CorrectionResultId);
                            if (!scan.HasValue) { waiting = true; continue; }
                            var item = _store.Save(p.Completion.ToItem(scan.Value, p.ReceivedLocal));
                            // Save has either committed a complete workbook or verified the durable identity.
                            PublishSaved(item);
                            _pending.TryRemove(entry.Key, out _);
                        }
                        _saveStatus = waiting ? "等待原扫码日志中的已确认事件时间，尚未写入正式记录" : "产品Excel保存正常";
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                    catch (Exception e)
                    {
                        _saveStatus = "产品Excel读取/保存失败，将后台重试：" + e.Message;
                    }
                    UpdateStatus();
                    await Task.Delay(1000, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        }
    }
}
