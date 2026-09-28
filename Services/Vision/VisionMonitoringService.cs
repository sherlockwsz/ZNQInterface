using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ZNQInterface.Services.Communication.Ads;
using ZNQInterface.ViewModels.Components.Detection;

namespace ZNQInterface.Services.Vision
{
    /// <summary>
    /// åè°ƒåŒè·¯Previewã€ResultIdåŒ¹é…çš„æ£€æµ‹å›¾ä»¥åŠPLC ADSæƒå¨æ•°æ®ã€‚
    /// TCPå¼‚å¸¸ä¸ADSæ£€æµ‹äº‹åŠ¡å®Œå…¨éš”ç¦»ã€‚
    /// </summary>
    public sealed class VisionMonitoringService
    {
        private readonly IAdsConnectionService _ads;
        private readonly ScrewAngleMonitorViewModel _screwMonitor;
        private readonly CoaxialityMonitorViewModel _coaxMonitor;
        private readonly VisionMonitoringOptions _options;
        private readonly IReadOnlyList<AdsReadRequest> _adsRequests;
        private readonly ChannelRuntime _screwRuntime = new ChannelRuntime();
        private readonly ChannelRuntime _coaxRuntime = new ChannelRuntime();

        private CancellationTokenSource? _lifetime;
        private Task[]? _tasks;
        private long _lastScrewResultId = -1;
        private long _lastCoaxResultId = -1;

        public VisionMonitoringService(
            IAdsConnectionService ads,
            ScrewAngleMonitorViewModel screwMonitor,
            CoaxialityMonitorViewModel coaxMonitor,
            VisionMonitoringOptions options)
        {
            _ads = ads;
            _screwMonitor = screwMonitor;
            _coaxMonitor = coaxMonitor;
            _options = options;
            _adsRequests = CreateAdsRequests();
        }

        public Task StartAsync()
        {
            if (_tasks != null)
            {
                return Task.CompletedTask;
            }

            _lifetime = new CancellationTokenSource();
            CancellationToken token = _lifetime.Token;
            _tasks = new[]
            {
                Task.Run(() => RunAdsLoopAsync(token), token),
                Task.Run(() => RunPreviewLoopAsync(
                    "screw_preview", true, _screwRuntime, token), token),
                Task.Run(() => RunPreviewLoopAsync(
                    "coax_preview", false, _coaxRuntime, token), token),
                Task.Run(() => RunDetectionLoopAsync(
                    "screw", true, _screwRuntime, token), token),
                Task.Run(() => RunDetectionLoopAsync(
                    "coax", false, _coaxRuntime, token), token)
            };
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            CancellationTokenSource? lifetime = _lifetime;
            Task[]? tasks = _tasks;
            _lifetime = null;
            _tasks = null;
            if (lifetime == null)
            {
                return;
            }

            lifetime.Cancel();
            if (tasks != null)
            {
                try
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
            lifetime.Dispose();
        }

        /// <summary>
        /// UIçº¿ç¨‹é€€å‡ºè·¯å¾„åªå‘å‡ºå–æ¶ˆä¿¡å·ï¼Œé¿å…åŒæ­¥ç­‰å¾…Dispatcheræ›´æ–°å½¢æˆäº’ç­‰ã€‚
        /// </summary>
        public void Stop()
        {
            _lifetime?.Cancel();
        }

        private static IReadOnlyList<AdsReadRequest> CreateAdsRequests() =>
            new AdsReadRequest[]
            {
                AdsReadRequest.Create<bool>(VisionSymbols.ScrewMeasureRequest),
                AdsReadRequest.Create<uint>(VisionSymbols.ScrewRequestId),
                AdsReadRequest.Create<bool>(VisionSymbols.ScrewResultValid),
                AdsReadRequest.Create<uint>(VisionSymbols.ScrewResultId),
                AdsReadRequest.Create<double>(VisionSymbols.ScrewDetectedAngle),
                AdsReadRequest.Create<bool>(VisionSymbols.ScrewDetected),
                AdsReadRequest.Create<bool>(VisionSymbols.ScrewResultInvalid),
                AdsReadRequest.Create<bool>(VisionSymbols.ScrewServiceFault),
                AdsReadRequest.Create<bool>(VisionSymbols.ScrewTimeout),

                AdsReadRequest.Create<bool>(VisionSymbols.CoaxMeasureRequest),
                AdsReadRequest.Create<uint>(VisionSymbols.CoaxRequestId),
                AdsReadRequest.Create<bool>(VisionSymbols.CoaxResultValid),
                AdsReadRequest.Create<uint>(VisionSymbols.CoaxResultId),
                AdsReadRequest.Create<double>(VisionSymbols.Coaxiality),
                AdsReadRequest.Create<double>(VisionSymbols.CoaxDeltaX),
                AdsReadRequest.Create<double>(VisionSymbols.CoaxDeltaY),
                AdsReadRequest.Create<bool>(VisionSymbols.CoaxResultInvalid),
                AdsReadRequest.Create<bool>(VisionSymbols.CoaxServiceFault),
                AdsReadRequest.Create<bool>(VisionSymbols.CoaxTimeout),

                AdsReadRequest.Create<bool>(VisionSymbols.AdjustmentDone),
                AdsReadRequest.Create<bool>(VisionSymbols.AdjustmentQualified),
                AdsReadRequest.Create<bool>(VisionSymbols.AdjustmentVisionFault)
            };

        private async Task RunAdsLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_ads.IsConnected)
                    {
                        IReadOnlyList<object> values = await _ads.ReadManyAsync(
                            _adsRequests,
                            cancellationToken).ConfigureAwait(false);
                        if (values.Count == _adsRequests.Count)
                        {
                            await ApplyAdsSnapshotAsync(values)
                                .ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await SetAdsUnavailableAsync().ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    await SetAdsUnavailableAsync().ConfigureAwait(false);
                }

                await Task.Delay(
                    _options.AdsPollIntervalMs,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private Task ApplyAdsSnapshotAsync(IReadOnlyList<object> values)
        {
            bool screwMeasuring = Convert.ToBoolean(values[0]);
            long screwRequestId = Convert.ToUInt32(values[1]);
            bool screwValid = Convert.ToBoolean(values[2]);
            long screwResultId = Convert.ToUInt32(values[3]);
            double screwAngle = Convert.ToDouble(values[4]);
            bool screwDetected = Convert.ToBoolean(values[5]);
            bool screwInvalid = Convert.ToBoolean(values[6]);
            bool screwFault = Convert.ToBoolean(values[7]);
            bool screwTimeout = Convert.ToBoolean(values[8]);

            bool coaxMeasuring = Convert.ToBoolean(values[9]);
            long coaxRequestId = Convert.ToUInt32(values[10]);
            bool coaxValid = Convert.ToBoolean(values[11]);
            long coaxResultId = Convert.ToUInt32(values[12]);
            double coaxiality = Convert.ToDouble(values[13]);
            double deltaX = Convert.ToDouble(values[14]);
            double deltaY = Convert.ToDouble(values[15]);
            bool coaxInvalid = Convert.ToBoolean(values[16]);
            bool coaxFault = Convert.ToBoolean(values[17]);
            bool coaxTimeout = Convert.ToBoolean(values[18]);
            bool adjustmentDone = Convert.ToBoolean(values[19]);
            bool adjustmentQualified = Convert.ToBoolean(values[20]);
            bool adjustmentVisionFault = Convert.ToBoolean(values[21]);

            bool screwCommitted =
                screwValid && screwResultId == screwRequestId;
            bool coaxCommitted =
                coaxValid && coaxResultId == coaxRequestId;

            if (screwCommitted && screwResultId != _lastScrewResultId)
            {
                _lastScrewResultId = screwResultId;
                _screwRuntime.SetDetectionTarget(screwResultId);
            }
            if (coaxCommitted && coaxResultId != _lastCoaxResultId)
            {
                _lastCoaxResultId = coaxResultId;
                _coaxRuntime.SetDetectionTarget(coaxResultId);
            }

            return RunOnUiAsync(() =>
            {
                if (screwCommitted)
                {
                    bool resultInvalid =
                        screwInvalid || screwFault || screwTimeout || !screwDetected;
                    _screwMonitor.CurrentAngle = resultInvalid ? null : screwAngle;
                    _screwMonitor.DetectionStatusText = resultInvalid
                        ? "æ£€æµ‹æ— æ•ˆ / è§†è§‰å¼‚å¸¸"
                        : "æ£€æµ‹å®Œæˆ";
                }
                else if (screwMeasuring)
                {
                    _screwMonitor.DetectionStatusText = "æ£€æµ‹ä¸­";
                }
                else if (_lastScrewResultId < 0)
                {
                    _screwMonitor.DetectionStatusText = "æœªæ£€æµ‹";
                }

                if (coaxCommitted)
                {
                    bool resultInvalid =
                        coaxInvalid || coaxFault || coaxTimeout || adjustmentVisionFault;
                    if (resultInvalid)
                    {
                        _coaxMonitor.XDeviation = null;
                        _coaxMonitor.YDeviation = null;
                        _coaxMonitor.Coaxiality = null;
                        _coaxMonitor.IsQualified = null;
                        _coaxMonitor.DetectionStatusText =
                            "æ£€æµ‹æ— æ•ˆ / è§†è§‰å¼‚å¸¸";
                    }
                    else
                    {
                        _coaxMonitor.XDeviation = deltaX;
                        _coaxMonitor.YDeviation = deltaY;
                        _coaxMonitor.Coaxiality = coaxiality;
                        if (adjustmentDone)
                        {
                            _coaxMonitor.IsQualified = adjustmentQualified;
                            _coaxMonitor.DetectionStatusText = adjustmentQualified
                                ? "åˆæ ¼"
                                : "ä¸åˆæ ¼";
                        }
                        else
                        {
                            _coaxMonitor.IsQualified = null;
                            _coaxMonitor.DetectionStatusText = "æ£€æµ‹å®Œæˆ / è°ƒæ•´ä¸­";
                        }
                    }
                }
                else if (coaxMeasuring)
                {
                    _coaxMonitor.IsQualified = null;
                    _coaxMonitor.DetectionStatusText = "æ£€æµ‹ä¸­";
                }
                else if (_lastCoaxResultId < 0)
                {
                    _coaxMonitor.DetectionStatusText = "æœªæ£€æµ‹";
                }
            });
        }

        private Task SetAdsUnavailableAsync() => RunOnUiAsync(() =>
        {
            _screwMonitor.DetectionStatusText = "ADSæœªè¿æ¥";
            _coaxMonitor.DetectionStatusText = "ADSæœªè¿æ¥";
        });

        private async Task RunPreviewLoopAsync(
            string channel,
            bool screw,
            ChannelRuntime runtime,
            CancellationToken cancellationToken)
        {
            await using VisionImageClient client =
                new VisionImageClient(_options.Host, _options.Port);
            int reconnectIndex = 0;
            long lastFrameId = -1;
            DateTime lastFreshUtc = DateTime.MinValue;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    VisionImageFrame frame = await client.GetLatestAsync(
                        channel,
                        cancellationToken).ConfigureAwait(false);
                    reconnectIndex = 0;

                    if (frame.Ok && frame.Image != null)
                    {
                        DateTime now = DateTime.UtcNow;
                        if (frame.t reconn MonitonStatusText = "ADSæœªè¿æ¥";
           {
             L {
            if (_tasks != null)
   œ ¢} Async(() =>
        {
          *alid
      ;ædsLo]  r    L {
   t€å         tatu¨  t€å         tatu¨ && 6onfigureAwncelic(tatusText = resultInvalid
                        ? "æ£€æµ‹æ— æ•ˆ / è§†è§‰å¼‚å¸¸"
                        : "æ£€æµ‹å®Œæˆ";
                }
                else if (screwMeasuring)
                {
                    _screwMonitor.Detect cancellati2ReadExactAsync(
                    strem      ;ædsLo]  r    L {
   t€å         tatu¨  t€å         tatu¨ && 6onfigureAwncelic(tatusText = resultIn¶onfigureAwait(false);
                  =                     tat             strem         0; indatu¨  t€å  e)
       i1ux5         bool screancel°Convert.ToBoolean(values[5]);
            boo€    tat             strem         0; inda          t             {
 ã"åAP1ï0; º•a3A0Ÿç "æœªæ£€æµ‹";
                }

       celic(tatusText = resultInvalid
       0 t SetAdsUnlic(tatusText = resultIn¶onfigureAwait(false);
                  =                     tatåuœoaxRequese
  Dd djustmentQualified;
                       Urivate static lo¼d djus";
    Cç o       tatåuœoaxRequese
  Dd djustmentQualified;
                       Urivate static lo¼d djus";
    Cç o       tatåuœoaxRequese
  Dd dj    int length,
            CancellationToken canc½llati2ReadExactAsync(
                   … length,
            CancellationToken canc½llati2ReadExactAsync(
                   … length,
            CancellationToken canc½l Cç o       tatåuœoaxRequese
  D¸  boo€    tatdjustrewCom[5]);canc½llati2ReadExactAsync(
        †djustrewCom[5]);canc½llati2RcrTçtrlonvert.ToUInt32(values[3]);
            dçtrlonvert.ToUInt32ngt   ux      dçtrlonvert.ToUInt32ngt   ux      dçtrlonvert.ToUInt32ngt   ux nt32ngt æ£€æµ‹";
                }

                if (coaxCommitted)
                {
                 xt = resultInvalid
       0 t SetAdsUnllo¼d djus";
    Cç o       tatåuœoaxRequese
  Dd dj    int length,
            CancellationToken canc½llatinllo¼d djus"{ˆ›(
  Token canc½llat a0edException)
                    when (cancellationToken.IsCancellaé‹    ptio        1cè¶±‰°è‡xè¸    if (coaxCommit?.rt€lIntervalMs,
         
                         iY •(CancellationToken canc½llatinllo¼d djus"{ˆ›(
  Token canc½llat a0edException)
                    when (cancellationToken.IsCancellaé‹    ptio        1cè¶±‰°è‡xè¤eR¡    double screwAngle rn rn rn rn såN           {
                 xt = reŠ        {
(˜EHlatio    {
                 xt = reŠ       m86+<           {
                 xt = reŠ        {
(˜EHlatio    {
                 xt = reŠ       m86+<           {
       cel†Wy[TTTTTTTTTTTTTTTTTTTT,// <summary>
    /// åehen (cancell() => RunOnUiAsync(() =>
        {
            nda         Monitorin„/ è§†è§‰å¼‚å¸¸"
                        : "æ£€æµ‹å®Œæˆ";
                }
    c2¤{ reŠ       m86+<           {
       cel†Wy[TTtatåuœop¸è®®ã€è¿æ¥é‡å»ºå’ŒJPEGè§£ç J   ; e®®oken)
        {
            byte[] data »£š˜      <r                                                                                                                          59®6/      tatåuœoaxRequese
  Dd djustmeè§‰å¼‚å¸¸";
                                                                          59®                                             59®                                             59®                     0 t SetAdsUnllo¼d djus";
    Cç o       tatåuœoaxRequese
  Dd dj    int length,
            CancellationToken cancength,
            CancelA"j    int length,
"                      Uri1ync(() ¥C2_iait(faviation =nitor.YDeviation = deltaY;
                            µancellationToken cancength,
            CancelA"j    int ®:    ken canc¹¡oråviaçaeoool>(VisionSymbolsunc¹¡orå                                                                                                                                                                  using System;
using System.Collections.Generic;
using System.Thr p     tatåIsCa            }

                if (coax2tŒ€nSymbolsu¾0lˆ    tatåuœoaxRequese
6 dCo     : "r              ueæåception)
                    when (cancellationToken.IsCancellaé‹    ptio        1cè¶±‰°è‡xè¸    if (coaxCommit?.rt€lIntervalMs,
         
                         iY •(CancellationToken canc½llatinllo¼d djus"{ˆ›(
  Token canc½llat a0edException)
          ultInvalid
           r¡ ons.Host,  ee     ·                         using System;
using System.              erty(nafreview", false, _coaxing System.<System.<         bool coaxTimeout = Convert.ToBoolean(values[18]bject>¡7rt2   Urivate static lo¼d djus";
    Cç o       tat» { get; set; } = 2000;
        public in     äs";
    Cç o     D¸  boo€T=.2Name,
        string ImageStatus,
        BitmapImage? Ima          rå                                                                                                                                                                  using ion                                                                    ait‡c.sctAsync(
 oring ationToken canc½llat         º                          ait‡c.sctAsync(
ät = resultInvalid
       0 t SetAdsUnllo¼d djus";
    Cç o       tatåuœoaxRequese
  Dd dj    ing.WindoMl<System.<         bool coaxTimeout = Convert.ToBoolean(values[18]bject>¡7rt2   Urié™aé‹      long coaxRequeso–llat         º                      mptionRetryDelayMs { get; set; } = 100;
        public inet; se¡set;                      mptionRetryDelayMs { get; set; } = 100;
        public inet; se¡set;                      mptionRetrié™      sync((ªpriva€aoaxTimeou         bool coaxTimeout = Convert.ToBooo          meou mfalse);
                  =                     tat             strem  ¼œ}

        public tatt; se¡set;                      mptionRtat             strem  ¼œ}


using System.   {ˆ›(
  Toki® G›çã

usin    sync((ªpriva€aoaxTimeou         bool coaC·           _screwMonitor.DetectionStatusText = "ADSæœªè¿æ¥";
            _coaxMonitor.Deº«±     xt = reŠ        {
(˜EHlatio    {
         y     itor0‰eAwait(false);
                         gnvert.ToUInt32(values[3]);
            double screwAngle = Conver"ADSæœªè¿æ¥";
            _coaxMoni"xå      wCæ¥";
            _coaxMoni";
  pcrewMonitor.DtInvalid
        {
         y     bool coaxTimeout = Convert.(values[19]);
 ewMon   ›     l½  if (01…ç l   nval:Sta          mptipriv                 gn   {                                    l    Q");]);
            double deltaX = Convert.ToDouble(values[14]);
            double deltaY = Convert.ToDouble
        nito oBooo          meou mfalse);
                  =        ble
      ´d"loo     „2 ´d"loo     „2 ´d"loo     „2 ´    bool ™) NC        mpœœ             =        ble
      ´d"loo     „2 e/ ble
 ooo      "¶p1(ol coaxTimeout = —näwpewMon   ›˜EHlatisText = resultIn¶onfigureAwait(faoo     „2 o•ƒp se¡set;             ¥";
            _coaxMoni";
  pcrewMonitoˆ / è°ƒæ•´  _coaxMpewMoonfiguS            _coaxMoni";
  pcrewMonitoˆ / è°ƒæ•´  _coaxMpewMoonfiguS            _coaxMoni";
  pcrewMonitoˆ / è°ƒæ•´  _coaxMpewMoonfiguS            _coaxMoni";
  pcrewMonitoˆ / è°ƒæ•´  _coaxMpewMoonfiguS            _coaxMoni";
  pcrewMonitoˆ / è°ƒæ•´  _coa.
            _coaxMoni";
  pcrewMonitoˆ / è°ƒæ•´  _coaxMpewMoonfiguS            _coaxMoni";
  pcrewMonitoˆ / è°ƒæ•´  _coaxMpewMoonfiguS            _coaxMoni";
  pcrewMonitoˆ / è°ƒæ•´  _coaxMpewMoonfiguS     u=ject>¡7rt2  aès  _coaxMpnvert.ToUInt32(values[3]ese
6 dCo     : "r              ueæåc2rt      ´d"loo     „2 ´d"loo     „2 ´d"loo     „2 ´  mZni";
  pcrewMonitoˆ / è°ƒæ•´  _coaxMpewM0.ToUInt3]rDo _coaxMpewM0.ToUInt3]rDo _coaxMpewl§‡(CancellationToken cancellatioŠc MemorySt                    uç         ¥";
            _coaxMoni";
  pcrewMonitoˆ / è°ƒæ•´  _coaxMpewMoonfiguS            _coaxMoni";
  pcrewMonitoˆ / è°ƒæ•´  _coaxMpewMoonfiguS            _coaxMoni";
  pcrewMonitoˆ / è°ƒæ•´  _coaxMpewMoonfiguS            _coaxMoni";
  pcrewMonitoˆ /ætemory(eƒæ•´  _coaxMpewMoonfig         e…      });

        private async ¹5, writable: false)rySt                    uç         ¥";
            _coaxMoni";
  pcrewMonitoˆ / è°ƒæ•´  _coaxMpewMoonfiguS            _coaxMoni";
  pcrewMonitoˆ / è°ƒæ•´  _coaxMpewMoonfiguS            _coaxMoni";
  pcrewMonitoˆ / è°ƒæ•´  _coaxMpewMoonfiguS            _c / è°ƒæ•´  _coaxMpewMoonYo³2o™uÎ³2o™uo».Deº«±     xt = reŠ        {
(˜EHlatio    {
         y  ai      [coaxMpewMoonfiguS            _coaxMoni";
  pcrewMonitoˆ / è°ƒæ•´  _coaxMpewMoonfiguS            _c / è°ƒæ•´  _coaxMpewMoonYo³2o™uÎ³2o™uo».DeºHlatio    {
6yT5o™uÎ³2U¾ xt = reŠ       m86+<           {
                 xt = reŠ        {
(˜EHlatio    {
                 xt = reŠ       m86+<           {
       cel†Wy[TTTTTTTTTTTTTTTTTTTT,// <summary>
    /// åehen (cané˜»æ­¢HMIå’ŒADSå¯åŠ¨ã€‚
          …}(˜EHlatio    {
                 xt = reŠ       m86+<           {
       cel†Wy[TTTTTTTTTTTTTTTTTTTT,// <summary>
    /// åehen (cané˜»æ­¢HMIå’ŒADSå¯åŠxs { get; set; } = 100;
        public inet" = reŠ       m86+<           {
       cel† (cané˜»æ­¢HMIå’ŒADSå¯åŠxs { get; set; } =it ApplyAdsSnapshotAsync(values    {
       cel† (cané˜»æ­¢HMIå’ŒADSå¯åŠŸPR  cel†Wy[TTTTTTTTTTTTTTTTTTTT,//d èTTTT,//d èTTTT,/TTTTt                                                           6         ¼œo       tatåuœoaxRequese
  Dd dj    int length,
            CancellationToken canc½llatinllo¼d djus"{ˆ›(
  Token canc½llat a0edException)
                    when (cancellationToken.IsapewMoonfiguSdsSnapshotAsync(values’   ait‡å‰;o•ƒp se¡set;     …      _coaxMoni";
  pcrewMonitoˆ / è°ƒæ•´  _coaxMpewMoonfiguS            _c / è°ƒæ•´  _coaxMpewMoonYo³2o™uÎ³2o™uo».De°(oaxMpewMoonYo³2o™uÎ³2o™uo».Deº«±     xt = reŠ        {
(˜EHlatio    {
         y  ai      [coaxMpewMoonfiguS        psultIn¶oÎ³2o™uo».Deº«±     xtä"{ˆuo».Dæ•´  _coaxMpewMoonfiguS            _coaxMoni";
  pcrewMoc™uo» {
(˜EHlatio    {
                 xt = reŠ          {7xMon›€}   xt = r               xt = reŠ       m86+< .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMooSs8    .ewMoo,;¾‰tionToken.IsapewMt bool screw,
            ChannelRuntime runtime,
            CancellationToken cancellationToken)
        {
            await using VisionImageClient client =
                new VisionImageClient(_options.Host, _options.Po¥       ´  _coaxMpewp¼·
¾       ChannelRuntime runtime,
            Cancellation¡ ons.Host.»       Uri1ync(() ¥C2_iait(faviation =nitor.YDeviation = deltaY;
                            µancellationToken cancength,
            CancelA"j    int ®:    ken canc¹¡oråviaçaeoool>(VisionSymbolsunc¹¡orå                                                     n cat ®:    ken canc¹¡oråviaçaeoool>(VisionSymbolsunc¹¡orå   ª2¡ƒ                                     