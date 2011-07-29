namespace e2Kindle.UI
{
    using System.Linq;
    using System.Windows.Controls;

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
