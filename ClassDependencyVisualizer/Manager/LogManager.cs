using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows;


namespace ClassDependencyVisualizer.Manager
{
    public class LogManager
    {
        private readonly RichTextBox _logRichTextBox;

        public LogManager(RichTextBox logRichTextBox)
        {
            _logRichTextBox = logRichTextBox ?? throw new ArgumentNullException(nameof(logRichTextBox));
        }

        public void LogInfo(string message)
        {
            AppendLog($"Info: {message}", Colors.LightGreen);
        }

        public void LogError(string message)
        {
            AppendLog($"Error: {message}", Colors.Red);
        }

        private void AppendLog(string message, Color color)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var paragraph = _logRichTextBox.Document.Blocks.LastBlock as Paragraph;
                if (paragraph == null)
                {
                    paragraph = new Paragraph();
                    _logRichTextBox.Document.Blocks.Add(paragraph);
                }
                var run = new Run($"[{DateTime.Now:HH:mm:ss}] {message}\n")
                {
                    Foreground = new SolidColorBrush(color)
                };
                paragraph.Inlines.Add(run);
                _logRichTextBox.ScrollToEnd();
            });
        }
    }
}
