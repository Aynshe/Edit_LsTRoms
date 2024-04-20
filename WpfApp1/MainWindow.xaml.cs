using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

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

                // mettre à jour la liste des fichiers .ahk
                romsDataGrid.SelectionChanged += RomsDataGrid_SelectionChanged;

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
                MessageBox.Show("roms.ini file not found.");
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (romsDataGrid.SelectionUnit != DataGridSelectionUnit.CellOrRowHeader)
            {
                MessageBox.Show("Please switch to cell selection mode to edit cell text.");
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
                MessageBox.Show("roms.ini file not found.");
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


        private void RomsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Réinitialiser les ListBox
            ahkFilesListBox.ItemsSource = null;
            ahkFilesListBoxGame.ItemsSource = null;
            ahkFilesListBoxCustom.ItemsSource = null;

            if (romsDataGrid.SelectedItem != null)
            {
                string[] selectedGame = romsDataGrid.SelectedItem as string[];
                string systemName = selectedGame[0];
                string gameName = selectedGame[2]; // Récupérer le nom du jeu sélectionné

                // Construire le chemin du dossier du système correspondant
                string systemFolderPath = Path.Combine(Directory.GetCurrentDirectory(), systemName);

                // Charger les fichiers .ahk du dossier du système correspondant
                List<string> systemAhkFiles = LoadAhkFiles(systemFolderPath);

                if (!string.IsNullOrEmpty(gameName))
                {
                    // Construire le chemin du fichier .ahk correspondant au jeu sélectionné
                    string gameAhkFile = $"{gameName}.ahk"; // Nom du fichier .ahk

                    // Vérifier si le fichier .ahk du jeu existe dans le dossier du système
                    if (systemAhkFiles.Contains(gameAhkFile))
                    {
                        // Afficher le fichier .ahk correspondant au jeu sélectionné
                        ahkFilesListBoxGame.ItemsSource = new List<string> { gameAhkFile };
                    }
                    else
                    {
                        // Afficher "No AutoHotkey game found" si aucun fichier .ahk correspondant au jeu n'est trouvé
                        ahkFilesListBoxGame.ItemsSource = new List<string> { "No AutoHotkey game found" };
                    }

                    // Afficher le fichier .ahk du nom du système si le .ahk du jeu n'existe pas
                    string systemAhkFile = $"{systemName}.ahk";
                    if (systemAhkFiles.Contains(systemAhkFile))
                    {
                        ahkFilesListBox.ItemsSource = new List<string> { systemAhkFile };
                    }
                    else
                    {
                        // Afficher "No AutoHotkey system found" si aucun fichier .ahk correspondant au système n'est trouvé
                        ahkFilesListBox.ItemsSource = new List<string> { "No AutoHotkey system found" };
                    }
                }

                // Charger les fichiers .ahk du dossier personnalisé
                string customAhkFolderPath = Path.Combine(systemFolderPath, "ahk", ".serial-send", ".edit");
                List<string> customAhkFiles = LoadCustomAhkFiles(customAhkFolderPath);
                ahkFilesListBoxCustom.ItemsSource = customAhkFiles;

                // Si aucun fichier .ahk n'a été trouvé pour le jeu, le système ou le dossier personnalisé, afficher "No AutoHotkey found"
                if ((ahkFilesListBox.Items.Count == 0 && ahkFilesListBoxGame.Items.Count == 0 && ahkFilesListBoxCustom.Items.Count == 0) ||
                    (ahkFilesListBox.Items.Count == 0 && ahkFilesListBoxGame.Items[0].ToString() == "No AutoHotkey game found") ||
                    (ahkFilesListBoxCustom.Items.Count == 0))
                {
                    ahkFilesListBoxCustom.ItemsSource = new List<string> { "No AutoHotkey found" };
                }

                // Mettre à jour le TextBlock avec le nom du système sélectionné
                selectedSystemTextBlock.Text = $"System : {systemName}";

                // Charger le contenu du fichier system-config.prc et l'afficher dans le TextBox
                string systemConfigPath = Path.Combine(Directory.GetCurrentDirectory(), systemName, "ahk", "system-config.prc");

                if (File.Exists(systemConfigPath))
                {
                    string configContent = File.ReadAllText(systemConfigPath);
                    configTextBox.Text = configContent;
                }
                else
                {
                    // Indiquer que le fichier system-config.prc est absent
                    configTextBox.Text = "The file system-config.prc is not found.";
                }
            }
        }

        private void AhkFilesListBoxCustom_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ahkFilesListBoxCustom.SelectedItem != null)
            {
                string selectedAhkFile = ahkFilesListBoxCustom.SelectedItem.ToString();
                string systemName = selectedSystemTextBlock.Text.Substring(selectedSystemTextBlock.Text.IndexOf(':') + 2); // Récupérer le nom du système à partir du TextBlock

                // Construction du chemin vers le dossier .edit\ahk\ du système
                string customAhkFolderPath = Path.Combine(Directory.GetCurrentDirectory(), systemName, "ahk", ".serial-send", ".edit");

                // Construction du chemin complet du fichier .ahk dans le dossier du système
                string filePath = Path.Combine(customAhkFolderPath, selectedAhkFile);

                if (selectedAhkFile == "No AutoHotkey found")
                {
                    // Demander à l'utilisateur s'il souhaite copier les fichiers de modèle
                    var result = MessageBox.Show("No AutoHotkey files found. Do you want to copy Start.ahk, End.ahk, and remap.ahk from the templates folder?", "Copy Templates", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            // Créer les dossiers nécessaires s'ils n'existent pas
                            if (!Directory.Exists(customAhkFolderPath))
                            {
                                Directory.CreateDirectory(customAhkFolderPath);
                            }

                            string templatesFolderPath = Path.Combine(Directory.GetCurrentDirectory(), ".templates");

                            // Copier les fichiers de modèle
                            string[] templateFiles = { "Start.ahk", "End.ahk", "remap.ahk" };
                            foreach (string file in templateFiles)
                            {
                                string sourceFile = Path.Combine(templatesFolderPath, file);
                                string destFile = Path.Combine(customAhkFolderPath, file);

                                // Vérifier si le fichier de modèle existe avant de le copier
                                if (File.Exists(sourceFile))
                                {
                                    File.Copy(sourceFile, destFile, true);
                                }
                                else
                                {
                                    MessageBox.Show($"Template file '{file}' not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }

                            // Recharger la liste des fichiers AHK personnalisés après la copie des fichiers
                            List<string> customAhkFiles = LoadCustomAhkFiles(customAhkFolderPath);
                            ahkFilesListBoxCustom.ItemsSource = customAhkFiles;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    if (File.Exists(filePath))
                    {
                        var editAhkWindow = new EditAhk();
                        editAhkWindow.LoadFile(filePath);
                        editAhkWindow.ShowDialog();
                    }
                    else
                    {
                        // Si le fichier n'existe pas, afficher un message d'erreur
                        MessageBox.Show("The selected AutoHotkey file was not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                // Réinitialiser la sélection de la ListBox
                ahkFilesListBoxCustom.SelectedItem = null;
            }
        }




        private List<string> LoadAhkFiles(string folderPath)
        {
            List<string> ahkFiles = new List<string>();

            try
            {
                // Assurez-vous que les dossiers nécessaires existent ou créez-les s'ils sont absents
                string ahkEditFolderPath = Path.Combine(folderPath, "ahk", ".edit");
                if (!Directory.Exists(ahkEditFolderPath))
                {
                    Directory.CreateDirectory(ahkEditFolderPath);
                }

                // Recherchez les fichiers .ahk dans le dossier spécifié
                string[] files = Directory.GetFiles(ahkEditFolderPath, "*.ahk");
                foreach (string file in files)
                {
                    ahkFiles.Add(Path.GetFileName(file));
                }
            }
            catch (Exception ex)
            {
                // Gérez l'exception ici (par exemple, en affichant un message d'erreur)
                Console.WriteLine($"An error occurred while loading .ahk files : {ex.Message}");
            }

            return ahkFiles;
        }



        private void CreateAhkFile(string selectedItem)
        {
            if (romsDataGrid.SelectedItem != null)
            {
                string[] selectedGame = romsDataGrid.SelectedItem as string[];
                string systemName = selectedGame[0];
                string gameName = selectedGame[2]; // Récupérer le nom du jeu sélectionné

                // Construction du chemin complet du dossier du système
                string systemFolderPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), systemName);
                string ahkEditFolderPath = System.IO.Path.Combine(systemFolderPath, "ahk", ".edit");

                // Demander à l'utilisateur de choisir le template
                var selectTypeDialog = new SelectTypeDialog();
                selectTypeDialog.Initialize(gameName); // Utiliser le nom du jeu comme valeur par défaut
                if (selectTypeDialog.ShowDialog() == true)
                {
                    string newFileName = selectTypeDialog.SelectedName;

                    // Si l'utilisateur a sélectionné "System", utiliser le nom du système
                    if (selectTypeDialog.SelectedType == "System")
                    {
                        newFileName = systemName;
                    }

                    // Construction du chemin complet de la template
                    string templateFileName = selectTypeDialog.SelectedType == "Game" ? "game.ahk" : "system.ahk";
                    string templatePath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ".templates", templateFileName);

                    // Vérifie si l'utilisateur a sélectionné un template valide
                    if (System.IO.File.Exists(templatePath))
                    {
                        try
                        {
                            // Lecture du contenu de la template
                            string templateContent = System.IO.File.ReadAllText(templatePath);

                            // Construction du chemin complet du nouveau fichier .ahk
                            string newAhkFilePath = System.IO.Path.Combine(ahkEditFolderPath, newFileName + ".ahk");

                            // Création du nouveau fichier .ahk en utilisant le contenu de la template
                            System.IO.File.WriteAllText(newAhkFilePath, templateContent);

                            // Ouvrir le fichier nouvellement créé dans EditAhk
                            var editAhkWindow = new EditAhk();
                            editAhkWindow.LoadFile(newAhkFilePath);
                            editAhkWindow.ShowDialog();
                        }
                        catch (System.Exception ex)
                        {
                            MessageBox.Show($"An error occurred while creating the AutoHotkey file : {ex.Message}");
                        }
                    }
                    else
                    {
                        MessageBox.Show("Selected template file not found.");
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a game to create an AutoHotkey file.");
            }
        }

        private List<string> LoadCustomAhkFiles(string folderPath)
        {
            List<string> ahkFiles = new List<string>();

            try
            {
                // Recherchez les fichiers .ahk dans le dossier spécifié
                string[] files = Directory.GetFiles(folderPath, "*.ahk");
                foreach (string file in files)
                {
                    ahkFiles.Add(Path.GetFileName(file));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while loading .ahk files: {ex.Message}");
            }

            return ahkFiles;
        }


        private void AhkFilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ahkFilesListBox.SelectedItem != null)
            {
                string selectedAhkFile = ahkFilesListBox.SelectedItem.ToString();
                string systemName = selectedSystemTextBlock.Text.Substring(selectedSystemTextBlock.Text.IndexOf(':') + 2); // Récupérer le nom du système à partir du TextBlock

                // Construction du chemin vers le dossier .edit\ahk\ du système
                string systemAhkFolderPath = Path.Combine(Directory.GetCurrentDirectory(), systemName, "ahk", ".edit");

                // Construction du chemin complet du fichier .ahk dans le dossier du système
                string filePath = Path.Combine(systemAhkFolderPath, selectedAhkFile);

                if (File.Exists(filePath))
                {
                    var editAhkWindow = new EditAhk();
                    editAhkWindow.LoadFile(filePath);
                    editAhkWindow.ShowDialog();
                }
                else
                {
                    // Si le fichier n'existe pas, vérifiez s'il s'agit de "No AutoHotkey system found"
                    if (selectedAhkFile == "No AutoHotkey system found")
                    {
                        // Demander à l'utilisateur s'il souhaite créer un fichier .ahk
                        CreateAhkFile(selectedAhkFile);
                    }
                    else
                    {
                        MessageBox.Show("The selected AutoHotkey file was not found.");
                    }
                }

                // Réinitialiser la sélection de la ListBox
                ahkFilesListBox.SelectedItem = null;
            }
        }

        private void AhkFilesListBoxGame_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ahkFilesListBoxGame.SelectedItem != null)
            {
                string selectedAhkFile = ahkFilesListBoxGame.SelectedItem.ToString();
                string systemName = selectedSystemTextBlock.Text.Substring(selectedSystemTextBlock.Text.IndexOf(':') + 2); // Récupérer le nom du système à partir du TextBlock

                // Construction du chemin vers le dossier .edit\ahk\ du système
                string systemAhkFolderPath = Path.Combine(Directory.GetCurrentDirectory(), systemName, "ahk", ".edit");

                // Construction du chemin complet du fichier .ahk dans le dossier du système
                string filePath = Path.Combine(systemAhkFolderPath, selectedAhkFile);

                if (File.Exists(filePath))
                {
                    var editAhkWindow = new EditAhk();
                    editAhkWindow.LoadFile(filePath);
                    editAhkWindow.ShowDialog();
                }
                else
                {
                    // Si le fichier n'existe pas, vérifiez s'il s'agit de "No AutoHotkey game found"
                    if (selectedAhkFile == "No AutoHotkey game found")
                    {
                        // Demander à l'utilisateur s'il souhaite créer un fichier .ahk
                        CreateAhkFile(selectedAhkFile);
                    }
                    else
                    {
                        MessageBox.Show("The selected AutoHotkey file was not found.");
                    }
                }

                // Réinitialiser la sélection de la ListBox
                ahkFilesListBoxGame.SelectedItem = null;
            }
        }

        // Créer un DispatcherTimer
        DispatcherTimer timer = new DispatcherTimer();
        private void CompileAhk(string systemName, string fileName)
        {
            string ahkFolderPath = Path.Combine(Directory.GetCurrentDirectory(), systemName, "ahk");
            string editFolderPath = Path.Combine(ahkFolderPath, ".edit");

            string ahkFilePath = Path.Combine(editFolderPath, fileName);
            string exeFilePath = Path.Combine(ahkFolderPath, $"{fileName.Replace(".ahk", ".exe")}");

            if (File.Exists(ahkFilePath))
            {
                try
                {
                    string ahk2ExeFolderPath = Path.Combine(Directory.GetCurrentDirectory(), ".Ahk2Exe");

                    string command = $"\"{Path.Combine(ahk2ExeFolderPath, "Ahk2Exe.exe")}\" /in \"{ahkFilePath}\" /out \"{exeFilePath}\" /compress 1 /base \"{Path.Combine(ahk2ExeFolderPath, "Unicode 64-bit.bin")}\" ";

                    // Créer un fichier batch avec la commande de compilation
                    string batchFilePath = Path.Combine(ahk2ExeFolderPath, "compile.bat");
                    File.WriteAllText(batchFilePath, command);

                    ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", $"/c {batchFilePath}")
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    Process process = Process.Start(psi);
                    process.WaitForExit();

                    // Imprimer les erreurs éventuelles
                    string stderr = process.StandardError.ReadToEnd();
                    if (!string.IsNullOrEmpty(stderr))
                    {
                        Debug.WriteLine($"Compilation Errors: {stderr}");
                    }

                    //MessageBox.Show($"successful saved : {fileName}");
                    // Ajouter le nouveau message à la fin du texte existant
                    confirmationTextBlock.Text += $"successful saved :\n{Path.GetFileName(exeFilePath)}\n";

                    // Démarrer le timer après avoir affiché le message de confirmation
                    timer.Interval = TimeSpan.FromSeconds(5);
                    timer.Tick += Timer_Tick;
                    timer.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error compiling AutoHotkey file: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("AutoHotkey file not found.");
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // Effacer le message de confirmation et arrêter le timer
            confirmationTextBlock.Text = "";
            timer.Stop();
        }

        // Gestionnaire d'événements pour le clic sur le bouton de compilation
        private void CompileAhkButton_Click(object sender, RoutedEventArgs e)
        {
            if (romsDataGrid.SelectedItem != null)
            {
                string[] selectedGame = romsDataGrid.SelectedItem as string[];
                string systemName = selectedGame[0];
                string gameName = selectedGame[2]; // Récupérer le nom du jeu sélectionné

                // Compilez le fichier .ahk correspondant au jeu sélectionné
                CompileAhk(systemName, $"{gameName}.ahk");
                CompileAhk(systemName, $"{systemName}.ahk");
            }
            else
            {
                MessageBox.Show("Please select a game to compile an AutoHotkey file.");
            }
        }

        private void DeleteCompiledAhk(string systemName)
        {
            // Utiliser le même chemin relatif pour le dossier contenant vos fichiers .exe
            string ahkFolderPath = Path.Combine(Directory.GetCurrentDirectory(), systemName, "ahk");

            // Obtenir tous les fichiers .exe dans le dossier
            string[] filePaths = Directory.GetFiles(ahkFolderPath, "*.exe");

            foreach (string filePath in filePaths)
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting file: {ex.Message}");
                }
            }

            // Ajouter le nouveau message à la fin du texte existant
            confirmationTextBlock.Text += "Files deleted successfully.\n";

            // Démarrer le timer après avoir affiché le message de confirmation
            timer.Interval = TimeSpan.FromSeconds(3);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void DeleteCompiledAhkButton_Click(object sender, RoutedEventArgs e)
        {
            if (romsDataGrid.SelectedItem != null)
            {
                string[] selectedGame = romsDataGrid.SelectedItem as string[];
                string systemName = selectedGame[0]; // Récupérer le nom du système sélectionné

                // Supprimez tous les fichiers .exe compilés pour le système sélectionné
                DeleteCompiledAhk(systemName);
            }
            else
            {
                MessageBox.Show("Please select a system to delete compiled AutoHotkey files.");
            }
        }

        private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
        {
             string systemName = selectedSystemTextBlock.Text.Substring(selectedSystemTextBlock.Text.IndexOf(':') + 2); // Récupérer le nom du système à partir du TextBlock
            // Récupérer le chemin complet du fichier system-config.prc
            string systemConfigPath = Path.Combine(Directory.GetCurrentDirectory(), systemName, "ahk", "system-config.prc");

            try
            {
                // Écrire le contenu modifié dans le fichier system-config.prc
                File.WriteAllText(systemConfigPath, configTextBox.Text);
                MessageBox.Show("The changes were saved successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while saving changes: {ex.Message}");
            }
        }

        private void CompileCustomAhkFiles(string systemName)
        {
            // Construction du chemin complet du dossier des fichiers AHK personnalisés
            string customAhkFolderPath = Path.Combine(Directory.GetCurrentDirectory(), systemName, "ahk", ".serial-send", ".edit");

            // Assurez-vous que le dossier existe
            if (!Directory.Exists(customAhkFolderPath))
            {
                MessageBox.Show("No custom AHK files found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Obtenir la liste des fichiers AHK personnalisés
            List<string> customAhkFiles = LoadCustomAhkFiles(customAhkFolderPath);

            // Compiler chaque fichier AHK
            foreach (string file in customAhkFiles)
            {
                string ahkFilePath = Path.Combine(customAhkFolderPath, file);
                string exeFilePath = Path.Combine(Directory.GetCurrentDirectory(), systemName, "ahk", ".serial-send", $"{file.Replace(".ahk", ".exe")}");

                // Compiler le fichier AHK
                CompileAhkFile(ahkFilePath, exeFilePath);
            }
        }

        private void CompileAhkFile(string ahkFilePath, string exeFilePath)
        {
            if (File.Exists(ahkFilePath))
            {
                try
                {
                    string ahk2ExeFolderPath = Path.Combine(Directory.GetCurrentDirectory(), ".Ahk2Exe");

                    string command = $"\"{Path.Combine(ahk2ExeFolderPath, "Ahk2Exe.exe")}\" /in \"{ahkFilePath}\" /out \"{exeFilePath}\" /compress 1 /base \"{Path.Combine(ahk2ExeFolderPath, "Unicode 64-bit.bin")}\" ";

                    // Créer un fichier batch avec la commande de compilation
                    string batchFilePath = Path.Combine(ahk2ExeFolderPath, "compile.bat");
                    File.WriteAllText(batchFilePath, command);

                    ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", $"/c {batchFilePath}")
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    Process process = Process.Start(psi);
                    process.WaitForExit();

                    // Imprimer les erreurs éventuelles
                    string stderr = process.StandardError.ReadToEnd();
                    if (!string.IsNullOrEmpty(stderr))
                    {
                        Debug.WriteLine($"Compilation Errors: {stderr}");
                    }

                    // Ajouter le nouveau message à la fin du texte existant
                    confirmationTextBlock.Text += $"successful saved :{Path.GetFileName(exeFilePath)}\n";

                    // Démarrer le timer après avoir affiché le message de confirmation
                    timer.Interval = TimeSpan.FromSeconds(5);
                    timer.Tick += Timer_Tick;
                    timer.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error compiling AutoHotkey file: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("AutoHotkey file not found.");
            }
        }

        // Gestionnaire d'événements pour le clic sur le bouton de compilation personnalisé
        private void CompileCustomAhkButton_Click(object sender, RoutedEventArgs e)
        {
            if (romsDataGrid.SelectedItem != null)
            {
                string[] selectedGame = romsDataGrid.SelectedItem as string[];
                string systemName = selectedGame[0];

                // Compiler les fichiers AHK personnalisés
                CompileCustomAhkFiles(systemName);
            }
            else
            {
                MessageBox.Show("Please select a system to compile the custom AutoHotkey files.");
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