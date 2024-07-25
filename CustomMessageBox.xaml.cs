using System.Windows;

namespace PS3_XMB_Tools
{
    public partial class CustomMessageBox : Window
    {
        public enum CustomMessageBoxResult
        {
            Clear,
            Resume,
            Cancel
        }

        public CustomMessageBoxResult Result { get; private set; }

        public CustomMessageBox(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Result = CustomMessageBoxResult.Clear;
            DialogResult = true;
            Close();
        }

        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            Result = CustomMessageBoxResult.Resume;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = CustomMessageBoxResult.Cancel;
            DialogResult = false;
            Close();
        }

        public static CustomMessageBoxResult Show(string message)
        {
            CustomMessageBox box = new CustomMessageBox(message);
            box.ShowDialog();
            return box.Result;
        }
    }
}
