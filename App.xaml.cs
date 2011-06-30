using System.Windows;
using NLog;
using NLog.Config;

namespace e2Kindle
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly Logger logger;

        static App()
        {
            ConfigurationItemFactory.Default.Targets.RegisterDefinition("MyTarget", typeof(NLogTarget));
            logger = LogManager.GetCurrentClassLogger();
        }
    }
}
