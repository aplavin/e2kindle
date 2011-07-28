namespace e2Kindle.UI
{
    using System;
    using System.ComponentModel;
    using System.Threading;
    using System.Windows;

    using Ookii.Dialogs.Wpf;

    using e2Kindle.Aspects;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public static class Dialogs
    {
        [OnWorkerThread]
        public static void ShowException(Exception exception, string windowTitle = null, string mainInstruction = null)
        {
            if (windowTitle == null)
            {
                windowTitle = exception.GetType().ToString();
            }

            if (mainInstruction == null)
            {
                mainInstruction = "An exception occured: {0}".FormatWith(exception.GetType());
            }

            if (TaskDialog.OSSupportsTaskDialogs)
            {
                using (var dialog = new TaskDialog())
                {
                    dialog.WindowTitle = windowTitle;
                    dialog.MainInstruction = mainInstruction;
                    dialog.Content = exception.Message;
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
                MessageBox.Show(mainInstruction + "\n" + exception.Message, windowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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