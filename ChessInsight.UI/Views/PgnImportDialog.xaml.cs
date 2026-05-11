using System.Windows;

namespace ChessInsight.UI.Views
{
    public partial class PgnImportDialog : Window
    {
        public string PgnText => TxtPgn.Text;

        public PgnImportDialog()
        {
            InitializeComponent();
            Loaded += (_, _) => TxtPgn.Focus();
        }

        public void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtPgn.Text))
            {
                ShowError("Unesite PGN tekst partije.");
                return;
            }
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) =>
            DialogResult = false;
    }
}
