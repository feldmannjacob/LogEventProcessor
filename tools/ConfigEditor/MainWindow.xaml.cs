using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using ConfigEditor.Models;
using ConfigEditor.Services;
using ConfigEditor.Views;
using System.Text;
using System.Windows.Input;

namespace ConfigEditor
{
    public partial class MainWindow : Window
    {
        private readonly ConfigService _service = new ConfigService();
        private readonly ValidationService _validator = new ValidationService();
        private readonly PresetService _presets = new PresetService();
        private string _currentPath = string.Empty;
        public ObservableCollection<RegexRule> Rules { get; } = new ObservableCollection<RegexRule>();
        public ConfigRoot Current { get; private set; } = new ConfigRoot();

        public MainWindow()
        {
            InitializeComponent();
            RulesGrid.ItemsSource = Rules;

            // Prefer portable config next to the editor's EXE; fallback to repo config
            var portablePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.yaml");
            if (File.Exists(portablePath)) {
                LoadFrom(portablePath);
            } else {
                var repoPath = FindRepoRoot();
                var defaultPath = repoPath != null ? System.IO.Path.Combine(repoPath, "LogEventProcessor", "config.yaml") : null;
                if (defaultPath != null && File.Exists(defaultPath))
                {
                    // Load repo config for convenience, but default saves to portable path
                    LoadFrom(defaultPath);
                    _currentPath = portablePath;
                    Title = $"EQ Log Config Editor - {System.IO.Path.GetFileName(portablePath)}";
                }
                else
                {
                    // Start with empty model; default save will go to portable path
                    _currentPath = portablePath;
                    Current = new ConfigRoot();
                    BindGeneral();
                    Title = $"EQ Log Config Editor - {System.IO.Path.GetFileName(portablePath)}";
                }
            }
        }
        private void EditRule_Click(object sender, RoutedEventArgs e)
        {
            if (RulesGrid.SelectedItem is RegexRule r)
            {
                var dlg = new RuleEditorWindow(r);
                dlg.Owner = this;
                if (dlg.ShowDialog() == true)
                {
                    RulesGrid.Items.Refresh();
                    AutoSave();
                }
            }
        }

        private void RulesGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            EditRule_Click(sender, e);
        }

        private string? FindRepoRoot()
        {
            try
            {
                var dir = AppContext.BaseDirectory;
                var d = new DirectoryInfo(dir);
                while (d != null)
                {
                    if (File.Exists(System.IO.Path.Combine(d.FullName, ".gitignore")))
                        return d.FullName;
                    d = d.Parent;
                }
            }
            catch { }
            return null;
        }

        private void BindGeneral()
        {
            LogFilePathText.Text = Current.LogFilePath ?? string.Empty;
            OutputDirectoryText.Text = Current.OutputDirectory ?? string.Empty;
            PollingIntervalText.Text = Current.PollingIntervalMs.ToString();
            ParallelProcessingCheck.IsChecked = Current.ParallelProcessing;
            DebugModeCheck.IsChecked = Current.DebugMode;
            MaxQueueSizeText.Text = Current.MaxQueueSize.ToString();
            ProcessErrorsCheck.IsChecked = Current.ProcessErrors;
            ProcessWarningsCheck.IsChecked = Current.ProcessWarnings;
            ProcessInfoCheck.IsChecked = Current.ProcessInfo;
        }

        private void UpdateFromGeneral()
        {
            Current.LogFilePath = LogFilePathText.Text;
            Current.OutputDirectory = OutputDirectoryText.Text;
            if (int.TryParse(PollingIntervalText.Text, out var pi)) Current.PollingIntervalMs = pi; else Current.PollingIntervalMs = 1000;
            Current.ParallelProcessing = ParallelProcessingCheck.IsChecked == true;
            Current.DebugMode = DebugModeCheck.IsChecked == true;
            if (int.TryParse(MaxQueueSizeText.Text, out var mq)) Current.MaxQueueSize = mq; else Current.MaxQueueSize = 0;
            Current.ProcessErrors = ProcessErrorsCheck.IsChecked == true;
            Current.ProcessWarnings = ProcessWarningsCheck.IsChecked == true;
            Current.ProcessInfo = ProcessInfoCheck.IsChecked == true;
        }

        private void RefreshRules()
        {
            Rules.Clear();
            foreach (var r in Current.RegexRules)
            {
                Rules.Add(r);
            }
        }

        private void LoadFrom(string path)
        {
            try
            {
                var cfg = _service.Load(path);
                _currentPath = path;
                Current = cfg;
                BindGeneral();
                RefreshRules();
                Title = $"EQ Log Config Editor - {System.IO.Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Failed to load", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool SaveTo(string path)
        {
            try
            {
                UpdateFromGeneral();
                var issues = _validator.Validate(Current);
                var errors = issues.Where(i => i.Severity == ValidationSeverity.Error).ToList();
                if (errors.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Please fix the following before saving:");
                    foreach (var e in errors)
                    {
                        sb.AppendLine("- " + (string.IsNullOrEmpty(e.RuleName) ? "" : ($"[{e.RuleName}] ")) + e.Message);
                    }
                    MessageBox.Show(this, sb.ToString(), "Validation errors", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                _service.Save(path, Current);
                _currentPath = path;
                Title = $"EQ Log Config Editor - {System.IO.Path.GetFileName(path)}";
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Failed to save", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "YAML files (*.yaml;*.yml)|*.yaml;*.yml|All files (*.*)|*.*",
                FileName = string.IsNullOrEmpty(_currentPath) ? "config.yaml" : System.IO.Path.GetFileName(_currentPath)
            };
            if (dlg.ShowDialog(this) == true)
            {
                LoadFrom(dlg.FileName);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentPath))
            {
                // Default to portable path next to this EXE
                var portablePath = System.IO.Path.Combine(AppContext.BaseDirectory, "config.yaml");
                SaveTo(portablePath);
            }
            else
            {
                SaveTo(_currentPath);
            }
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "YAML files (*.yaml)|*.yaml|All files (*.*)|*.*",
                FileName = string.IsNullOrEmpty(_currentPath) ? "config.yaml" : System.IO.Path.GetFileName(_currentPath)
            };
            if (dlg.ShowDialog(this) == true)
            {
                SaveTo(dlg.FileName);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            UpdateFromGeneral();
            var issues = _validator.Validate(Current);
            if (issues.Count == 0)
            {
                MessageBox.Show(this, "No issues found.", "Validation", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var sb = new StringBuilder();
            foreach (var i in issues)
            {
                var tag = i.Severity == ValidationSeverity.Error ? "ERROR" : (i.Severity == ValidationSeverity.Warning ? "WARN" : "INFO");
                sb.AppendLine($"[{tag}] {(string.IsNullOrEmpty(i.RuleName) ? "" : (i.RuleName + ": "))}{i.Message}");
            }
            MessageBox.Show(this, sb.ToString(), "Validation results", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BrowseLogFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Log files (*.txt;*.log)|*.txt;*.log|All files (*.*)|*.*",
                FileName = LogFilePathText.Text
            };
            if (dlg.ShowDialog(this) == true)
            {
                LogFilePathText.Text = dlg.FileName;
            }
        }

        private void BrowseOutputDir_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            var res = dlg.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK)
            {
                OutputDirectoryText.Text = dlg.SelectedPath;
            }
        }

        private void AddRule_Click(object sender, RoutedEventArgs e)
        {
            var baseName = "new_rule";
            var idx = 1;
            var name = baseName;
            var existingNames = Rules.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            while (existingNames.Contains(name)) { idx++; name = $"{baseName}_{idx}"; }

            // Prepare a new rule but do not insert until confirmed
            var draft = new RegexRule { Name = name, Pattern = ".*", ActionType = "keystroke", ActionValue = "f1", Modifiers = 0, Enabled = true };
            var dlg = new RuleEditorWindow(draft);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                // Ensure unique name after possible edits
                var finalName = draft.Name ?? name;
                idx = 1;
                while (existingNames.Contains(finalName)) { idx++; finalName = $"{draft.Name}_{idx}"; }
                draft.Name = finalName;
                Rules.Add(draft);
                Current.RegexRules.Add(draft);
                AutoSave();
            }
        }

        private void DeleteRule_Click(object sender, RoutedEventArgs e)
        {
            var selected = RulesGrid.SelectedItems.Cast<RegexRule>().ToList();
            if (selected.Count == 0) return;
            if (MessageBox.Show(this, $"Delete {selected.Count} rule(s)?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            foreach (var r in selected)
            {
                Rules.Remove(r);
                Current.RegexRules.Remove(r);
            }
            AutoSave();
        }

        private void DuplicateRule_Click(object sender, RoutedEventArgs e)
        {
            if (RulesGrid.SelectedItem is RegexRule r)
            {
                var copy = r.Clone();
                copy.Name = r.Name + "_copy";
                Rules.Add(copy);
                Current.RegexRules.Add(copy);
                AutoSave();
            }
        }

        private void EditSteps_Click(object sender, RoutedEventArgs e)
        {
            if (RulesGrid.SelectedItem is RegexRule r)
            {
                var dlg = new StepsEditorWindow(r);
                dlg.Owner = this;
                if (dlg.ShowDialog() == true)
                {
                    // The dialog edits the collection in-place
                    RulesGrid.Items.Refresh();
                    AutoSave();
                }
            }
        }

        private void RulesGrid_RowEditEnding(object sender, System.Windows.Controls.DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction == System.Windows.Controls.DataGridEditAction.Commit)
            {
                // Delay autosave until after the grid commits to avoid reentrancy
                var _ = Dispatcher.BeginInvoke(new Action(() => { AutoSave(); }));
            }
        }

        private void RulesGrid_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
        {
            // Ensure cell-level edits like changing action type trigger a save
            var _ = Dispatcher.BeginInvoke(new Action(() => { AutoSave(); }));
        }

        private void RulesGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            // In some cases ComboBox edits commit on cell change; catch that too
            var _ = Dispatcher.BeginInvoke(new Action(() => { AutoSave(); }));
        }

        private void General_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            DebouncedAutoSave();
        }

        private void General_CheckChanged(object sender, RoutedEventArgs e)
        {
            AutoSave();
        }

        private DateTime _lastChange = DateTime.MinValue;
        private System.Timers.Timer? _debounceTimer;
        private void DebouncedAutoSave()
        {
            _lastChange = DateTime.UtcNow;
            if (_debounceTimer == null)
            {
                _debounceTimer = new System.Timers.Timer(400);
                _debounceTimer.AutoReset = false;
                _debounceTimer.Elapsed += (s, e) =>
                {
                    Dispatcher.Invoke(() => AutoSave());
                };
            }
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private bool _isSaving = false;
        private void AutoSave()
        {
            try
            {
                if (_isSaving) return;
                _isSaving = true;
                UpdateFromGeneral();
                var issues = _validator.Validate(Current);
                var errors = issues.Where(i => i.Severity == ValidationSeverity.Error).ToList();
                if (errors.Count > 0)
                {
                    // Do not save on validation errors; optionally surface indicator later
                    return;
                }
                var path = string.IsNullOrEmpty(_currentPath) ? System.IO.Path.Combine(AppContext.BaseDirectory, "config.yaml") : _currentPath;
                // Atomic write
                var tmp = path + ".tmp";
                _service.Save(tmp, Current);
                if (File.Exists(path))
                {
                    File.Replace(tmp, path, null);
                }
                else
                {
                    File.Move(tmp, path);
                }
                _currentPath = path;
            }
            catch { }
            finally { _isSaving = false; }
        }

        private void InsertPreset_Click(object sender, RoutedEventArgs e)
        {
            // Offer a simple chooser of EQ command presets
            var cmds = _presets.GetEqCommands();
            var dlg = new Presets.ChoosePresetWindow(cmds, _presets.GetPresets());
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                foreach (var rule in dlg.SelectedRules)
                {
                    // ensure unique name
                    var baseName = rule.Name;
                    var name = baseName;
                    int i = 1;
                    var names = Current.RegexRules.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    while (names.Contains(name)) { i++; name = baseName + "_" + i; }
                    var toAdd = rule.Clone();
                    toAdd.Name = name;
                    Current.RegexRules.Add(toAdd);
                    Rules.Add(toAdd);
                }
            }
        }

        private void CommitAllEdits() { try { this.UpdateLayout(); } catch { } }
    }
}


