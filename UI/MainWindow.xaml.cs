namespace e2Kindle.UI
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Input;
    using System.Windows.Media;

    using e2Kindle.Aspects;
    using e2Kindle.ContentProcess;
    using e2Kindle.Properties;
    using GoogleAPI.GoogleReader;
    using Microsoft.Windows.Controls.Ribbon;
    using NLog;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static MainWindow _instance;
        private readonly HashSet<string> formats = new HashSet<string>();

        #region Feeds collection management
        // For WPF binding.
        public ObservableCollection<Feed> Feeds { get; private set; }

        public Dictionary<Feed, FeedSettings> FeedSettings { get; private set; }

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
        private Feed FeedsGetSelected()
        {
            return listView.SelectedItem as Feed;
        }
        #endregion

        public MainWindow()
        {
            Feeds = new ObservableCollection<Feed>();
            Feeds.CollectionChanged += (s, e) => FeedSettings = Feeds.ToDictionary(f => f, f => new FeedSettings(f));

            InitializeComponent();
            _instance = this;

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
            _instance.SetWaitInternal(wait);
        }

        [OnGuiThread]
        private void SetWaitInternal(bool wait)
        {
            Cursor = wait ? Cursors.Wait : Cursors.Arrow;
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            logger.Info("e2Kindle {0} start", Assembly.GetExecutingAssembly().GetName().Version.ToString(2));
        }

        private void WindowClosing(object sender, CancelEventArgs e)
        {
            logger.Info("e2Kindle exit");
            Settings.Default.Save();
        }

        private void LoadFeedsClick(object sender, RoutedEventArgs e)
        {
            Dialogs.ShowProgress("Loading feeds...", LoadFeeds, marquee: true, showCancel: false);
        }

        [ExceptionHandle(ExceptionHandling.Dialog | ExceptionHandling.Log)]
        [UseSetWait]
        private void LoadFeeds()
        {
            logger.Info("----------");

            FeedsClear();

            logger.Info("Loading feeds from Google Reader");

            GoogleReader.SetCredentials(Settings.Default.GoogleUser, Settings.Default.GooglePassword);

            FeedsAddRange(GoogleReader.GetFeeds().OrderByDescending(gf => gf.UnreadCount));

            Feeds.ForEach(f => FeedSettings[f].Enabled = f.UnreadCount > 0);
            Feeds.ForEach(UpdateItemStyle);

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

        [ExceptionHandle(ExceptionHandling.Dialog | ExceptionHandling.Log)]
        [UseSetWait]
        private void CreateBook(Action<int, string> progress)
        {
            if (Feeds.Empty()) return;

            logger.Info("----------");

            GoogleReader.SetCredentials(Settings.Default.GoogleUser, Settings.Default.GooglePassword);
            using (var writer = new StreamWriter("out.fb2"))
            {
                ContentProcess.CreateFb2(
                        writer,
                        Feeds,
                        FeedSettings,
                        progress);
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

                        if (format.ToLower() == "mobi" && Kindle.IsConnected)
                        {
                            Kindle.SendDocument("out.mobi");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.ErrorException("Conversion to {0} failed".FormatWith(format), ex);
                    }
                }

                logger.Info("Converted to all specified formats");
            }

            logger.Info("----------");
        }

        private void OpenFolderClick(object sender, RoutedEventArgs e)
        {
            Process.Start(".");
        }

        private void ListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = FeedsGetSelected();

            if (selected == null)
            {
                feedButton.IsEnabled = false;
            }
            else
            {
                feedButton.IsEnabled = true;

                feedButton.IsChecked = FeedSettings[selected].Enabled;
                loadFullContent.IsChecked = FeedSettings[selected].FullContent;
                allEntries.IsChecked = FeedSettings[selected].LoadAllEntries;
                unreadEntriesOnly.IsChecked = !allEntries.IsChecked;
                loadImages.IsChecked = FeedSettings[selected].LoadImages;
            }
        }

        private void FeedSettingsChanged(object sender, object e)
        {
            var selected = FeedsGetSelected();

            if (selected != null)
            {
                FeedSettings[selected].Enabled = feedButton.IsChecked;
                FeedSettings[selected].FullContent = loadFullContent.IsChecked.To<bool>();
                FeedSettings[selected].LoadAllEntries = allEntries.IsChecked.To<bool>();
                FeedSettings[selected].LoadImages = loadImages.IsChecked.To<bool>();

                UpdateItemStyle(selected);
            }
        }

        [OnGuiThread]
        private void UpdateItemStyle(Feed item)
        {
            var lvItem = (ListViewItem)listView.ItemContainerGenerator.ContainerFromItem(item);
            if (lvItem == null) return;

            var settings = FeedSettings[item];

            lvItem.Foreground = settings.Enabled ? listView.Foreground : Brushes.Gray;
        }
    }
}