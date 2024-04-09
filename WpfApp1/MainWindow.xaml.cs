using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private Dictionary<string, List<string[]>> systems = new Dictionary<string, List<string[]>>();
        private Dictionary<string, int> systemCounters = new Dictionary<string, int>();
        private Dictionary<string, Dictionary<string, int>> categoryCounters = new Dictionary<string, Dictionary<string, int>>();

        public MainWindow()
        {
            InitializeComponent();

            // Trouver le fichier roms.ini
            string romsIniPath = FindRomsIniFile(Directory.GetCurrentDirectory());

            if (romsIniPath != null && File.Exists(romsIniPath))
            {
                string[] lines = File.ReadAllLines(romsIniPath);

                string currentSystem = null;
                foreach (string line in lines)
                {
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        // C'est un système
                        currentSystem = line.Trim('[', ']');
                        systems[currentSystem] = new List<string[]>();
                        systemCounters[currentSystem] = 0;
                        categoryCounters[currentSystem] = new Dictionary<string, int>();
                    }
                    else if (line.Contains("="))
                    {
                        // C'est une entrée de jeu
                        var parts = line.Split('=');
                        string key = parts[0].Trim();
                        var gameDetails = parts[1].Split('|').Select(detail => detail.Trim()).ToArray();
                        systems[currentSystem].Add(new string[] { key }.Concat(gameDetails).ToArray());

                        // Mettre à jour categoryCounters
                        string category = key.Split('_')[0];
                        int number = int.Parse(key.Split('_')[1]);
                        if (!categoryCounters[currentSystem].ContainsKey(category) || number > categoryCounters[currentSystem][category])
                        {
                            categoryCounters[currentSystem][category] = number;
                        }
                    }
                }

                // Mettre à jour le DataGrid
                UpdateDataGrid();
            }
        }

        private string FindRomsIniFile(string directory)
        {
            string[] files = null;
            try
            {
                files = Directory.GetFiles(directory, "roms.ini", SearchOption.AllDirectories);
            }
            catch (UnauthorizedAccessException) { }
            catch (PathTooLongException) { }

            if (files != null && files.Length > 0)
            {
                return files[0]; // Retourne le premier fichier roms.ini trouvé
            }
            else
            {
                return null; // Aucun fichier roms.ini trouvé
            }
        }

        private void UpdateDataGrid()
        {
            romsDataGrid.Columns.Clear();
            romsDataGrid.Columns.Add(new DataGridTextColumn { Header = "System", Binding = new Binding("[0]") });
            romsDataGrid.Columns.Add(new DataGridTextColumn { Header = "Key", Binding = new Binding("[1]") });
            romsDataGrid.Columns.Add(new DataGridTextColumn { Header = "GameName", Binding = new Binding("[2]") });
            romsDataGrid.Columns.Add(new DataGridTextColumn { Header = "-rom=", Binding = new Binding("[3]") });
            romsDataGrid.Columns.Add(new DataGridTextColumn { Header = "VersionX", Binding = new Binding("[4]") });
            romsDataGrid.Columns.Add(new DataGridTextColumn { Header = "-target=", Binding = new Binding("[5]") });
            romsDataGrid.Columns.Add(new DataGridTextColumn { Header = "delayForStart", Binding = new Binding("[6]") });
            romsDataGrid.Columns.Add(new DataGridTextColumn { Header = "-arguments or false", Binding = new Binding("[7]") });
            romsDataGrid.Columns.Add(new DataGridCheckBoxColumn { Header = "JoyToKey", Binding = new Binding("[8]") });
            romsDataGrid.Columns.Add(new DataGridCheckBoxColumn { Header = "nomousy", Binding = new Binding("[9]") });


            List<string[]> allGameDetails = new List<string[]>();
            foreach (var systemEntry in systems)
            {
                string systemName = systemEntry.Key;
                List<string[]> systemDetails = systemEntry.Value;

                foreach (string[] gameDetails in systemDetails)
                {
                    allGameDetails.Add(new string[] { systemName }.Concat(gameDetails).ToArray());
                }
            }
            romsDataGrid.ItemsSource = allGameDetails;
        }

        private void ToggleSelectionUnit_Checked(object sender, RoutedEventArgs e)
        {
            romsDataGrid.SelectionUnit = DataGridSelectionUnit.CellOrRowHeader;
        }

        private void ToggleSelectionUnit_Unchecked(object sender, RoutedEventArgs e)
        {
            romsDataGrid.SelectionUnit = DataGridSelectionUnit.FullRow;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string romsIniPath = FindRomsIniFile(Directory.GetCurrentDirectory());

            if (romsIniPath != null)
            {
                // Mettre à jour les données sous-jacentes avant de sauvegarder
                UpdateUnderlyingData();
                UpdateKeys();

                // Écrire toutes les données dans le fichier roms.ini
                List<string> romsIniLines = new List<string>();
                foreach (var systemEntry in systems)
                {
                    string system = systemEntry.Key;
                    romsIniLines.Add($"[{system}]");
                    foreach (var gameDetails in systemEntry.Value)
                    {
                        string key = gameDetails[0];
                        string values = string.Join("|", gameDetails.Skip(1));
                        romsIniLines.Add($"{key} = {values}");
                    }
                }

                File.WriteAllLines(romsIniPath, romsIniLines);
            }
            else
            {
                MessageBox.Show("Fichier roms.ini non trouvé.");
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (romsDataGrid.SelectionUnit != DataGridSelectionUnit.CellOrRowHeader)
            {
                MessageBox.Show("Veuillez passer en mode de sélection de cellule pour modifier le texte des cellules.");
                return;
            }

            foreach (var cell in romsDataGrid.SelectedCells)
            {
                var column = cell.Column as DataGridBoundColumn;
                if (column != null)
                {
                    var c = column.Binding as Binding;
                    var data = cell.Item as string[];
                    data[Array.IndexOf(romsDataGrid.Columns.Cast<DataGridColumn>().ToArray(), cell.Column)] = editBox.Text;
                }
            }
            UpdateUnderlyingData(); // Mettre à jour les données sous-jacentes après les modifications
            UpdateKeys(); // Mettre à jour les clés si nécessaire
            UpdateDataGrid(); // Mettre à jour le DataGrid pour refléter les modifications
        }

        private void editBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (editBox.Text == "Text...")
            {
                editBox.Text = "";
                editBox.Foreground = Brushes.Black; // Changez la couleur du texte pour qu'il soit visible
            }
        }

        private void editBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(editBox.Text))
            {
                editBox.Text = "Text...";
                editBox.Foreground = Brushes.Gray; // Revert à la couleur de texte gris clair
            }
        }

        private void UpdateKeys()
        {
            foreach (var systemEntry in systems)
            {
                string systemName = systemEntry.Key;
                List<string[]> systemDetails = systemEntry.Value;
                int counter = 0;
                foreach (string[] gameDetails in systemDetails)
                {
                    string category = gameDetails[0].Split('_')[0];
                    gameDetails[0] = $"{category}_{++counter}";
                }
            }
        }

        private void UpdateUnderlyingData()
        {
            // Mise à jour des données sous-jacentes pour refléter les modifications dans l'interface utilisateur
            foreach (var item in romsDataGrid.ItemsSource)
            {
                var gameDetails = item as string[];
                string systemName = gameDetails[0];
                string key = gameDetails[1];
                string[] details = gameDetails.Skip(2).ToArray(); // Ignorer les deux premières colonnes (System, Key)
                int index = systems[systemName].FindIndex(g => g[0] == key);
                if (index != -1)
                {
                    systems[systemName][index] = new string[] { key }.Concat(details).ToArray();
                }
            }
        }

        private void AddGameButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CategoryDialog(systems.Keys.ToList(), systems.SelectMany(s => s.Value.Select(v => v[0].Split('_')[0])).Distinct().ToList());
            if (dialog.ShowDialog() == true)
            {
                string system = dialog.SelectedSystem;
                if (dialog.IsNewSystem)
                {
                    system = dialog.NewSystem;
                    systems[system] = new List<string[]>();
                    systemCounters[system] = 0;
                    categoryCounters[system] = new Dictionary<string, int>();
                }
                string category = dialog.Category;
                if (!string.IsNullOrEmpty(dialog.NewCategory))
                {
                    category = dialog.NewCategory;
                    categoryCounters[system][category] = 0;
                }
                else if (!categoryCounters[system].ContainsKey(category))
                {
                    categoryCounters[system][category] = systems[system].Count(g => g[0].StartsWith(category + "_"));
                }
                string key = $"{category}_{++categoryCounters[system][category]}";
                systems[system].Add(new string[] { key, "", "", "", "", "", "", "", "" });
                UpdateDataGrid();
            }
        }

        private void RemoveGameFromSystem(string systemName, string key)
        {
            if (systems.ContainsKey(systemName))
            {
                systems[systemName].RemoveAll(game => game[0] == key);
            }
        }

        private void DeleteSelectedGames()
        {
            var selectedGames = romsDataGrid.SelectedItems.Cast<string[]>().ToList();

            foreach (var game in selectedGames)
            {
                string systemName = game[0];
                string key = game[1];

                RemoveGameFromSystem(systemName, key);
            }

            UpdateDataGrid();
            SaveData();
        }

        private void DeleteGamesButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedGames();
        }

        private void SaveData()
        {
            string romsIniPath = FindRomsIniFile(Directory.GetCurrentDirectory());

            if (romsIniPath != null)
            {
                // Mettre à jour les données sous-jacentes avant de sauvegarder
                UpdateUnderlyingData();
                UpdateKeys();

                // Écrire toutes les données dans le fichier roms.ini
                List<string> romsIniLines = new List<string>();
                foreach (var systemEntry in systems)
                {
                    string system = systemEntry.Key;
                    romsIniLines.Add($"[{system}]");
                    foreach (var gameDetails in systemEntry.Value)
                    {
                        string key = gameDetails[0];
                        string values = string.Join("|", gameDetails.Skip(1));
                        romsIniLines.Add($"{key} = {values}");
                    }
                }

                File.WriteAllLines(romsIniPath, romsIniLines);
                // Rafraîchir la liste après la sauvegarde
                UpdateDataGrid();
            }
            else
            {
                MessageBox.Show("Fichier roms.ini non trouvé.");
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchTerm = searchBox.Text.ToLower();
            List<string[]> filteredGameDetails = new List<string[]>();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                foreach (var systemEntry in systems)
                {
                    string systemName = systemEntry.Key;
                    List<string[]> systemDetails = systemEntry.Value;

                    foreach (string[] gameDetails in systemDetails)
                    {
                        if (gameDetails[1].ToLower().StartsWith(searchTerm)) // Si le nom du jeu commence par le terme de recherche
                        {
                            filteredGameDetails.Add(new string[] { systemName }.Concat(gameDetails).ToArray());
                        }
                    }
                }
            }
            else
            {
                // Si le champ de recherche est vide, afficher tous les jeux
                foreach (var systemEntry in systems)
                {
                    string systemName = systemEntry.Key;
                    List<string[]> systemDetails = systemEntry.Value;

                    foreach (string[] gameDetails in systemDetails)
                    {
                        filteredGameDetails.Add(new string[] { systemName }.Concat(gameDetails).ToArray());
                    }
                }
            }

            romsDataGrid.ItemsSource = filteredGameDetails; // Mettre à jour le DataGrid avec les jeux filtrés
        }
    }
}
