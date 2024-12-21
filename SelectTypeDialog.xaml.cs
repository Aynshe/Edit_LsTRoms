using System.Windows;
using System.Windows.Controls;

namespace Edit_LsTRoms
{
    public partial class SelectTypeDialog : Window
    {
        private string? _selectedType;
        private string? _selectedName;

        public string SelectedType => _selectedType ?? string.Empty;
        public string SelectedName => _selectedName ?? string.Empty;

        public SelectTypeDialog()
        {
            InitializeComponent();
        }

        public void Initialize(string selectedName)
        {
            nameTextBox.Text = selectedName;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Récupérer le type sélectionné dans le ComboBox
            _selectedType = (typeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            // Récupérer le nom du jeu ou du système
            _selectedName = nameTextBox.Text.Trim();

            // Fermer la fenêtre de dialogue avec DialogResult=true si le nom est valide
            if (!string.IsNullOrWhiteSpace(_selectedName))
                DialogResult = true;
            else
                MessageBox.Show("Please enter a valid name.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Fermer la fenêtre de dialogue avec DialogResult=false
            DialogResult = false;
        }
    }
}
