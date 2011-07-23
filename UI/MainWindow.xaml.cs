namespace e2Kindle.UI
{
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Documents;
    using System.Windows.Input;

    using Microsoft.Windows.Controls.Ribbon;

    using e2Kindle.ContentProcess;
    using e2Kindle.Properties;
    using GoogleAPI.GoogleReader;
    using NLog;
    using Brush = System.Windows.Media.Brush;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static MainWindow instance;
        private SettingsWindow settingsWindow;

        // For WPF binding.
        public ObservableCollection<Feed> Feeds { get; private set; }

        public MainWindow()
        {
            this.Feeds = new ObservableCollection<Feed>();
            InitializeComponent();
            instance = this;
        }

        /// <summary>
        /// Logs the message to textbox on the window using specified brush for text.
        /// </summary>
        public static void Log(string message, Brush brush)
        {
            if (instance != null && instance.richTextBox != null)
            {
                instance.Dispatcher.Invoke(
                    new Action(() =>
                    {
                        TextRange range = new TextRange(
                            instance.richTextBox.Document.ContentEnd,
                            instance.richTextBox.Document.ContentEnd);
                        range.Text = message + '\r';
                        range.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
                        instance.richTextBox.ScrollToEnd();
                    }),
                    null);
            }
        }

        private void SetWait(bool wait)
        {
            Cursor = wait ? Cursors.Wait : Cursors.Arrow;
            //toolbar.IsEnabled = !wait;
        }

        private void SettingsClick(object sender, RoutedEventArgs e)
        {
            if (this.settingsWindow == null)
                this.settingsWindow = new SettingsWindow();

            this.settingsWindow.ShowDialog();
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            logger.Info("e2Kindle exit");

            Settings.Default.WindowRectangle = new Rectangle((int)Left, (int)Top, (int)Width, (int)Height);
            Settings.Default.Save();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            if (!Settings.Default.WindowRectangle.IsEmpty)
            {
                Left = Settings.Default.WindowRectangle.Left;
                Top = Settings.Default.WindowRectangle.Top;
                Width = Settings.Default.WindowRectangle.Width;
                Height = Settings.Default.WindowRectangle.Height;
            }

            GoogleReader.SetCredentials(Settings.Default.GoogleUser, Settings.Default.GooglePassword);

            logger.Info("e2Kindle {0} start", Assembly.GetExecutingAssembly().GetName().Version.ToString(2));
        }

        // NOTE: C# 5.0 feature used: async/await
        private async void LoadFeedsClick(object sender, RoutedEventArgs e)
        {
            logger.Info("----------");
            SetWait(true);

            Feeds.Clear();

            logger.Info("Loading feeds from Google Reader");

            try
            {
                Feeds.AddRange(await TaskEx.Run(() => GoogleReader.GetFeeds().OrderByDescending(gf => gf.UnreadCount)));

                listView.SelectedItems.Clear();
                listView.SelectedItems.AddRange(Feeds.Where(f => f.UnreadCount > 0));

                logger.Info(
                    "Loaded {0} feeds ({2} unread entries in {1} feeds)",
                    Feeds.Count(),
                    Feeds.Count(f => f.UnreadCount > 0),
                    Feeds.Sum(f => f.UnreadCount));
            }
            catch (GoogleReaderException ex)
            {
                logger.ErrorException(ex.Message, ex);
            }

            SetWait(false);
            logger.Info("----------");
        }

        // NOTE: C# 5.0 feature used: async/await
        private async void SendClick(object sender, RoutedEventArgs e)
        {
            if (listView.SelectedItems.Empty())
            {
                logger.Warn("No feeds selected.");
                return;
            }

            logger.Info("----------");
            SetWait(true);

            var feeds = listView.SelectedItems.Cast<Feed>().ToArray();
            logger.Info("Getting feed entries ({0} feeds)", feeds.Count());

            try
            {
                var entries = (await TaskEx.Run(() => GoogleReader.GetEntries(feeds))).
                    Flatten().
                    ToArray();

                using (var writer = new StreamWriter("out.fb2"))
                {
                    await TaskEx.Run(
                        () => ContentProcess.CreateFb2(
                            writer,
                            entries,
                            (v, m) => logger.Info("Processed {0}/{1}", v, m))); // TODO: progress bar
                }

                logger.Info("Feeds are downloaded and saved as FB2");

                if (XmlUtils.CheckValid("out.fb2")) logger.Info("FB2 file is valid as XML");
                else logger.Warn("FB2 file isn't valid as XML");

                Process.Start(".");

                if (!Settings.Default.NeededFormats.IsNullOrWhiteSpace())
                {
                    foreach (string format in Settings.Default.NeededFormats.Split(' ').WhereNot(StringUtils.IsEmpty))
                    {
                        logger.Info("Converting to {0}", format);
                        string tFormat = format;
                        try
                        {
                            await TaskEx.Run(() => Calibre.Convert("out.fb2", tFormat));
                            logger.Info("Successfully converted to {0}", format);
                        }
                        catch (Exception ex)
                        {
                            logger.ErrorException("Conversion to {0} failed".FormatWith(format), ex);
                        }
                    }

                    logger.Info("Converted to all specified formats");
                }

                if (Settings.Default.MarkAsRead)
                {
                    logger.Info("Marking downloaded and saved items as read at Google Reader");
                    await TaskEx.Run(() => GoogleReader.MarkAsRead(entries));
                    logger.Info("Marked all entries as read");
                }
            }
            catch (GoogleReaderException ex)
            {
                logger.ErrorException(ex.Message, ex);
            }

            logger.Info("----------");

            SetWait(false);
        }

        private void GoogleCredentialChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            //Settings.Default.GoogleUser = googleUserTexbox.Text;
            //Settings.Default.GooglePassword = googlePasswordTexbox.Text;
        }
    }
}
