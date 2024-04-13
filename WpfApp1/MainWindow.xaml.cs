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
            private readonly Dictionary<string, List<string[]>> systems = new Dictionary<string, List<string[]>>();
            private readonly Dictionary<string, int> systemCounters = new Dictionary<string, int>();
            private readonly Dictionary<string, Dictionary<string, int>> categoryCounters = new Dictionary<string, Dictionary<string, int>>();

        public MainWindow()
        {
            InitializeComponent();

            UpdateToggleButtonState();

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
            try
            {
                return Directory.GetFiles(directory, "roms.ini", SearchOption.AllDirectories).FirstOrDefault();
            }
            catch (UnauthorizedAccessException) { }
            catch (PathTooLongException) { }

            return null;
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
                   var data = cell.Item as string[];
                    data[Array.IndexOf(romsDataGrid.Columns.Cast<DataGridColumn>().ToArray(), cell.Column)] = editBox.Text;
                }
            }
            UpdateUnderlyingData(); // Mettre à jour les données sous-jacentes après les modifications
            UpdateKeys(); // Mettre à jour les clés si nécessaire
            UpdateDataGrid(); // Mettre à jour le DataGrid pour refléter les modifications
        }

        private void EditBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (editBox.Text == "Text...")
            {
                editBox.Text = "";
                editBox.Foreground = Brushes.Black; // Changez la couleur du texte pour qu'il soit visible
            }
        }

        private void EditBox_LostFocus(object sender, RoutedEventArgs e)
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

        private void UpdateConfigFileWithNewSystem(string newSystem)
        {
            string configFile = ".config";
            List<string> lines = File.ReadAllLines(configFile).ToList();

            // Recherche de la ligne contenant la section [systems]
            int systemsIndex = lines.FindIndex(line => line.Trim() == "[systems]");
            if (systemsIndex != -1)
            {
                // Vérifiez si le nouveau système existe déjà dans la section [systems]
                int existingSystemIndex = lines.FindIndex(systemsIndex + 1, line => line.Trim() == newSystem);
                if (existingSystemIndex == -1)
                {
                    // Si le nouveau système n'existe pas encore, ajoutez-le après la section [systems]
                    lines.Insert(systemsIndex + 1, newSystem);
                    File.WriteAllLines(configFile, lines);
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

                    // Ajouter le nouveau système au fichier .config
                    UpdateConfigFileWithNewSystem(system);
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
/// <summary>
/// zone debut delete
/// </summary>

        private void RemoveEmptySystems()
        {
            // Récupérer les systèmes sans catégorie
            List<string> emptySystems = systems.Where(system => system.Value.Count == 0).Select(system => system.Key).ToList();

            // Supprimer les sections vides du fichier roms.ini
            string romsIniPath = FindRomsIniFile(Directory.GetCurrentDirectory());
            if (romsIniPath != null)
            {
                List<string> romsIniLines = File.ReadAllLines(romsIniPath).ToList();

                foreach (var emptySystem in emptySystems)
                {
                    romsIniLines.RemoveAll(line => line.Trim() == $"[{emptySystem}]");
                }

                File.WriteAllLines(romsIniPath, romsIniLines);
            }
        }

        private void UpdateConfigFileWithRemovedSystems()
        {
            string configFile = ".config";
            List<string> lines = File.ReadAllLines(configFile).ToList();

            // Recherche de la ligne contenant la section [systems]
            int systemsIndex = lines.FindIndex(line => line.Trim() == "[systems]");
            if (systemsIndex != -1)
            {
                // Suppression des noms de système qui n'ont plus de catégorie
                foreach (var system in systems.Keys)
                {
                    if (!systems[system].Any())
                    {
                        lines.RemoveAll(line => line.Trim() == system);
                    }
                }

                File.WriteAllLines(configFile, lines);
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

            RemoveEmptySystems(); // Supprimer les systèmes sans catégorie du fichier roms.ini
            UpdateConfigFileWithRemovedSystems(); // Mettre à jour la section [systems] du fichier .config
            UpdateDataGrid();
            SaveData();
        }

        private void DeleteGamesButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedGames();
        }

        private void RemoveSystemIfNoGames()
        {
            // Récupérer les systèmes sans clé de jeu
            List<string> systemsWithoutGames = systems.Where(system => system.Value.Count == 0).Select(system => system.Key).ToList();

            // Supprimer les sections de ces systèmes du fichier roms.ini
            string romsIniPath = FindRomsIniFile(Directory.GetCurrentDirectory());
            if (romsIniPath != null)
            {
                List<string> romsIniLines = File.ReadAllLines(romsIniPath).ToList();

                foreach (var system in systemsWithoutGames)
                {
                    romsIniLines.RemoveAll(line => line.Trim() == $"[{system}]");
                }

                File.WriteAllLines(romsIniPath, romsIniLines);
            }
        }

        private void RemoveEmptySystemsFromRomsIni()
        {
            // Récupérer les systèmes sans clé de jeu ni de catégorie
            List<string> emptySystems = systems.Where(system => system.Value.Count == 0).Select(system => system.Key).ToList();

            // Supprimer les sections vides du fichier roms.ini
            string romsIniPath = FindRomsIniFile(Directory.GetCurrentDirectory());
            if (romsIniPath != null)
            {
                List<string> romsIniLines = File.ReadAllLines(romsIniPath).ToList();

                foreach (var emptySystem in emptySystems)
                {
                    // Recherche de la ligne correspondant à la section du système dans le fichier roms.ini
                    int startIndex = romsIniLines.FindIndex(line => line.Trim() == $"[{emptySystem}]");
                    if (startIndex != -1)
                    {
                        // Supprimer toutes les lignes jusqu'à la prochaine section ou la fin du fichier
                        int endIndex = romsIniLines.FindIndex(startIndex + 1, line => line.StartsWith("["));
                        if (endIndex == -1)
                        {
                            endIndex = romsIniLines.Count;
                        }
                        romsIniLines.RemoveRange(startIndex, endIndex - startIndex);
                    }
                }

                File.WriteAllLines(romsIniPath, romsIniLines);
            }
        }

        /// <summary>
        /// zone fin delete
        /// </summary>

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
                    if (systemEntry.Value.Count > 0) // Ajouter seulement les systèmes avec des jeux
                    {
                        romsIniLines.Add($"[{system}]");
                        foreach (var gameDetails in systemEntry.Value)
                        {
                            string key = gameDetails[0];
                            string values = string.Join("|", gameDetails.Skip(1));
                            romsIniLines.Add($"{key} = {values}");
                        }
                    }
                }

                File.WriteAllLines(romsIniPath, romsIniLines);

                // Supprimer les sections de systèmes vides du fichier roms.ini
                RemoveEmptySystemsFromRomsIni();

                // Rafraîchir la liste après la sauvegarde
                UpdateDataGrid();

                // Assurez-vous de mettre à jour les données après la sauvegarde
                string[] lines = File.ReadAllLines(romsIniPath);
                UpdateSystemsFromRomsIni(lines);
            }
            else
            {
                MessageBox.Show("Fichier roms.ini non trouvé.");
            }
        }

        private void UpdateSystemsFromRomsIni(string[] lines)
        {
            // Effacer les données existantes
            systems.Clear();
            systemCounters.Clear();
            categoryCounters.Clear();

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
        }

        private void UpdateToggleButtonState()
        {
            string configFile = ".config";
            bool isDSUpdtlockEnabled = false; // Valeur par défaut

            // Vérifier si le fichier de configuration existe et lire la valeur de DSUpdtlock
            if (File.Exists(configFile))
            {
                string[] lines = File.ReadAllLines(configFile);
                foreach (string line in lines)
                {
                    if (line.StartsWith("DSUpdtlock="))
                    {
                        isDSUpdtlockEnabled = line.Split('=')[1].Trim().ToLower() == "true";
                        break;
                    }
                }
            }
            else
            {
                // Créer le fichier de configuration avec une valeur par défaut
                UpdateConfigFile("DSUpdtlock=false");
            }

            // Mettre à jour le bouton ToggleButton avec la valeur lue du fichier de configuration
            toggleDSUpdtlock.IsChecked = isDSUpdtlockEnabled;
            toggleDSUpdtlock.Content = isDSUpdtlockEnabled ? "On" : "Off";
        }


        private void ToggleDSUpdtlock_Checked(object sender, RoutedEventArgs e)
        {
            // Mettre à jour le fichier de configuration lorsque la case est cochée
            UpdateConfigFile("DSUpdtlock=true");

            // Mettre à jour le texte du bouton
            toggleDSUpdtlock.Content = "On";
        }

        private void ToggleDSUpdtlock_Unchecked(object sender, RoutedEventArgs e)
        {
            // Mettre à jour le fichier de configuration lorsque la case est décochée
            UpdateConfigFile("DSUpdtlock=false");

            // Mettre à jour le texte du bouton
            toggleDSUpdtlock.Content = "Off";
        }

        private void UpdateConfigFile(string newLine)
        {
            string configFile = ".config";
            string[] lines = File.ReadAllLines(configFile);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("DSUpdtlock="))
                {
                    lines[i] = newLine;
                    break;
                }
            }
            File.WriteAllLines(configFile, lines);
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