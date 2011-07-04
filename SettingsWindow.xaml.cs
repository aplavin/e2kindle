using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using e2Kindle.Properties;

namespace e2Kindle
{
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

            Settings.Default.Save();
        }

        private void Window_Loaded(object sender, EventArgs e)
        {
            tbUsername.Text = Settings.Default.GoogleUser;
            tbPassword.Text = Settings.Default.GooglePassword;
            cbMarkAsRead.IsChecked = Settings.Default.MarkAsRead;
            tbKindleEmail.Text = Settings.Default.KindleEmail;
            cbLoadFull.IsChecked = Settings.Default.LoadFullContent;
            tbFormats.Text = Settings.Default.NeededFormats;
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Hide();
            e.Cancel = true;
        }
    }
}
