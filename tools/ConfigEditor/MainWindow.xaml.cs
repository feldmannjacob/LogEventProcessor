using Microsoft.Win32;
using System;
using System.Collections.Generic;
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
            
            // Subscribe to property changes for auto-save
            Rules.CollectionChanged += (s, e) => {
                if (e.NewItems != null)
                {
                    foreach (RegexRule rule in e.NewItems)
                    {
                        rule.PropertyChanged += Rule_PropertyChanged;
                    }
                }
                if (e.OldItems != null)
                {
                    foreach (RegexRule rule in e.OldItems)
                    {
                        rule.PropertyChanged -= Rule_PropertyChanged;
                    }
                }
            };

            // Load configuration after the window is fully loaded
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Prefer portable config next to the editor's EXE; fallback to repo config
                var portablePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.yaml");
                if (File.Exists(portablePath)) {
                    LoadFrom(portablePath);
                } else {
                    var repoPath = FindRepoRoot();
                    var defaultPath = repoPath != null ? System.IO.Path.Combine(repoPath, "LogEventProcessor", "config.yaml") : null;
                    if (defaultPath != null && File.Exists(defaultPath))
                    {
                        // Load repo config and save to the same file
                        _currentPath = defaultPath; // Set the current path BEFORE loading
                        LoadFrom(defaultPath);
                        Title = $"EQ Log Config Editor - {System.IO.Path.GetFileName(defaultPath)}";
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
            catch (Exception ex)
            {
                MessageBox.Show($"Error during startup: {ex.Message}\n\nStack trace:\n{ex.StackTrace}", 
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                var dir = AppDomain.CurrentDomain.BaseDirectory;
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
            try
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
                
                // Email configuration
                EmailSmtpServerText.Text = Current.EmailSmtpServer ?? string.Empty;
                EmailSmtpPortText.Text = Current.EmailSmtpPort.ToString();
                EmailUsernameText.Text = Current.EmailUsername ?? string.Empty;
                EmailPasswordText.Password = Current.EmailPassword ?? string.Empty;
                EmailFromText.Text = Current.EmailFrom ?? string.Empty;
                EmailToText.Text = Current.EmailTo ?? string.Empty;
                EmailEnableSslCheck.IsChecked = Current.EmailEnableSsl;
                EmailPollIntervalText.Text = Current.EmailPollIntervalSeconds.ToString();
                
                // Process targeting configuration - only if ProcessSelector is loaded
                try
                {
                    System.Diagnostics.Debug.WriteLine($"BindGeneral: ProcessSelector null: {ProcessSelector == null}, IsLoaded: {ProcessSelector?.IsLoaded}");
                    System.Diagnostics.Debug.WriteLine($"BindGeneral: Current values - TargetAll={Current.TargetAllProcesses}, IDs={Current.TargetProcessIds?.Count ?? 0}, Names={Current.TargetProcessNames?.Count ?? 0}");
                    
                    if (ProcessSelector != null && ProcessSelector.IsLoaded)
                    {
                        System.Diagnostics.Debug.WriteLine("BindGeneral: Loading ProcessSelector config immediately");
                        ProcessSelector.LoadFromConfig(Current.TargetAllProcesses, Current.TargetProcessIds ?? new List<int>(), new List<string>());
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("BindGeneral: ProcessSelector not ready, deferring config load");
                        // If ProcessSelector isn't ready, defer the configuration loading
                        if (ProcessSelector != null)
                        {
                            // Remove any existing Loaded event handler to avoid duplicates
                            ProcessSelector.Loaded -= ProcessSelector_LoadedHandler;
                            ProcessSelector.Loaded += ProcessSelector_LoadedHandler;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error accessing ProcessSelector: {ex.Message}");
                    // Continue without ProcessSelector configuration - it's not critical for startup
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in BindGeneral: {ex.Message}\n\nStack trace:\n{ex.StackTrace}", 
                    "BindGeneral Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            
            // Email configuration
            Current.EmailSmtpServer = EmailSmtpServerText.Text;
            if (int.TryParse(EmailSmtpPortText.Text, out var port)) Current.EmailSmtpPort = port; else Current.EmailSmtpPort = 587;
            Current.EmailUsername = EmailUsernameText.Text;
            Current.EmailPassword = EmailPasswordText.Password;
            Current.EmailFrom = EmailFromText.Text;
            Current.EmailTo = EmailToText.Text;
            Current.EmailEnableSsl = EmailEnableSslCheck.IsChecked == true;
            if (int.TryParse(EmailPollIntervalText.Text, out var epi)) Current.EmailPollIntervalSeconds = epi; else Current.EmailPollIntervalSeconds = 30;
            
            // Process targeting configuration - only if ProcessSelector is ready
            try
            {
                if (ProcessSelector != null && ProcessSelector.IsLoaded)
                {
                    System.Diagnostics.Debug.WriteLine($"UpdateFromGeneral: Setting TargetAll={ProcessSelector.TargetAllProcesses}, IDs={ProcessSelector.SelectedProcessIds.Count}");
                    Current.TargetAllProcesses = ProcessSelector.TargetAllProcesses;
                    Current.TargetProcessIds = ProcessSelector.SelectedProcessIds;
                    Current.TargetProcessNames = new List<string>(); // Always empty since we only use process IDs
                    System.Diagnostics.Debug.WriteLine($"UpdateFromGeneral: Current values set to TargetAll={Current.TargetAllProcesses}, IDs={Current.TargetProcessIds?.Count ?? 0}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"UpdateFromGeneral: ProcessSelector not available or not loaded, preserving current values: TargetAll={Current.TargetAllProcesses}, IDs={Current.TargetProcessIds?.Count ?? 0}, Names={Current.TargetProcessNames?.Count ?? 0}");
                }
                // Don't reset to defaults when ProcessSelector is not available - preserve current values
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error accessing ProcessSelector in UpdateFromGeneral: {ex.Message}");
                // Don't reset to defaults on error - preserve current values
            }
        }

        private void RefreshRules()
        {
            // Unsubscribe from old rules
            foreach (var rule in Rules)
            {
                rule.PropertyChanged -= Rule_PropertyChanged;
            }
            
            Rules.Clear();
            foreach (var r in Current.RegexRules)
            {
                r.PropertyChanged += Rule_PropertyChanged;
                Rules.Add(r);
            }
        }

        private void LoadFrom(string path)
        {
            try
            {
                _isLoading = true;
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
            finally
            {
                _isLoading = false;
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
                var portablePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.yaml");
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
            var draft = new RegexRule { Name = name, Pattern = ".*TESTPATTERN.*", ActionType = "keystroke", ActionValue = "f1", Modifiers = 0, Enabled = true };
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
            // Ensure cell-level edits like changing enabled state trigger a save
            if (e.Column.Header.ToString() == "Enabled")
            {
                // Get the rule and update its enabled state
                if (e.Row.DataContext is RegexRule rule && e.EditingElement is System.Windows.Controls.CheckBox checkbox)
                {
                    rule.Enabled = checkbox.IsChecked == true;
                }
                
                var _ = Dispatcher.BeginInvoke(new Action(() => { AutoSave(); }));
            }
        }

        private void RulesGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            // Check if we're in the Enabled column and trigger save
            if (RulesGrid.CurrentCell != null && RulesGrid.CurrentCell.Column != null)
            {
                var columnHeader = RulesGrid.CurrentCell.Column.Header?.ToString();
                if (columnHeader == "Enabled")
                {
                    var _ = Dispatcher.BeginInvoke(new Action(() => { AutoSave(); }));
                }
            }
            
            // Also trigger a delayed save for any cell change to catch enabled changes
            Dispatcher.BeginInvoke(new Action(() => {
                System.Threading.Tasks.Task.Delay(100).ContinueWith(task => {
                    Dispatcher.Invoke(() => {
                        AutoSave();
                    });
                });
            }));
        }

        private void General_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            DebouncedAutoSave();
        }

        private void General_CheckChanged(object sender, RoutedEventArgs e)
        {
            AutoSave();
        }

        private void General_PasswordChanged(object sender, RoutedEventArgs e)
        {
            DebouncedAutoSave();
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
        private bool _isLoading = false;
        private void AutoSave()
        {
            try
            {
                if (_isSaving || _isLoading) 
                {
                    System.Diagnostics.Debug.WriteLine($"AutoSave skipped - _isSaving: {_isSaving}, _isLoading: {_isLoading}");
                    return;
                }
                _isSaving = true;
                System.Diagnostics.Debug.WriteLine($"AutoSave starting - Current values before UpdateFromGeneral: TargetAll={Current.TargetAllProcesses}, IDs={Current.TargetProcessIds?.Count ?? 0}, Names={Current.TargetProcessNames?.Count ?? 0}");
                UpdateFromGeneral();
                System.Diagnostics.Debug.WriteLine($"AutoSave - Current values after UpdateFromGeneral: TargetAll={Current.TargetAllProcesses}, IDs={Current.TargetProcessIds?.Count ?? 0}, Names={Current.TargetProcessNames?.Count ?? 0}");
                var issues = _validator.Validate(Current);
                var errors = issues.Where(i => i.Severity == ValidationSeverity.Error).ToList();
                if (errors.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"AutoSave skipped due to validation errors: {string.Join(", ", errors.Select(e => e.Message))}");
                    return;
                }
                var path = string.IsNullOrEmpty(_currentPath) ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.yaml") : _currentPath;
                System.Diagnostics.Debug.WriteLine($"AutoSave saving to: {path}");
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
                System.Diagnostics.Debug.WriteLine("AutoSave completed successfully");
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"AutoSave failed: {ex.Message}");
            }
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
                // For each selected preset, open the Rule Editor prefilled with action fields
                foreach (var preset in dlg.SelectedRules)
                {
                    var prefilled = new RegexRule
                    {
                        // Leave Name/Pattern empty so user provides them
                        Name = string.Empty,
                        Pattern = string.Empty,
                        Enabled = true,
                        CooldownMs = 0,
                        Modifiers = 0
                    };

                    // Prefer top-level ActionType/Value; fallback to first step if present
                    if (!string.IsNullOrWhiteSpace(preset.ActionType))
                    {
                        prefilled.ActionType = preset.ActionType;
                        prefilled.ActionValue = preset.ActionValue;
                    }
                    else if (preset.Actions != null && preset.Actions.Count > 0)
                    {
                        var first = preset.Actions[0];
                        prefilled.ActionType = first.Type;
                        prefilled.ActionValue = first.Value;
                    }

                    var editor = new Views.RuleEditorWindow(prefilled) { Owner = this };
                    if (editor.ShowDialog() == true)
                    {
                        // Ensure unique and non-empty name
                        var baseName = string.IsNullOrWhiteSpace(prefilled.Name) ? "new_rule" : prefilled.Name;
                        var finalName = baseName;
                        int i = 1;
                        var existingNames = Current.RegexRules.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        while (existingNames.Contains(finalName)) { i++; finalName = baseName + "_" + i; }
                        prefilled.Name = finalName;

                        Current.RegexRules.Add(prefilled);
                        Rules.Add(prefilled);
                        AutoSave();
                    }
                }

                // Also allow direct EQ command selection to prefill action type/value
                foreach (var cmd in dlg.SelectedCommands)
                {
                    var prefilled = new RegexRule
                    {
                        Name = string.Empty,
                        Pattern = string.Empty,
                        Enabled = true,
                        CooldownMs = 0,
                        Modifiers = 0,
                        ActionType = "command",
                        ActionValue = cmd
                    };

                    var editor = new Views.RuleEditorWindow(prefilled) { Owner = this };
                    if (editor.ShowDialog() == true)
                    {
                        var baseName = string.IsNullOrWhiteSpace(prefilled.Name) ? "new_rule" : prefilled.Name;
                        var finalName = baseName;
                        int i = 1;
                        var existingNames = Current.RegexRules.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        while (existingNames.Contains(finalName)) { i++; finalName = baseName + "_" + i; }
                        prefilled.Name = finalName;

                        Current.RegexRules.Add(prefilled);
                        Rules.Add(prefilled);
                        AutoSave();
                    }
                }

                // And allow selecting spells: prefill as /cast <spell name>
                foreach (var spell in dlg.SelectedSpells)
                {
                    var prefilled = new RegexRule
                    {
                        Name = string.Empty,
                        Pattern = string.Empty,
                        Enabled = true,
                        CooldownMs = 0,
                        Modifiers = 0,
                        ActionType = "spell",
                        ActionValue = spell.Name
                    };

                    var editor = new Views.RuleEditorWindow(prefilled) { Owner = this };
                    if (editor.ShowDialog() == true)
                    {
                        var baseName = string.IsNullOrWhiteSpace(prefilled.Name) ? "new_rule" : prefilled.Name;
                        var finalName = baseName;
                        int i = 1;
                        var existingNames = Current.RegexRules.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        while (existingNames.Contains(finalName)) { i++; finalName = baseName + "_" + i; }
                        prefilled.Name = finalName;

                        Current.RegexRules.Add(prefilled);
                        Rules.Add(prefilled);
                        AutoSave();
                    }
                }
            }
        }

        private void Rule_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Auto-save when any rule property changes (especially Enabled)
            if (e.PropertyName == nameof(RegexRule.Enabled))
            {
                AutoSave();
            }
        }

        private void EnabledCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // Get the checkbox and its data context
            if (sender is System.Windows.Controls.CheckBox checkbox)
            {
                var rule = checkbox.DataContext as RegexRule;
                if (rule != null)
                {
                    // Explicitly update the property
                    rule.Enabled = checkbox.IsChecked == true;
                }
            }
            
            // Auto-save
            AutoSave();
        }


        private void ProcessSelector_LoadedHandler(object? sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("ProcessSelector Loaded event fired, loading config");
                if (ProcessSelector != null)
                {
                    // Use a small delay to ensure the ProcessSelector is fully initialized
                    var timer = new System.Windows.Threading.DispatcherTimer();
                    timer.Interval = TimeSpan.FromMilliseconds(100);
                    timer.Tick += (s, args) => {
                        timer.Stop();
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"ProcessSelector_LoadedHandler delayed: TargetAll={Current.TargetAllProcesses}, IDs={Current.TargetProcessIds?.Count ?? 0}");
                            ProcessSelector.LoadFromConfig(Current.TargetAllProcesses, Current.TargetProcessIds ?? new List<int>(), new List<string>());
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error in delayed ProcessSelector LoadFromConfig: {ex.Message}");
                        }
                    };
                    timer.Start();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ProcessSelector Loaded event: {ex.Message}");
            }
        }

        private void ProcessSelector_SelectionChanged(object? sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"ProcessSelector_SelectionChanged called - ProcessSelector null: {ProcessSelector == null}, IsLoaded: {ProcessSelector?.IsLoaded}");
                
                // Only auto-save if ProcessSelector is available and loaded
                if (ProcessSelector != null && ProcessSelector.IsLoaded)
                {
                    System.Diagnostics.Debug.WriteLine($"ProcessSelector_SelectionChanged: TargetAll={ProcessSelector.TargetAllProcesses}, IDs={ProcessSelector.SelectedProcessIds.Count}, Names={ProcessSelector.SelectedProcessNames.Count}");
                    AutoSave();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ProcessSelector_SelectionChanged: ProcessSelector not available or not loaded");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ProcessSelector_SelectionChanged: {ex.Message}");
            }
        }

        private void CommitAllEdits() { try { this.UpdateLayout(); } catch { } }
    }
}


