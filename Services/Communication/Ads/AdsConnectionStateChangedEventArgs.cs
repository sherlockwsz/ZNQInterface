using System;

namespace ZNQInterface.Services.Communication.Ads
{
    public sealed class AdsConnectionStateChangedEventArgs : EventArgs
    {
        public AdsConnectionStateChangedEventArgs(
            AdsConnectionState state,
            string message)
        {
            State = state;
            Message = message;
        }

        public AdsConnectionState State { get; }

        public string Message { get; }
    }
}
