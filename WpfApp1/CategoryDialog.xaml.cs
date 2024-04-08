using System.Collections.Generic;
using System.Windows;

namespace WpfApp1
{
    public partial class CategoryDialog : Window
    {
        public string SelectedSystem { get; private set; }
        public string NewSystem { get; private set; }
        public string Category { get; private set; }
        public string NewCategory { get; private set; }
        public bool IsNewSystem { get; private set; }

        public CategoryDialog(List<string> systems, List<string> categories)
        {
            InitializeComponent();
            systemComboBox.ItemsSource = systems;
            categoryComboBox.ItemsSource = categories;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedSystem = systemComboBox.SelectedItem as string;
            NewSystem = newSystemTextBox.Text;
            Category = categoryComboBox.SelectedItem as string;
            NewCategory = newCategoryTextBox.Text;
            IsNewSystem = !string.IsNullOrEmpty(NewSystem);
            DialogResult = true;
            Close();
        }
    }
}
