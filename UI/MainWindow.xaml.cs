namespace e2Kindle.UI
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Input;

    using Microsoft.Windows.Controls.Ribbon;

    using e2Kindle.Aspects;
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
        private readonly HashSet<string> formats = new HashSet<string>();

        #region Feeds collection management
        // For WPF binding.
        public ObservableCollection<Feed> Feeds { get; private set; }

        [OnGuiThread]
        private void FeedsClear()
        {
            Feeds.Clear();
        }

        [OnGuiThread]
        private void FeedsAdd(Feed feed)
        {
            Feeds.Add(feed);
        }

        [OnGuiThread]
        private void FeedsAddRange(IEnumerable<Feed> feeds)
        {
            Feeds.AddRange(feeds);
        }

        [OnGuiThread]
        private void FeedsSetSelected(IEnumerable<Feed> feeds)
        {
            listView.SelectedItems.Clear();
            listView.SelectedItems.AddRange(feeds);
        }

        [OnGuiThread]
        private IEnumerable<Feed> FeedsGetSelected()
        {
            return listView.SelectedItems.Cast<Feed>();
        }
        #endregion

        public MainWindow()
        {
            this.Feeds = new ObservableCollection<Feed>();
            InitializeComponent();
            instance = this;

            Dictionary<string, string[]> formatsDic = new Dictionary<string, string[]>
                {
                    {"Default", new []{"FB2"}},
                    {"Common", new []{"MOBI", "EPUB", "PDF"}},
                    {"Other", new []{"LIT", "LRF", "OEB", "PDB", "PML", "RB", "RTF", "TCR", "TXT", "TXTZ", "HTML", "HTMLZ", "SNB" }},
                };
            formats.Add("mobi");

            foreach (var group in formatsDic)
            {
                var galleryCategory = new RibbonGalleryCategory { Header = group.Key };
                foreach (var format in group.Value)
                {
                    var checkbox = new CheckBox { Content = format, IsEnabled = format != "FB2", IsChecked = format.IsOneOf("FB2", "MOBI") };

                    string formatLocal = format;
                    checkbox.Checked += (s, e) => formats.Add(formatLocal);
                    checkbox.Unchecked += (s, e) => formats.Remove(formatLocal);

                    galleryCategory.Items.Add(checkbox);
                }
                formatsGallery.Items.Add(galleryCategory);
            }
        }

        public static void SetWait(bool wait)
        {
            instance.SetWaitInternal(wait);
        }

        [OnGuiThread]
        private void SetWaitInternal(bool wait)
        {
            Cursor = wait ? Cursors.Wait : Cursors.Arrow;
            ribbon.IsEnabled = !wait;
            listView.IsEnabled = !wait;
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            logger.Info("e2Kindle {0} start", Assembly.GetExecutingAssembly().GetName().Version.ToString(2));
        }

        private void WindowClosing(object sender, CancelEventArgs e)
        {
            logger.Info("e2Kindle exit");
        }

        private void LoadFeedsClick(object sender, RoutedEventArgs e)
        {
            Dialogs.ShowProgress("Loading feeds...", LoadFeeds, marquee: true, showCancel: false);
        }

        [ExceptionLog]
        [ExceptionDialog]
        [UseSetWait]
        private void LoadFeeds()
        {
            logger.Info("----------");

            FeedsClear();

            logger.Info("Loading feeds from Google Reader");

            GoogleReader.SetCredentials(Settings.Default.GoogleUser, Settings.Default.GooglePassword);

            FeedsAddRange(GoogleReader.GetFeeds().OrderByDescending(gf => gf.UnreadCount));

            FeedsSetSelected(Feeds.Where(f => f.UnreadCount > 0));

            logger.Info(
                "Loaded {0} feeds ({2} unread entries in {1} feeds)",
                Feeds.Count(),
                Feeds.Count(f => f.UnreadCount > 0),
                Feeds.Sum(f => f.UnreadCount));

            logger.Info("----------");
        }

        private void CreateBookClick(object sender, RoutedEventArgs e)
        {
            Dialogs.ShowProgress("Creating book...", (pd, a) => CreateBook((i, s) => pd.ReportProgress(i, null, s)), showCancel: false);
        }

        [ExceptionLog]
        [ExceptionDialog]
        [UseSetWait]
        private void CreateBook(Action<int, string> progress)
        {
            if (FeedsGetSelected().Empty())
            {
                logger.Warn("No feeds selected.");
                return;
            }

            logger.Info("----------");

            var feeds = FeedsGetSelected().ToList();
            logger.Info("Getting feed entries ({0} feeds)", feeds.Count());

            GoogleReader.SetCredentials(Settings.Default.GoogleUser, Settings.Default.GooglePassword);

            progress(0, "Downloading feed entries...");

            var entries = GoogleReader.GetEntries(feeds)
                .Flatten()
                .ToArray();

            using (var writer = new StreamWriter("out.fb2"))
            {
                ContentProcess.CreateFb2(
                        writer,
                        entries,
                        (v, m) => progress(v * 100 / m, "Processing entries: {0}/{1} completed...".FormatWith(v, m)));
            }

            logger.Info("Feeds are downloaded and saved as FB2");

            if (XmlUtils.CheckValidFile("out.fb2")) logger.Info("FB2 file is valid as XML");
            else logger.Warn("FB2 file isn't valid as XML");

            if (formats.Any())
            {
                foreach (string format in formats)
                {
                    logger.Info("Converting to {0}", format);
                    progress(100, "Converting to {0}...".FormatWith(format));

                    string tFormat = format;
                    try
                    {
                        Calibre.Convert("out.fb2", tFormat);
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
                logger.Info("Marking downloaded and saved entries as read at Google Reader");
                progress(100, "Marking downloaded and saved entries as read at Google Reader");

                GoogleReader.MarkAsRead(entries);
                logger.Info("Marked all entries as read");
            }

            logger.Info("----------");
        }
    }
}
