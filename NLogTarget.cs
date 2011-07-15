namespace e2Kindle
{
    using System.Windows.Media;
    using e2Kindle.UI;
    using NLog;
    using NLog.Targets;

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