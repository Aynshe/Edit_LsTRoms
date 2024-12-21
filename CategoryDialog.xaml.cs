using System.Collections.Generic;
using System.Windows;

namespace Edit_LsTRoms
{
    public partial class CategoryDialog : Window
    {
        private string? _selectedSystem;
        private string? _newSystem;
        private string? _category;
        private string? _newCategory;

        public string SelectedSystem => _selectedSystem ?? string.Empty;
        public string NewSystem => _newSystem ?? string.Empty;
        public string Category => _category ?? string.Empty;
        public string NewCategory => _newCategory ?? string.Empty;
        public bool IsNewSystem { get; private set; }

        public CategoryDialog(List<string> systems, List<string> categories)
        {
            InitializeComponent();
            systemComboBox.ItemsSource = systems;
            categoryComboBox.ItemsSource = categories;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedSystem = systemComboBox.SelectedItem as string;
            _newSystem = newSystemTextBox.Text;
            _category = categoryComboBox.SelectedItem as string;
            _newCategory = newCategoryTextBox.Text;
            IsNewSystem = !string.IsNullOrEmpty(_newSystem);
            DialogResult = true;
            Close();
        }
    }
}
