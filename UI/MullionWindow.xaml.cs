using System.Windows;

namespace Bulico
{
    public partial class MullionWindow : Window
    {
        public string InputText { get; private set; }
        public bool IsFromBottom => FromBottom.IsChecked == true;

        public MullionWindow()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string input = OffsetInput.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                MessageBox.Show("请输入偏移值！", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            InputText = input;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
