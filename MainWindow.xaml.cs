using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using System.Runtime.Versioning;
using Edit_LsTRoms;

namespace Edit_LsTRoms
{
    [SupportedOSPlatform("windows")]
    public partial class MainWindow : Window
    {
        private const string APP_VERSION = "1.2.0";
        private readonly Dictionary<string, List<string[]>> systems = new Dictionary<string, List<string[]>>();
        private readonly Dictionary<string, int> systemCounters = new Dictionary<string, int>();
        private readonly Dictionary<string, Dictionary<string, int>> categoryCounters = new Dictionary<string, Dictionary<string, int>>();
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly DispatcherTimer timer = new DispatcherTimer();
        private string? _currentVersion;
        private string? _latestVersion;

        public MainWindow()
        {
            InitializeComponent();
            this.Title = $"Edit_LsTRoms v{APP_VERSION}";
            UpdateToggleButtonState();
            CheckStartupShortcut();

            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Edit_LsTRoms");

            // Vérifier la version au démarrage
            _ = CheckDemulShooterVersion();

            // Trouver le fichier roms.ini
            string? romsIniPath = FindRomsIniFile(Directory.GetCurrentDirectory());

            if (romsIniPath != null && File.Exists(romsIniPath))
            {
                string[] lines = File.ReadAllLines(romsIniPath);

                string? currentSystem = null;
                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        // Extraire le nom du système et vérifier qu'il n'est pas vide
                        string? systemName = line.Trim('[', ']');
                        if (!string.IsNullOrEmpty(systemName))
                        {
                            currentSystem = systemName;
                        systems[currentSystem] = new List<string[]>();
                        systemCounters[currentSystem] = 0;
                        categoryCounters[currentSystem] = new Dictionary<string, int>();
                    }
                        continue;
                    }

                    if (currentSystem == null || !line.Contains("=")) continue;

                    string[] parts = line.Split('=', 2);
                    if (parts.Length != 2) continue;

                        string key = parts[0].Trim();
                    if (string.IsNullOrEmpty(key)) continue;

                    string[] keyParts = key.Split('_');
                    if (keyParts.Length < 2 || !int.TryParse(keyParts[1], out int number)) continue;

                    string category = keyParts[0];
                        var gameDetails = parts[1].Split('|').Select(detail => detail.Trim()).ToArray();
                    if (gameDetails.Length == 0) continue;

                    systems[currentSystem].Add(new[] { key }.Concat(gameDetails).ToArray());

                        if (!categoryCounters[currentSystem].ContainsKey(category) || number > categoryCounters[currentSystem][category])
                        {
                            categoryCounters[currentSystem][category] = number;
                    }
                }

                // mettre à jour la liste des fichiers .ahk
                romsDataGrid.SelectionChanged += RomsDataGrid_SelectionChanged;

                // Mettre à jour le DataGrid
                UpdateDataGrid();
            }

            _ = CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                // Read current version from ChangeLog.txt
                string changeLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "ChangeLog.txt");
                if (!File.Exists(changeLogPath))
                {
                    UpdateStatus.Text = "Cannot find ChangeLog.txt";
                    UpdateIndicator.Fill = Brushes.Red;
                    return;
                }

                string[] changeLogLines = await File.ReadAllLinesAsync(changeLogPath);
                if (changeLogLines.Length == 0)
                {
                    UpdateStatus.Text = "ChangeLog.txt is empty";
                    UpdateIndicator.Fill = Brushes.Red;
                    return;
                }

                string? currentVersionLine = changeLogLines[0];
                if (string.IsNullOrEmpty(currentVersionLine))
                {
                    UpdateStatus.Text = "Invalid version line";
                    UpdateIndicator.Fill = Brushes.Red;
                    return;
                }

                Version currentVersion = ExtractVersion(currentVersionLine);

                // Check GitHub latest version
                try
                {
                    var response = await _httpClient.GetStringAsync("https://api.github.com/repos/argonlefou/DemulShooter/releases/latest");
                    if (string.IsNullOrEmpty(response))
                    {
                        UpdateStatus.Text = "Invalid response from GitHub";
                        UpdateIndicator.Fill = Brushes.Orange;
                        return;
                    }

                    if (!response.Contains("DemulShooter_v"))
                    {
                        UpdateStatus.Text = "Invalid version format";
                        UpdateIndicator.Fill = Brushes.Orange;
                        return;
                    }

                    string[] versionParts = response.Split("DemulShooter_v");
                    if (versionParts.Length < 2)
                    {
                        UpdateStatus.Text = "Invalid version format";
                        UpdateIndicator.Fill = Brushes.Orange;
                        return;
                    }

                    string? latestVersionStr = versionParts[1].Split(".zip")[0];
                    if (string.IsNullOrEmpty(latestVersionStr))
                    {
                        UpdateStatus.Text = "Invalid version format";
                        UpdateIndicator.Fill = Brushes.Orange;
                        return;
                    }

                    if (Version.TryParse(latestVersionStr, out Version latestVersion))
                    {
                        if (latestVersion > currentVersion)
                        {
                            UpdateStatus.Text = "Update available!";
                            UpdateIndicator.Fill = Brushes.Green;
                        }
                        else
                        {
                            UpdateStatus.Text = "Up to date";
                            UpdateIndicator.Fill = Brushes.Red;
                        }
                    }
                    else
                    {
                        UpdateStatus.Text = "Invalid version format";
                        UpdateIndicator.Fill = Brushes.Orange;
                    }
                }
                catch (HttpRequestException ex)
                {
                    Debug.WriteLine($"HTTP Request Error: {ex.Message}");
                    UpdateStatus.Text = "Cannot connect to GitHub";
                    UpdateIndicator.Fill = Brushes.Orange;
                }
                catch (TaskCanceledException)
                {
                    UpdateStatus.Text = "Request timeout";
                    UpdateIndicator.Fill = Brushes.Orange;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update Check Error: {ex.Message}");
                UpdateStatus.Text = $"Error checking updates";
                UpdateIndicator.Fill = Brushes.Red;
            }
        }

        private Version ExtractVersion(string versionLine)
        {
            try
            {
                // Vérifier si la ligne est valide
                if (string.IsNullOrEmpty(versionLine))
                {
                    return new Version(0, 0, 0);
                }

                // Vérifier si la ligne contient les délimiteurs nécessaires
                var parts = versionLine.Split('[', ']');
                if (parts.Length < 2)
                {
                    return new Version(0, 0, 0);
                }

                // Extraire la version et vérifier qu'elle est valide
                string versionStr = parts[1].Trim();
                if (string.IsNullOrEmpty(versionStr))
                {
                    return new Version(0, 0, 0);
                }

                // Tenter de parser la version
                if (Version.TryParse(versionStr, out Version version))
                {
                    return version;
                }

                return new Version(0, 0, 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting version: {ex.Message}");
                return new Version(0, 0, 0);
            }
        }

        private string? FindRomsIniFile(string directory)
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
                    allGameDetails.Add(new[] { systemName }.Concat(gameDetails).ToArray());
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

        private void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            string? romsIniPath = FindRomsIniFile(Directory.GetCurrentDirectory());

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
                if (cell.Column is DataGridBoundColumn column && cell.Item is string[] data)
                {
                    int columnIndex = Array.IndexOf(romsDataGrid.Columns.Cast<DataGridColumn>().ToArray(), cell.Column);
                    if (columnIndex >= 0)
                    {
                        data[columnIndex] = editBox.Text;
                    }
                }
            }
            UpdateUnderlyingData();
            UpdateKeys();
            UpdateDataGrid();
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
            if (romsDataGrid.ItemsSource == null) return;

            foreach (var item in romsDataGrid.ItemsSource)
            {
                if (item is string[] gameDetails && gameDetails.Length >= 2)
                {
                string systemName = gameDetails[0];
                string key = gameDetails[1];

                    if (!string.IsNullOrEmpty(systemName) && systems.ContainsKey(systemName))
                    {
                        string[] details = gameDetails.Skip(2).ToArray();
                int index = systems[systemName].FindIndex(g => g[0] == key);
                if (index != -1)
                {
                            systems[systemName][index] = new[] { key }.Concat(details).ToArray();
                        }
                    }
                }
            }
        }

        private void UpdateConfigFileWithNewSystem(string newSystem)
        {
            if (string.IsNullOrEmpty(newSystem))
            {
                MessageBox.Show("Invalid system name.");
                return;
            }

            string configFile = ".config";
            if (!File.Exists(configFile))
            {
                MessageBox.Show("Configuration file not found.");
                return;
            }

            List<string> lines = File.ReadAllLines(configFile).ToList();
            int systemsIndex = lines.FindIndex(line => line.Trim() == "[systems]");
            
            if (systemsIndex == -1)
            {
                MessageBox.Show("Systems section not found in configuration file.");
                return;
            }

                int existingSystemIndex = lines.FindIndex(systemsIndex + 1, line => line.Trim() == newSystem);
                if (existingSystemIndex == -1)
                {
                    lines.Insert(systemsIndex + 1, newSystem);
                    File.WriteAllLines(configFile, lines);
            }
        }

        private void AddGameButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CategoryDialog(systems.Keys.ToList(), systems.SelectMany(s => s.Value.Select(v => v[0].Split('_')[0])).Distinct().ToList());
            if (dialog.ShowDialog() != true) return;

                string system = dialog.SelectedSystem;
                if (dialog.IsNewSystem)
                {
                    system = dialog.NewSystem;
                if (string.IsNullOrEmpty(system))
                {
                    MessageBox.Show("Invalid system name.");
                    return;
                }

                    systems[system] = new List<string[]>();
                    systemCounters[system] = 0;
                    categoryCounters[system] = new Dictionary<string, int>();

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
            systems[system].Add(new[] { key, "", "", "", "", "", "", "", "" });
                UpdateDataGrid();
            }

        private void RemoveEmptySystems()
        {
            // Récupérer les systèmes sans catégorie
            List<string> emptySystems = systems.Where(system => system.Value.Count == 0)
                                             .Select(system => system.Key)
                                             .ToList();

            string? romsIniPath = FindRomsIniFile(Directory.GetCurrentDirectory());
            if (romsIniPath == null)
            {
                MessageBox.Show("roms.ini file not found.");
                return;
            }

            try
            {
                List<string> romsIniLines = File.ReadAllLines(romsIniPath).ToList();

                foreach (var emptySystem in emptySystems)
                {
                    romsIniLines.RemoveAll(line => line.Trim() == $"[{emptySystem}]");
                }

                File.WriteAllLines(romsIniPath, romsIniLines);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing empty systems: {ex.Message}");
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
            List<string> systemsWithoutGames = systems.Where(system => system.Value.Count == 0)
                                                    .Select(system => system.Key)
                                                    .ToList();

            string? romsIniPath = FindRomsIniFile(Directory.GetCurrentDirectory());
            if (romsIniPath == null)
            {
                MessageBox.Show("roms.ini file not found.");
                return;
            }

            try
            {
                List<string> romsIniLines = File.ReadAllLines(romsIniPath).ToList();

                foreach (var system in systemsWithoutGames)
                {
                    romsIniLines.RemoveAll(line => line.Trim() == $"[{system}]");
                }

                File.WriteAllLines(romsIniPath, romsIniLines);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing systems: {ex.Message}");
            }
        }

        private void RemoveEmptySystemsFromRomsIni()
        {
            // Récupérer les systèmes sans clé de jeu ni de catégorie
            List<string> emptySystems = systems.Where(system => system.Value.Count == 0)
                                             .Select(system => system.Key)
                                             .ToList();

            string? romsIniPath = FindRomsIniFile(Directory.GetCurrentDirectory());
            if (romsIniPath == null)
            {
                MessageBox.Show("roms.ini file not found.");
                return;
            }

            try
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
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing empty systems: {ex.Message}");
        }
        }

        private void SaveData()
        {
            string? romsIniPath = FindRomsIniFile(Directory.GetCurrentDirectory());
            if (romsIniPath == null)
            {
                MessageBox.Show("roms.ini file not found.");
                return;
            }

            try
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
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving data: {ex.Message}");
            }
        }

        private void UpdateSystemsFromRomsIni(string[] lines)
        {
            systems.Clear();
            systemCounters.Clear();
            categoryCounters.Clear();

            string? currentSystem = null;
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    // Extraire le nom du système et vérifier qu'il n'est pas vide
                    var systemName = line.Trim('[', ']');
                    if (!string.IsNullOrEmpty(systemName))
                    {
                        systems[systemName] = new List<string[]>();
                        systemCounters[systemName] = 0;
                        categoryCounters[systemName] = new Dictionary<string, int>();
                        currentSystem = systemName;
                    }
                    continue;
                }

                if (string.IsNullOrEmpty(currentSystem) || !line.Contains("=")) continue;

                var parts = line.Split('=', 2);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                if (string.IsNullOrEmpty(key)) continue;

                var keyParts = key.Split('_');
                if (keyParts.Length < 2 || !int.TryParse(keyParts[1], out int number)) continue;

                var category = keyParts[0];
                    var gameDetails = parts[1].Split('|').Select(detail => detail.Trim()).ToArray();
                if (gameDetails.Length == 0) continue;

                systems[currentSystem].Add(new[] { key }.Concat(gameDetails).ToArray());

                    if (!categoryCounters[currentSystem].ContainsKey(category) || number > categoryCounters[currentSystem][category])
                    {
                        categoryCounters[currentSystem][category] = number;
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
            if (!File.Exists(configFile))
            {
                MessageBox.Show("Configuration file not found.");
                return;
            }

            string[] lines = File.ReadAllLines(configFile);
            bool found = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("DSUpdtlock="))
                {
                    lines[i] = newLine;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                MessageBox.Show("DSUpdtlock setting not found in configuration file.");
                return;
            }

            File.WriteAllLines(configFile, lines);
        }

        private void RegisterOCXFilesButton_Click(object sender, RoutedEventArgs e)
        {
            // Chemin du dossier contenant les fichiers OCX
            string ocxFolderPath = Path.Combine(Directory.GetCurrentDirectory(), ".mamehooker");
            if (!Directory.Exists(ocxFolderPath))
            {
                MessageBox.Show("The .mamehooker directory does not exist.");
                return;
            }

            // Chemin complet des fichiers OCX
            string ledwizOcxPath = Path.Combine(ocxFolderPath, "LEDWIZM.OCX");
            string richtxOcxPath = Path.Combine(ocxFolderPath, "richtx32.ocx");

            // Vérifier si les fichiers OCX existent
            if (!File.Exists(ledwizOcxPath) || !File.Exists(richtxOcxPath))
            {
                MessageBox.Show("One or both OCX files not found in the specified folder.");
                return;
            }

            // Créer la commande regsvr32.exe
            string regsvr32Command = $"/c regsvr32.exe \"{ledwizOcxPath}\" && regsvr32.exe \"{richtxOcxPath}\"";

            // Lancer un processus CMD avec les privilèges d'administrateur
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = regsvr32Command,
                Verb = "runas", // Exécuter en tant qu'administrateur
                UseShellExecute = true
            };

            try
            {
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error registering OCX files: {ex.Message}");
            }
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            AddToStartup();
            toggleStartupButton.Content = "On";
        }

        private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            RemoveFromStartup();
            toggleStartupButton.Content = "Off";
        }

        private void CheckStartupShortcut()
        {
            // Chemin complet vers le raccourci dans le dossier de démarrage
            string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Mamehook.lnk");

            // Mettre à jour l'état du bouton en fonction de la présence ou de l'absence du raccourci
            if (File.Exists(shortcutPath))
            {
                toggleStartupButton.IsChecked = true;
                toggleStartupButton.Content = "On";
            }
            else
            {
                toggleStartupButton.IsChecked = false;
                toggleStartupButton.Content = "Off";
            }
        }

        private void AddToStartup()
        {
            string exePath = Path.Combine(Directory.GetCurrentDirectory(), ".mamehooker", "mamehook.exe");
            if (!File.Exists(exePath))
            {
                MessageBox.Show("mamehook.exe not found in .mamehooker directory.");
                return;
            }

            string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcutPath = Path.Combine(startupFolderPath, "Mamehook.lnk");

            try
            {
                ShortcutManager.CreateShortcut(exePath, shortcutPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating shortcut: {ex.Message}");
            }
        }

        private void RemoveFromStartup()
        {
            // Chemin complet vers le raccourci dans le dossier de démarrage
            string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Mamehook.lnk");

            // Supprimer le fichier du raccourci s'il existe
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }
        }

        private void RomsDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            ahkFilesListBox.ItemsSource = null;
            ahkFilesListBoxGame.ItemsSource = null;
            ahkFilesListBoxCustom.ItemsSource = null;

            if (romsDataGrid.SelectedItem is string[] selectedGame && selectedGame.Length > 2)
            {
                string systemName = selectedGame[0];
                string gameName = selectedGame[2];

                if (string.IsNullOrEmpty(systemName))
                {
                    MessageBox.Show("Invalid system name.");
                    return;
                }

                string systemFolderPath = Path.Combine(Directory.GetCurrentDirectory(), systemName);
                List<string> systemAhkFiles = LoadAhkFiles(systemFolderPath);

                if (!string.IsNullOrEmpty(gameName))
                {
                    string gameAhkFile = $"{gameName}_ahk.ahk";
                    ahkFilesListBoxGame.ItemsSource = systemAhkFiles.Contains(gameAhkFile) 
                        ? new List<string> { gameAhkFile } 
                        : new List<string> { "No AutoHotkey game found" };

                    string systemAhkFile = $"{systemName}_ahk.ahk";
                    ahkFilesListBox.ItemsSource = systemAhkFiles.Contains(systemAhkFile)
                        ? new List<string> { systemAhkFile }
                        : new List<string> { "No AutoHotkey system found" };
                }

                string customAhkFolderPath = Path.Combine(systemFolderPath, "ahk", ".serial-send", ".edit");
                List<string> customAhkFiles = LoadCustomAhkFiles(customAhkFolderPath);
                
                if (customAhkFiles.Count == 0)
                {
                    ahkFilesListBoxCustom.ItemsSource = new List<string> { "No AutoHotkey found" };
                    }
                    else
                    {
                    ahkFilesListBoxCustom.ItemsSource = customAhkFiles;
                }

                selectedSystemTextBlock.Text = $"System : {systemName}";

                // Configuration AHK
                string systemConfigPath = Path.Combine(Directory.GetCurrentDirectory(), systemName, "ahk", "system-config.prc");
                configTextBox.Text = File.Exists(systemConfigPath)
                    ? File.ReadAllText(systemConfigPath)
                    : "The file system-config.prc is not found.";

                // Configuration Nomousy
                string systemConfigNomousyPath = Path.Combine(Directory.GetCurrentDirectory(), ".nomousy", "system-config.prc");
                configTextNomousyBox.Text = File.Exists(systemConfigNomousyPath)
                    ? File.ReadAllText(systemConfigNomousyPath)
                    : "The file system-config.prc is not found.";
            }
        }

        private void AhkFilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ahkFilesListBox.SelectedItem is not string selectedAhkFile) return;
            
            string? systemName = selectedSystemTextBlock.Text;
            if (string.IsNullOrEmpty(systemName))
            {
                MessageBox.Show("No system selected.");
                return;
            }

            int colonIndex = systemName.IndexOf(':');
            if (colonIndex < 0 || colonIndex + 2 >= systemName.Length)
            {
                MessageBox.Show("Invalid system name format.");
                return;
            }
            
            systemName = systemName.Substring(colonIndex + 2).Trim();
            if (string.IsNullOrEmpty(systemName))
            {
                MessageBox.Show("System name is empty.");
                return;
            }

            string systemAhkFolderPath = Path.Combine(Directory.GetCurrentDirectory(), systemName, "ahk", ".edit");
            string filePath = Path.Combine(systemAhkFolderPath, selectedAhkFile);

            if (selectedAhkFile == "No AutoHotkey system found")
            {
                CreateAhkFile(selectedAhkFile);
            }
            else if (!File.Exists(filePath))
            {
                MessageBox.Show("The selected AutoHotkey file was not found.");
            }
            else
            {
                var editAhkWindow = new EditAhk();
                editAhkWindow.LoadFile(filePath);
                editAhkWindow.ShowDialog();
            }

            ahkFilesListBox.SelectedItem = null;
        }

        private void AhkFilesListBoxGame_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ahkFilesListBoxGame.SelectedItem is not string selectedAhkFile) return;
            
            string? systemName = selectedSystemTextBlock.Text;
            if (string.IsNullOrEmpty(systemName))
            {
                MessageBox.Show("No system selected.");
                return;
            }

            int colonIndex = systemName.IndexOf(':');
            if (colonIndex < 0 || colonIndex + 2 >= systemName.Length)
            {
                MessageBox.Show("Invalid system name format.");
                return;
            }
            
            systemName = systemName.Substring(colonIndex + 2).Trim();
            if (string.IsNullOrEmpty(systemName))
            {
                MessageBox.Show("System name is empty.");
                return;
            }

            string systemAhkFolderPath = Path.Combine(Directory.GetCurrentDirectory(), systemName, "ahk", ".edit");
            string filePath = Path.Combine(systemAhkFolderPath, selectedAhkFile);

            if (selectedAhkFile == "No AutoHotkey game found")
            {
                CreateAhkFile(selectedAhkFile);
            }
            else if (!File.Exists(filePath))
            {
                MessageBox.Show("The selected AutoHotkey file was not found.");
                }
                else
                {
                var editAhkWindow = new EditAhk();
                editAhkWindow.LoadFile(filePath);
                editAhkWindow.ShowDialog();
            }

            ahkFilesListBoxGame.SelectedItem = null;
        }

        private void AhkFilesListBoxCustom_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ahkFilesListBoxCustom.SelectedItem is not string selectedAhkFile) return;
            
            string? systemName = selectedSystemTextBlock.Text;
            if (string.IsNullOrEmpty(systemName))
            {
                MessageBox.Show("No system selected.");
                return;
            }

            int colonIndex = systemName.IndexOf(':');
            if (colonIndex < 0 || colonIndex + 2 >= systemName.Length)
            {
                MessageBox.Show("Invalid system name format.");
                return;
            }
            
            systemName = systemName.Substring(colonIndex + 2).Trim();
            if (string.IsNullOrEmpty(systemName))
            {
                MessageBox.Show("System name is empty.");
                return;
            }

                string customAhkFolderPath = Path.Combine(Directory.GetCurrentDirectory(), systemName, "ahk", ".serial-send", ".edit");

                if (selectedAhkFile == "No AutoHotkey found")
                {
                var result = MessageBox.Show(
                    "No AutoHotkey files found. Do you want to copy Start.ahk, End.ahk, and remap.ahk from the templates folder?",
                    "Copy Templates",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            if (!Directory.Exists(customAhkFolderPath))
                            {
                                Directory.CreateDirectory(customAhkFolderPath);
                            }

                            string templatesFolderPath = Path.Combine(Directory.GetCurrentDirectory(), ".templates");
                            string[] templateFiles = { "Start.ahk", "End.ahk", "remap.ahk" };
                        
                            foreach (string file in templateFiles)
                            {
                                string sourceFile = Path.Combine(templatesFolderPath, file);
                                string destFile = Path.Combine(customAhkFolderPath, file);

                            if (!File.Exists(sourceFile))
                                {
                                    MessageBox.Show($"Template file '{file}' not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                continue;
                                }

                            File.Copy(sourceFile, destFile, true);
                            }

                        var customAhkFiles = LoadCustomAhkFiles(customAhkFolderPath);
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
                string filePath = Path.Combine(customAhkFolderPath, selectedAhkFile);
                if (!File.Exists(filePath))
                    {
                    MessageBox.Show("The selected AutoHotkey file was not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                        var editAhkWindow = new EditAhk();
                        editAhkWindow.LoadFile(filePath);
                        editAhkWindow.ShowDialog();
            }

                ahkFilesListBoxCustom.SelectedItem = null;
        }

        private List<string> LoadAhkFiles(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                MessageBox.Show("Invalid folder path.");
                return new List<string>();
            }

                string ahkEditFolderPath = Path.Combine(folderPath, "ahk", ".edit");
                if (!Directory.Exists(ahkEditFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(ahkEditFolderPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error creating directory: {ex.Message}");
                    return new List<string>();
                }
            }

            try
            {
                string[] files = Directory.GetFiles(ahkEditFolderPath, "*_ahk.ahk");
                var ahkFiles = files.Select(Path.GetFileName)
                                  .Where(f => !string.IsNullOrEmpty(f))
                                  .Select(f => f!)
                                  .ToList();

                if (ahkFiles.Count == 0)
                {
                    ahkFilesListBox.ItemsSource = new[] { "No AutoHotkey files found" };
                }
                else
                {
                    ahkFilesListBox.ItemsSource = ahkFiles;
            }

            return ahkFiles;
        }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading AHK files: {ex.Message}");
                ahkFilesListBox.ItemsSource = new[] { "Error loading files" };
                return new List<string>();
            }
        }

        private List<string> LoadCustomAhkFiles(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                MessageBox.Show("Invalid folder path.");
                return new List<string>();
            }

            if (!Directory.Exists(folderPath))
                    {
                        try
                        {
                    Directory.CreateDirectory(folderPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error creating directory: {ex.Message}");
                    return new List<string>();
                }
            }

            try
            {
                string[] files = Directory.GetFiles(folderPath, "*.ahk");
                var ahkFiles = files.Select(Path.GetFileName)
                                  .Where(f => !string.IsNullOrEmpty(f))
                                  .Select(f => f!)
                                  .ToList();

                if (ahkFiles.Count == 0)
                {
                    ahkFilesListBoxCustom.ItemsSource = new[] { "No AutoHotkey found" };
                }
                else
                {
                    ahkFilesListBoxCustom.ItemsSource = ahkFiles;
            }

            return ahkFiles;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading AHK files: {ex.Message}");
                ahkFilesListBoxCustom.ItemsSource = new[] { "Error loading files" };
                return new List<string>();
            }
        }

        private void CreateAhkFile(string selectedItem)
        {
            if (romsDataGrid.SelectedItem is not string[] selectedGame || selectedGame.Length < 3)
            {
                MessageBox.Show("Please select a game to create an AutoHotkey file.");
                return;
            }

            string systemName = selectedGame[0];
            string gameName = selectedGame[2];

            if (string.IsNullOrEmpty(systemName) || string.IsNullOrEmpty(gameName))
            {
                MessageBox.Show("Invalid system name or game name.");
                return;
            }

            string systemFolderPath = Path.Combine(Directory.GetCurrentDirectory(), systemName);
            string ahkEditFolderPath = Path.Combine(systemFolderPath, "ahk", ".edit");

            var selectTypeDialog = new SelectTypeDialog();
            selectTypeDialog.Initialize(gameName);
            
            if (selectTypeDialog.ShowDialog() != true) return;

            string newFileName = selectTypeDialog.SelectedName;
            if (string.IsNullOrEmpty(newFileName))
            {
                MessageBox.Show("Invalid file name selected.");
                return;
            }

            if (selectTypeDialog.SelectedType == "System")
            {
                newFileName = systemName;
            }

            string templateFileName = selectTypeDialog.SelectedType == "Game" ? "game.ahk" : "system.ahk";
            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), ".templates", templateFileName);

            if (!File.Exists(templatePath))
            {
                MessageBox.Show("Selected template file not found.");
                return;
            }

            try
            {
                string templateContent = File.ReadAllText(templatePath);
                string newAhkFilePath = Path.Combine(ahkEditFolderPath, $"{newFileName}_ahk.ahk");

                Directory.CreateDirectory(ahkEditFolderPath);
                File.WriteAllText(newAhkFilePath, templateContent);

                    var editAhkWindow = new EditAhk();
                editAhkWindow.LoadFile(newAhkFilePath);
                    editAhkWindow.ShowDialog();
                }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while creating the AutoHotkey file: {ex.Message}");
            }
        }

        private void CompileAhk(string systemName, string fileName)
        {
            if (string.IsNullOrEmpty(systemName) || string.IsNullOrEmpty(fileName))
            {
                MessageBox.Show("Invalid system name or file name.");
                return;
            }

            string ahkFolderPath = Path.Combine(Directory.GetCurrentDirectory(), systemName, "ahk");
            string editFolderPath = Path.Combine(ahkFolderPath, ".edit");

            if (!Directory.Exists(ahkFolderPath) || !Directory.Exists(editFolderPath))
            {
                MessageBox.Show($"Required directories not found: {ahkFolderPath} or {editFolderPath}");
                return;
            }

            string ahkFilePath = Path.Combine(editFolderPath, fileName);
            if (!File.Exists(ahkFilePath))
            {
                MessageBox.Show("AutoHotkey file not found.");
                return;
            }

            string exeFileName = fileName.Replace("_ahk.ahk", "_ahk.exe");
            if (string.IsNullOrEmpty(exeFileName))
            {
                MessageBox.Show("Invalid output file name.");
                return;
            }

            string exeFilePath = Path.Combine(ahkFolderPath, exeFileName);

                try
                {
                    string ahk2ExeFolderPath = Path.Combine(Directory.GetCurrentDirectory(), ".Ahk2Exe");
                if (!Directory.Exists(ahk2ExeFolderPath))
                {
                    MessageBox.Show(".Ahk2Exe directory not found.");
                    return;
                }

                string ahk2ExePath = Path.Combine(ahk2ExeFolderPath, "Ahk2Exe.exe");
                string unicodeBinPath = Path.Combine(ahk2ExeFolderPath, "Unicode 64-bit.bin");

                if (!File.Exists(ahk2ExePath) || !File.Exists(unicodeBinPath))
                {
                    MessageBox.Show("Required Ahk2Exe files not found.");
                    return;
                }

                string command = $"\"{ahk2ExePath}\" /in \"{ahkFilePath}\" /out \"{exeFilePath}\" /compress 1 /base \"{unicodeBinPath}\"";

                    string batchFilePath = Path.Combine(ahk2ExeFolderPath, "compile.bat");
                    File.WriteAllText(batchFilePath, command);

                    ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", $"/c {batchFilePath}")
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    MessageBox.Show("Failed to start compilation process.");
                    return;
                }

                    process.WaitForExit();

                string? stderr = process.StandardError.ReadToEnd();
                    if (!string.IsNullOrEmpty(stderr))
                    {
                        Debug.WriteLine($"Compilation Errors: {stderr}");
                    }

                confirmationTextBlock.Text += $"successful saved: {exeFileName}\n";

                    timer.Interval = TimeSpan.FromSeconds(5);
                    timer.Tick += Timer_Tick;
                    timer.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error compiling AutoHotkey file: {ex.Message}");
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Effacer le message de confirmation et arrêter le timer
            confirmationTextBlock.Text = "";
            timer.Stop();
        }

        // Gestionnaire d'événements pour le clic sur le bouton de compilation
        private void CompileAhkButton_Click(object sender, RoutedEventArgs e)
        {
            if (romsDataGrid.SelectedItem is not string[] selectedGame || selectedGame.Length < 3)
            {
                MessageBox.Show("Please select a game to compile an AutoHotkey file.");
                return;
            }

                string systemName = selectedGame[0];
            string gameName = selectedGame[2];

            if (string.IsNullOrEmpty(systemName) || string.IsNullOrEmpty(gameName))
            {
                MessageBox.Show("Invalid system name or game name.");
                return;
            }

                CompileAhk(systemName, $"{gameName}_ahk.ahk");
                CompileAhk(systemName, $"{systemName}_ahk.ahk");
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
            if (romsDataGrid.SelectedItem is not string[] selectedGame || selectedGame.Length < 1)
            {
                MessageBox.Show("Please select a system to delete compiled AutoHotkey files.");
                return;
            }

            string systemName = selectedGame[0];
            if (string.IsNullOrEmpty(systemName))
            {
                MessageBox.Show("Invalid system name.");
                return;
            }

                DeleteCompiledAhk(systemName);
            }

        private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
        {
            string? systemName = selectedSystemTextBlock.Text;
            if (string.IsNullOrEmpty(systemName))
            {
                MessageBox.Show("No system selected.");
                return;
            }

            int colonIndex = systemName.IndexOf(':');
            if (colonIndex < 0 || colonIndex + 2 >= systemName.Length)
            {
                MessageBox.Show("Invalid system name format.");
                return;
            }

            systemName = systemName.Substring(colonIndex + 2).Trim();
            if (string.IsNullOrEmpty(systemName))
            {
                MessageBox.Show("System name is empty.");
                return;
            }

            string systemConfigPath = Path.Combine(Directory.GetCurrentDirectory(), systemName, "ahk", "system-config.prc");

            try
            {
                File.WriteAllText(systemConfigPath, configTextBox.Text);
                MessageBox.Show("The changes were saved successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while saving changes: {ex.Message}");
            }
        }

        private void SaveConfigNomousyButton_Click(object sender, RoutedEventArgs e)
        {
            string? systemName = selectedSystemTextBlock.Text;
            if (string.IsNullOrEmpty(systemName))
            {
                MessageBox.Show("No system selected.");
                return;
            }

            int colonIndex = systemName.IndexOf(':');
            if (colonIndex < 0 || colonIndex + 2 >= systemName.Length)
            {
                MessageBox.Show("Invalid system name format.");
                return;
            }

            systemName = systemName.Substring(colonIndex + 2).Trim();
            if (string.IsNullOrEmpty(systemName))
            {
                MessageBox.Show("System name is empty.");
                return;
            }

            string systemConfigNomousyPath = Path.Combine(Directory.GetCurrentDirectory(), ".nomousy", "system-config.prc");

            try
            {
                File.WriteAllText(systemConfigNomousyPath, configTextNomousyBox.Text);
                MessageBox.Show("The changes were saved successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while saving changes: {ex.Message}");
            }
        }

        private void CompileCustomAhkFiles(string systemName)
        {
            if (string.IsNullOrEmpty(systemName))
            {
                MessageBox.Show("Invalid system name.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string customAhkFolderPath = Path.Combine(Directory.GetCurrentDirectory(), systemName, "ahk", ".serial-send", ".edit");
            if (!Directory.Exists(customAhkFolderPath))
            {
                MessageBox.Show("Custom AHK folder not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string[] files = Directory.GetFiles(customAhkFolderPath, "*.ahk");
            if (files.Length == 0)
            {
                MessageBox.Show("No AHK files found in the custom folder.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                if (string.IsNullOrEmpty(fileName)) continue;

                string exeFilePath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    systemName,
                    "ahk",
                    ".serial-send",
                    fileName.Replace(".ahk", ".exe"));

                CompileAhkFile(file, exeFilePath);
            }
        }

        private void CompileAhkFile(string ahkFilePath, string exeFilePath)
        {
            if (string.IsNullOrEmpty(ahkFilePath) || string.IsNullOrEmpty(exeFilePath))
            {
                MessageBox.Show("Invalid file paths provided.");
                return;
            }

            if (!File.Exists(ahkFilePath))
            {
                MessageBox.Show("AutoHotkey file not found.");
                return;
            }

                try
                {
                    string ahk2ExeFolderPath = Path.Combine(Directory.GetCurrentDirectory(), ".Ahk2Exe");
                if (!Directory.Exists(ahk2ExeFolderPath))
                {
                    MessageBox.Show(".Ahk2Exe directory not found.");
                    return;
                }

                string ahk2ExePath = Path.Combine(ahk2ExeFolderPath, "Ahk2Exe.exe");
                string unicodeBinPath = Path.Combine(ahk2ExeFolderPath, "Unicode 64-bit.bin");

                if (!File.Exists(ahk2ExePath) || !File.Exists(unicodeBinPath))
                {
                    MessageBox.Show("Required Ahk2Exe files not found.");
                    return;
                }

                string command = $"\"{ahk2ExePath}\" /in \"{ahkFilePath}\" /out \"{exeFilePath}\" /compress 1 /base \"{unicodeBinPath}\"";
                    string batchFilePath = Path.Combine(ahk2ExeFolderPath, "compile.bat");
                    File.WriteAllText(batchFilePath, command);

                    ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", $"/c {batchFilePath}")
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    MessageBox.Show("Failed to start compilation process.");
                    return;
                }

                    process.WaitForExit();

                string? stderr = process.StandardError.ReadToEnd();
                    if (!string.IsNullOrEmpty(stderr))
                    {
                        Debug.WriteLine($"Compilation Errors: {stderr}");
                    }

                string fileName = Path.GetFileName(exeFilePath);
                if (string.IsNullOrEmpty(fileName))
                {
                    MessageBox.Show("Invalid output file name.");
                    return;
                }

                confirmationTextBlock.Text += $"successful saved: {fileName}\n";

                    timer.Interval = TimeSpan.FromSeconds(5);
                    timer.Tick += Timer_Tick;
                    timer.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error compiling AutoHotkey file: {ex.Message}");
            }
        }

        // Gestionnaire d'événements pour le clic sur le bouton de compilation personnalisé
        private void CompileCustomAhkButton_Click(object sender, RoutedEventArgs e)
        {
            if (romsDataGrid.SelectedItem is not string[] selectedGame || selectedGame.Length < 1)
            {
                MessageBox.Show("Please select a system to compile the custom AutoHotkey files.");
                return;
            }

                string systemName = selectedGame[0];
            if (string.IsNullOrEmpty(systemName))
            {
                MessageBox.Show("Invalid system name.");
                return;
            }

                CompileCustomAhkFiles(systemName);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchTerm = searchBox.Text?.ToLower() ?? string.Empty;
            List<string[]> filteredGameDetails = new List<string[]>();

                foreach (var systemEntry in systems)
                {
                    string systemName = systemEntry.Key;
                    List<string[]> systemDetails = systemEntry.Value;

                    foreach (string[] gameDetails in systemDetails)
                    {
                    if (string.IsNullOrEmpty(searchTerm) || 
                        (gameDetails.Length > 1 && gameDetails[1].ToLower().StartsWith(searchTerm)))
                    {
                        filteredGameDetails.Add(new[] { systemName }.Concat(gameDetails).ToArray());
                    }
                }
            }

            romsDataGrid.ItemsSource = filteredGameDetails;
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                updateButton.IsEnabled = false;
                downloadProgressBar.Visibility = Visibility.Visible;

                // Obtenir les informations de la dernière version
                var response = await _httpClient.GetStringAsync("https://api.github.com/repos/argonlefou/DemulShooter/releases/latest");
                if (string.IsNullOrEmpty(response) || !response.Contains("DemulShooter_v"))
                {
                    MessageBox.Show("Error retrieving latest version information.", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    updateButton.IsEnabled = true;
                    downloadProgressBar.Visibility = Visibility.Collapsed;
                    return;
                }

                // Extraire les informations de la dernière version
                string[] versionParts = response.Split("DemulShooter_v");
                if (versionParts.Length < 2)
                {
                    MessageBox.Show("Invalid version format in response.", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    updateButton.IsEnabled = true;
                    downloadProgressBar.Visibility = Visibility.Collapsed;
                    return;
                }

                string latestVersionStr = versionParts[1].Split(".zip")[0];
                string[] changelogParts = response.Split("\"body\":\"");
                if (changelogParts.Length < 2)
                {
                    MessageBox.Show("Invalid changelog format in response.", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    updateButton.IsEnabled = true;
                    downloadProgressBar.Visibility = Visibility.Collapsed;
                    return;
                }

                string changelogContent = changelogParts[1].Split("\",\"")[0]
                    .Replace("\\r\\n", Environment.NewLine)
                    .Replace("\\n", Environment.NewLine);

                // Créer et afficher la fenêtre de changelog
                var changelogWindow = new Window
                {
                    Title = $"DemulShooter Update v{latestVersionStr}",
                    Width = 600,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E5E5"))
                };

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var scrollViewer = new ScrollViewer
                {
                    Margin = new Thickness(10),
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                var textBlock = new TextBlock
                {
                    Text = $"Changelog for version {latestVersionStr}:\n\n{changelogContent}",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10),
                    FontSize = 14
                };

                scrollViewer.Content = textBlock;
                Grid.SetRow(scrollViewer, 0);

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(10)
                };

                var confirmButton = new Button
                {
                    Content = "Update",
                    Width = 100,
                    Height = 30,
                    Margin = new Thickness(5),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                    Foreground = Brushes.White
                };

                var cancelButton = new Button
                {
                    Content = "Cancel",
                    Width = 100,
                    Height = 30,
                    Margin = new Thickness(5),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                    Foreground = Brushes.White
                };

                buttonPanel.Children.Add(confirmButton);
                buttonPanel.Children.Add(cancelButton);
                Grid.SetRow(buttonPanel, 1);

                grid.Children.Add(scrollViewer);
                grid.Children.Add(buttonPanel);
                changelogWindow.Content = grid;

                bool proceedWithUpdate = false;
                confirmButton.Click += (s, args) =>
                {
                    proceedWithUpdate = true;
                    changelogWindow.Close();
                };
                cancelButton.Click += (s, args) => changelogWindow.Close();

                changelogWindow.ShowDialog();

                if (!proceedWithUpdate)
                {
                    updateButton.IsEnabled = true;
                    downloadProgressBar.Visibility = Visibility.Collapsed;
                    downloadProgressBar.Value = 0;
                    return;
                }

                // Créer le dossier temporaire s'il n'existe pas
                string tempPath = Path.Combine(Path.GetTempPath(), "DemulShooterUpdate");
                Directory.CreateDirectory(tempPath);
                string zipPath = Path.Combine(tempPath, "DemulShooter.zip");

                // Obtenir l'URL de téléchargement
                string[] downloadUrlParts = response.Split("\"browser_download_url\":\"");
                if (downloadUrlParts.Length < 2)
                {
                    throw new InvalidOperationException("Could not find download URL in response.");
                }
                string downloadUrl = downloadUrlParts[1].Split("\"")[0];

                // Télécharger le fichier avec progression
                using (var client = new HttpClient())
                {
                    using var response2 = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response2.EnsureSuccessStatusCode();
                    var totalBytes = response2.Content.Headers.ContentLength ?? -1L;
                    
                    using var stream = await response2.Content.ReadAsStreamAsync();
                    using var fileStream = File.Create(zipPath);
                    var buffer = new byte[8192];
                    var totalBytesRead = 0L;
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        totalBytesRead += bytesRead;
                        if (totalBytes > 0)
                        {
                            downloadProgressBar.Value = (double)totalBytesRead / totalBytes * 100;
                        }
                    }
                }

                // Extraire dans le dossier parent de ChangeLog.txt
                string changeLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "ChangeLog.txt");
                string? extractPath = Path.GetDirectoryName(Path.GetFullPath(changeLogPath));

                if (string.IsNullOrEmpty(extractPath))
                {
                    throw new DirectoryNotFoundException("Could not determine extraction path.");
                }

                // Extraire le zip
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractPath, true);

                // Nettoyer
                File.Delete(zipPath);
                Directory.Delete(tempPath, true);

                // Attendre que les fichiers soient écrits
                await Task.Delay(1000);

                // Après une mise à jour réussie, nous savons que la version actuelle est la version que nous venons d'installer
                _currentVersion = latestVersionStr;
                CurrentVersionText.Text = $"Current: v{latestVersionStr}";
                LatestVersionText.Text = $"Latest: v{latestVersionStr}";
                UpdateStatus.Text = "Up to date";
                UpdateIndicator.Fill = Brushes.Green;
                updateButton.IsEnabled = false;

                MessageBox.Show("Update completed successfully!", "Update Success", MessageBoxButton.OK, MessageBoxImage.Information);
                downloadProgressBar.Visibility = Visibility.Collapsed;
                downloadProgressBar.Value = 0;

                // Vérifier à nouveau la version pour mettre à jour l'affichage
                await CheckDemulShooterVersion();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during update: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                downloadProgressBar.Visibility = Visibility.Collapsed;
                downloadProgressBar.Value = 0;
                updateButton.IsEnabled = true;
            }
        }

        private async Task CheckDemulShooterVersion()
        {
            try
            {
                UpdateStatus.Text = "Checking...";
                UpdateIndicator.Fill = Brushes.Gray;
                updateButton.IsEnabled = false;  // Désactiver pendant la vérification
                CurrentVersionText.Text = "Current: Checking...";
                LatestVersionText.Text = "Latest: Checking...";

                // Lire la version actuelle depuis ChangeLog.txt
                string changeLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "ChangeLog.txt");
                if (!File.Exists(changeLogPath))
                {
                    Debug.WriteLine("ChangeLog.txt non trouvé");
                    UpdateStatus.Text = "Version check error";
                    UpdateIndicator.Fill = Brushes.Gray;
                    updateButton.IsEnabled = true;
                    CurrentVersionText.Text = "Current: Unknown";
                    LatestVersionText.Text = "Latest: Unknown";
                    return;
                }

                // Lire les premières lignes du fichier pour trouver la version
                string[] lines = await File.ReadAllLinesAsync(changeLogPath);
                string? versionLine = lines.FirstOrDefault(l => l.Contains("## ["));
                
                if (string.IsNullOrEmpty(versionLine))
                {
                    Debug.WriteLine("Pas de version trouvée dans ChangeLog.txt");
                    UpdateStatus.Text = "Version check error";
                    UpdateIndicator.Fill = Brushes.Gray;
                    updateButton.IsEnabled = true;
                    CurrentVersionText.Text = "Current: Unknown";
                    LatestVersionText.Text = "Latest: Unknown";
                    return;
                }

                var versionMatch = System.Text.RegularExpressions.Regex.Match(versionLine, @"\[([\d\.]+)\]");
                if (!versionMatch.Success)
                {
                    Debug.WriteLine("Format de version invalide dans ChangeLog.txt");
                    UpdateStatus.Text = "Version check error";
                    UpdateIndicator.Fill = Brushes.Gray;
                    updateButton.IsEnabled = true;
                    CurrentVersionText.Text = "Current: Unknown";
                    LatestVersionText.Text = "Latest: Unknown";
                    return;
                }

                _currentVersion = versionMatch.Groups[1].Value.Trim();
                Debug.WriteLine($"Version actuelle trouvée: {_currentVersion}");
                CurrentVersionText.Text = $"Current: v{_currentVersion}";

                // Obtenir la dernière version depuis GitHub
                var response = await _httpClient.GetStringAsync("https://api.github.com/repos/argonlefou/DemulShooter/releases/latest");
                if (string.IsNullOrEmpty(response) || !response.Contains("DemulShooter_v"))
                {
                    Debug.WriteLine("Réponse GitHub invalide");
                    UpdateStatus.Text = "Version check error";
                    UpdateIndicator.Fill = Brushes.Gray;
                    updateButton.IsEnabled = true;
                    LatestVersionText.Text = "Latest: Unknown";
                    return;
                }

                string[] versionParts = response.Split("DemulShooter_v");
                if (versionParts.Length < 2)
                {
                    Debug.WriteLine("Format de version invalide dans la réponse");
                    UpdateStatus.Text = "Version check error";
                    UpdateIndicator.Fill = Brushes.Gray;
                    updateButton.IsEnabled = true;
                    LatestVersionText.Text = "Latest: Unknown";
                    return;
                }

                _latestVersion = versionParts[1].Split(".zip")[0].Trim();
                Debug.WriteLine($"Dernière version trouvée: {_latestVersion}");
                LatestVersionText.Text = $"Latest: v{_latestVersion}";

                Debug.WriteLine($"Comparaison directe: '{_currentVersion}' vs '{_latestVersion}'");

                Version currentVer = Version.Parse(_currentVersion);
                Version latestVer = Version.Parse(_latestVersion);

                int comparison = currentVer.CompareTo(latestVer);
                if (comparison >= 0)  // Version actuelle est égale ou plus récente
                {
                    Debug.WriteLine("Version actuelle est à jour - Désactivation de la mise à jour");
                    UpdateStatus.Text = "Up to date";
                    UpdateIndicator.Fill = Brushes.Green;
                    updateButton.IsEnabled = false;
                }
                else
                {
                    Debug.WriteLine("Nouvelle version disponible - Activation de la mise à jour");
                    UpdateStatus.Text = "Update available!";
                    UpdateIndicator.Fill = Brushes.Red;
                    updateButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erreur lors de la vérification de version: {ex.Message}");
                UpdateStatus.Text = "Check failed";
                UpdateIndicator.Fill = Brushes.Gray;
                updateButton.IsEnabled = true;
                CurrentVersionText.Text = "Current: Unknown";
                LatestVersionText.Text = "Latest: Unknown";
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}