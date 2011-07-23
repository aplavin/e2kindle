using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace e2Kindle.UI
{
    using System.ComponentModel;
    using System.Diagnostics;

    /// <summary>
    /// Interaction logic for RibbonPasswordTextBox.xaml
    /// </summary>
    public partial class RibbonPasswordTextBox
    {
        public new string Text { get; set; }
        public char PasswordChar { get; set; }

        public RibbonPasswordTextBox()
        {
            InitializeComponent();

            Text = string.Empty;
            PasswordChar = '*';
        }

        private void ThisTextChanged(object sender, TextChangedEventArgs e)
        {
            if (base.Text.Any(ch => ch != PasswordChar) || base.Text.Length != Text.Length)
            {
                var textChange = e.Changes.Single();
                int offset = textChange.Offset;
                int addedLength = textChange.AddedLength;
                int removedLength = textChange.RemovedLength;

                if (addedLength > 0)
                {
                    Text = Text.Insert(offset, base.Text[offset].ToString());

                    int caret = CaretIndex;
                    base.Text = base.Text.Replace(offset, PasswordChar);
                    CaretIndex = caret;
                }
                else if (removedLength > 0)
                {
                    Text = Text.Remove(offset, removedLength);
                }

            }
        }
    }
}
