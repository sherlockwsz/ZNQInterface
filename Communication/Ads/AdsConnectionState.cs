namespace ZNQInterface.Communication.Ads
{
    /// <summary>
    /// 上位机 ADS 客户端连接状态。
    /// </summary>
    public enum AdsConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Faulted
    }
}
