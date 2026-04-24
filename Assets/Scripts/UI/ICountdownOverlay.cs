using System;

namespace KeyFlow.UI
{
    public interface ICountdownOverlay
    {
        void BeginCountdown(Action onComplete);
    }
}
