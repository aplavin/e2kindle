namespace e2Kindle.UI
{
    using System;
    using System.ComponentModel;
    using System.Windows;
    using Ookii.Dialogs.Wpf;
    using e2Kindle.Aspects;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public static class Dialogs
    {
        [OnWorkerThread]
        public static void ShowException(Exception exception)
        {
            if (TaskDialog.OSSupportsTaskDialogs)
            {
                using (var dialog = new TaskDialog())
                {
                    dialog.WindowTitle = exception.GetType().ToString();
                    dialog.MainInstruction = exception.Message;
                    dialog.Content = "An exception occured: {0}".FormatWith(exception.GetType());
                    dialog.AllowDialogCancellation = true;
                    dialog.MainIcon = TaskDialogIcon.Error;

                    dialog.CollapsedControlText = "Additional information";
                    dialog.ExpandedInformation = exception.ToString();

                    dialog.Buttons.Add(new TaskDialogButton(ButtonType.Ok));
                    dialog.ShowDialog();
                }
            }
            else
            {
                MessageBox.Show(
                    "An exception occured: {0}".FormatWith(exception.GetType()) + "\n" + exception.Message,
                    exception.GetType().ToString(),
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }

        public static void ShowProgress(string title, Action<ProgressDialog, DoWorkEventArgs> action, bool marquee = false, bool showCancel = true, bool showRemaining = false)
        {
            using (var dialog = new ProgressDialog())
            {
                dialog.WindowTitle = title;
                dialog.Text = title;
                dialog.ShowTimeRemaining = showRemaining;
                dialog.ProgressBarStyle = marquee ? ProgressBarStyle.MarqueeProgressBar : ProgressBarStyle.ProgressBar;
                dialog.ShowCancelButton = showCancel;

                dialog.DoWork += (s, e) => action(dialog, e);

                dialog.Show();
            }
        }

        public static void ShowProgress(string title, Action action, bool marquee = false, bool showCancel = true, bool showRemaining = false)
        {
            ShowProgress(title, (pd, e) => action(), marquee, showCancel, showRemaining);
        }
    }
}