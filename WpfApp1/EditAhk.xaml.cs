using System.IO;
using System.Windows;

namespace WpfApp1
{
    public partial class EditAhk : Window
    {
        private string filePath;

        public EditAhk()
        {
            InitializeComponent();
        }

        public void LoadFile(string path)
        {
            filePath = path;
            editorTextBox.Text = File.ReadAllText(path);
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            editorTextBox.FontSize += 2; // Augmente la taille de la police de 2 points
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (editorTextBox.FontSize > 2)
            {
                editorTextBox.FontSize -= 2; // Diminue la taille de la police de 2 points, si la taille actuelle est supérieure à 2 points
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                File.WriteAllText(filePath, editorTextBox.Text);
                MessageBox.Show("File saved successfully.");
            }
            else
            {
                MessageBox.Show("No file loaded.");
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    MessageBox.Show("File deleted successfully.");
                    Close(); // Ferme la fenêtre après la suppression du fichier
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Error deleting file: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("No file loaded.");
            }
        }
    }
}
