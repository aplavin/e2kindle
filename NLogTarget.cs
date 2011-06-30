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
            MainWindow.Log(logMessage);
        }
    }
}