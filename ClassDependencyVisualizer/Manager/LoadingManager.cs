using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ClassDependencyVisualizer.Manager
{
    public class LoadingManager
    {
        private readonly TextBlock _loadingMessageText;
        private DispatcherTimer _animationTimer;
        private int _dotCount = 0;

        public LoadingManager(TextBlock loadingMessageText)
        {
            _loadingMessageText = loadingMessageText ?? throw new ArgumentNullException(nameof(loadingMessageText));
        }

        public void StartLoading(string baseMessage)
        {
            _loadingMessageText.Text = baseMessage;
            _loadingMessageText.Visibility = Visibility.Visible;

            _dotCount = 0;
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _animationTimer.Tick += (s, e) =>
            {
                _dotCount = (_dotCount + 1) % 30;
                _loadingMessageText.Text = baseMessage + new string('・', _dotCount);
            };
            _animationTimer.Start();
        }

        public void StopLoading()
        {
            _animationTimer?.Stop();
            _animationTimer = null;
            _loadingMessageText.Visibility = Visibility.Hidden;
        }
    }
}