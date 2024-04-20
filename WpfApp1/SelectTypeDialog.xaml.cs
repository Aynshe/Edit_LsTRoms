using System.Windows;
using System.Windows.Controls;

namespace WpfApp1
{
    public partial class SelectTypeDialog : Window
    {
        // Propriété publique pour obtenir le type sélectionné
        public string SelectedType { get; private set; }

        // Propriété publique pour obtenir le nom du jeu ou du système
        public string SelectedName { get; private set; }

        public SelectTypeDialog()
        {
            InitializeComponent();
        }

        // Méthode pour initialiser la fenêtre avec le nom du jeu ou du système sélectionné
        public void Initialize(string selectedName)
        {
            nameTextBox.Text = selectedName;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Récupérer le type sélectionné dans le ComboBox
            SelectedType = (typeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            // Récupérer le nom du jeu ou du système
            SelectedName = nameTextBox.Text.Trim();

            // Fermer la fenêtre de dialogue avec DialogResult=true si le nom est valide
            if (!string.IsNullOrWhiteSpace(SelectedName))
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
