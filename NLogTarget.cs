using System.Windows.Media;
using NLog;
using NLog.Targets;

namespace e2Kindle
{
    [Target("MyTarget")]
    public sealed class NLogTarget : TargetWithLayout
    {
        protected override void Write(LogEventInfo logEvent)
        {
            string logMessage = Layout.Render(logEvent);

            Brush brush;
            if (logEvent.Level == LogLevel.Error)
                brush = Brushes.Red;
            else if (logEvent.Level == LogLevel.Warn)
                brush = Brushes.DarkOrange;
            else if (logEvent.Level == LogLevel.Info)
                brush = Brushes.Green;
            else
                brush = Brushes.Gray;

            MainWindow.Log(logMessage, brush);
        }
    }
}