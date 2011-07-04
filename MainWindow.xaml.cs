using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using e2Kindle.Properties;
using NLog;
using Brush = System.Windows.Media.Brush;

namespace e2Kindle
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static MainWindow _instance;
        private SettingsWindow _settingsWindow;
        public ObservableCollection<GoogleFeed> Feeds { get; private set; }

        public MainWindow()
        {
            Feeds = new ObservableCollection<GoogleFeed>();

            InitializeComponent();
            _instance = this;

            if (!Settings.Default.WindowRectangle.IsEmpty)
            {
                Left = Settings.Default.WindowRectangle.Left;
                Top = Settings.Default.WindowRectangle.Top;
                Width = Settings.Default.WindowRectangle.Width;
                Height = Settings.Default.WindowRectangle.Height;
            }
        }

        private void SendClick(object sender, RoutedEventArgs e)
        {
            var feeds = Feeds.Where(f => listView.SelectedItems.Contains(f)).ToList();

            new Thread(() =>
            {
                SetWait(true);
                logger.Info("Getting feed entries ({0} feeds)", feeds.Count);

                try
                {
                    var entries = GoogleReader.GetEntries(feeds);
                    using (var writer = new StreamWriter("out.fb2"))
                    {
                        ContentProcess.CreateFb2(writer, entries);
                    }

                    logger.Info("Feeds are downloaded and saved as FB2");
                    logger.Info("Converting to needed formats");

                    ContentProcess.CalibreConvert("out.fb2", Settings.Default.NeededFormats.Split(' '));

                    logger.Info("Converted to all needed formats");

                    if (Settings.Default.MarkAsRead)
                    {
                        logger.Info("Marking downloaded and saved items as read at Google Reader");
                        GoogleReader.MarkAsRead(entries.SelectMany(gr => gr));
                        logger.Info("Marked all entries as read");
                    }
                }
                catch (GoogleReaderException ex)
                {
                    logger.ErrorException(ex.Message, ex);
                }

                SetWait(false);
            }).Start();
        }

        private void SettingsClick(object sender, RoutedEventArgs e)
        {
            if (_settingsWindow == null)
                _settingsWindow = new SettingsWindow();

            _settingsWindow.ShowDialog();
        }

        private void LoadFeedsClick(object sender, RoutedEventArgs e)
        {
            Feeds.Clear();
            new Thread(() =>
            {
                SetWait(true);
                logger.Info("Loading feeds from Google Reader");

                try
                {
                    var feeds = GoogleReader.GetFeeds().OrderByDescending(gf => gf.UnreadCount);
                    Dispatcher.Invoke(new Action(() => Feeds.AddRange((feeds))), null);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        listView.SelectedItems.Clear();
                        Feeds.Where(f => f.UnreadCount > 0).ForEach(
                            f => listView.SelectedItems.Add(f));
                    }
                                          ), null);

                    logger.Info("Loaded {0} feeds ({2} unread entries in {1} feeds)",
                                feeds.Count(),
                                feeds.Count(f => f.UnreadCount > 0),
                                feeds.Sum(f => f.UnreadCount));
                }
                catch (GoogleReaderException ex)
                {
                    logger.ErrorException(ex.Message, ex);
                }

                SetWait(false);
            }).Start();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            logger.Info("e2Kindle exit");

            Settings.Default.WindowRectangle = new Rectangle((int)Left, (int)Top, (int)Width, (int)Height);
            Settings.Default.Save();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            logger.Info("e2Kindle {0} start", Assembly.GetExecutingAssembly().GetName().Version.ToString(2));
        }

        public static void SetWait(bool wait)
        {
            _instance.Dispatcher.Invoke(
                new Action(() =>
                               {
                                   _instance.Cursor = wait ? Cursors.Wait : Cursors.Arrow;
                                   _instance.toolbar.IsEnabled = !wait;
                               }),
                null);
        }

        public static void Log(string message, Brush brush)
        {
            if (_instance != null && _instance.richTextBox != null)
            {
                _instance.richTextBox.Dispatcher.Invoke(
                    new Action(() =>
                                   {
                                       TextRange tr = new TextRange(_instance.richTextBox.Document.ContentEnd, _instance.richTextBox.Document.ContentEnd);
                                       tr.Text = message + '\r';
                                       tr.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
                                       _instance.richTextBox.ScrollToEnd();
                                   }),
                    null);
            }
        }
    }
}
