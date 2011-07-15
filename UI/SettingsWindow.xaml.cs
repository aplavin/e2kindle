namespace e2Kindle.UI
{
    using System;
    using System.ComponentModel;
    using System.Windows;

    using e2Kindle.Properties;

    using GoogleAPI.GoogleReader;

    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void OkClick(object sender, RoutedEventArgs e)
        {
            Hide();

            Settings.Default.GoogleUser = tbUsername.Text;
            Settings.Default.GooglePassword = tbPassword.Text;
            Settings.Default.MarkAsRead = (bool)cbMarkAsRead.IsChecked;
            Settings.Default.KindleEmail = tbKindleEmail.Text;
            Settings.Default.LoadFullContent = (bool)cbLoadFull.IsChecked;
            Settings.Default.NeededFormats = tbFormats.Text;

            GoogleReader.SetCredentials(Settings.Default.GoogleUser, Settings.Default.GooglePassword);

            Settings.Default.Save();
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void WindowLoaded(object sender, EventArgs e)
        {
            tbUsername.Text = Settings.Default.GoogleUser;
            tbPassword.Text = Settings.Default.GooglePassword;
            cbMarkAsRead.IsChecked = Settings.Default.MarkAsRead;
            tbKindleEmail.Text = Settings.Default.KindleEmail;
            cbLoadFull.IsChecked = Settings.Default.LoadFullContent;
            tbFormats.Text = Settings.Default.NeededFormats;
        }

        private void WindowClosing(object sender, CancelEventArgs e)
        {
            Hide();
            e.Cancel = true;
        }
    }
}
