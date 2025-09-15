using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace ConfigEditor.Views
{
    public partial class ProcessSelector : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? SelectionChanged;

        private ObservableCollection<ProcessInfo> _processes = new ObservableCollection<ProcessInfo>();
        private bool _targetAllProcesses = false;

        public ProcessSelector()
        {
            try
            {
                InitializeComponent();
                
                // Subscribe to ProcessInfo selection changes
                ProcessInfo.SelectionChanged += (sender, e) => {
                    try
                    {
                        SelectionChanged?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in ProcessInfo.SelectionChanged handler: {ex.Message}");
                    }
                };
            }
            catch (Exception ex)
            {
                // Log the error but don't throw to avoid startup issues
                System.Diagnostics.Debug.WriteLine($"Error in ProcessSelector constructor: {ex.Message}");
                // Don't re-throw the exception to prevent startup failures
            }
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            
            try
            {
                // Initialize after the control is fully created
                ProcessListBox.ItemsSource = _processes;
                
                // Initialize UI with current state
                UpdateUI();
                
                // Add event handlers
                Loaded += ProcessSelector_Loaded;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ProcessSelector OnInitialized: {ex.Message}");
            }
        }

        private void ProcessSelector_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Remove the event handler to avoid multiple calls
                Loaded -= ProcessSelector_Loaded;
                
                // Use a timer to delay the process refresh slightly
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(500); // Increased delay
                timer.Tick += (s, args) => {
                    timer.Stop();
                    RefreshProcesses();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ProcessSelector Loaded event: {ex.Message}");
            }
        }

        public bool TargetAllProcesses
        {
            get => _targetAllProcesses;
            set
            {
                if (_targetAllProcesses != value)
                {
                    _targetAllProcesses = value;
                    OnPropertyChanged();
                    UpdateUI();
                }
            }
        }

        public List<int> SelectedProcessIds
        {
            get
            {
                if (_targetAllProcesses)
                    return new List<int>();
                
                return _processes.Where(p => p.IsSelected).Select(p => p.ProcessId).ToList();
            }
        }

        public List<string> SelectedProcessNames
        {
            get
            {
                if (_targetAllProcesses)
                    return new List<string>();
                
                return _processes.Where(p => p.IsSelected).Select(p => p.ProcessName).ToList();
            }
        }

        private void UpdateUI()
        {
            try
            {
                if (TargetAllRadio != null && TargetSpecificRadio != null)
                {
                    TargetAllRadio.IsChecked = _targetAllProcesses;
                    TargetSpecificRadio.IsChecked = !_targetAllProcesses;
                }
                
                bool isSpecificMode = !_targetAllProcesses;
                if (ProcessListBox != null)
                    ProcessListBox.IsEnabled = isSpecificMode;
                if (SelectAllButton != null)
                    SelectAllButton.IsEnabled = isSpecificMode;
                if (ClearAllButton != null)
                    ClearAllButton.IsEnabled = isSpecificMode;
                
                if (StatusText != null)
                {
                    StatusText.Text = _targetAllProcesses 
                        ? "Targeting all EQGame.exe processes" 
                        : $"Targeting {SelectedProcessIds.Count} selected process(es)";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateUI: {ex.Message}");
            }
        }

        private void TargetMode_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.RadioButton radioButton)
                {
                    _targetAllProcesses = radioButton == TargetAllRadio;
                    UpdateUI();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in TargetMode_Changed: {ex.Message}");
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshProcesses();
        }

        private void RefreshProcesses()
        {
            try
            {
                _processes.Clear();
                
                var eqProcesses = Process.GetProcesses()
                    .Where(p => p.ProcessName.Equals("eqgame", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p.ProcessName)
                    .ThenBy(p => p.Id);

                foreach (var process in eqProcesses)
                {
                    try
                    {
                        var processInfo = new ProcessInfo
                        {
                            ProcessId = process.Id,
                            ProcessName = process.ProcessName,
                            WindowTitle = process.MainWindowTitle,
                            IsSelected = false
                        };
                        _processes.Add(processInfo);
                    }
                    catch (Exception ex)
                    {
                        // Skip processes we can't access
                        System.Diagnostics.Debug.WriteLine($"Could not access process {process.Id}: {ex.Message}");
                    }
                }
                
                // After loading processes, apply any pending selection
                ApplyPendingSelection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing processes: {ex.Message}");
                // Don't show MessageBox during startup as it might cause issues
                if (IsLoaded)
                {
                    System.Windows.MessageBox.Show($"Error refreshing processes: {ex.Message}", "Error", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var process in _processes)
                {
                    process.IsSelected = true;
                }
                UpdateStatus();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SelectAll_Click: {ex.Message}");
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var process in _processes)
                {
                    process.IsSelected = false;
                }
                UpdateStatus();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ClearAll_Click: {ex.Message}");
            }
        }

        private void UpdateStatus()
        {
            try
            {
                if (!_targetAllProcesses && StatusText != null)
                {
                    StatusText.Text = $"Targeting {SelectedProcessIds.Count} selected process(es)";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateStatus: {ex.Message}");
            }
        }

        private bool _pendingTargetAllProcesses;
        private List<int> _pendingProcessIds = new List<int>();

        public void LoadFromConfig(bool targetAllProcesses, List<int> processIds, List<string> processNames)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"ProcessSelector.LoadFromConfig: targetAll={targetAllProcesses}, IDs={processIds.Count}");
                System.Diagnostics.Debug.WriteLine($"ProcessSelector.LoadFromConfig: Before setting - _targetAllProcesses={_targetAllProcesses}");
                
                _targetAllProcesses = targetAllProcesses;
                System.Diagnostics.Debug.WriteLine($"ProcessSelector.LoadFromConfig: After setting - _targetAllProcesses={_targetAllProcesses}");
                
                UpdateUI();
                System.Diagnostics.Debug.WriteLine($"ProcessSelector.LoadFromConfig: After UpdateUI - TargetAllRadio.IsChecked={TargetAllRadio?.IsChecked}, TargetSpecificRadio.IsChecked={TargetSpecificRadio?.IsChecked}");
                
                if (!_targetAllProcesses)
                {
                    // Store the pending selection data (only process IDs)
                    _pendingTargetAllProcesses = targetAllProcesses;
                    _pendingProcessIds = new List<int>(processIds);
                    
                    System.Diagnostics.Debug.WriteLine($"ProcessSelector.LoadFromConfig: Stored pending selection - IDs={_pendingProcessIds.Count}");
                    
                    // If processes are already loaded, apply the selection immediately
                    if (_processes.Count > 0)
                    {
                        ApplyPendingSelection();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("ProcessSelector.LoadFromConfig: No processes loaded yet, will apply selection after RefreshProcesses");
                    }
                }
                System.Diagnostics.Debug.WriteLine($"ProcessSelector.LoadFromConfig: After loading - TargetAll={_targetAllProcesses}, SelectedCount={_processes.Count(p => p.IsSelected)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadFromConfig: {ex.Message}");
            }
        }

        private void ApplyPendingSelection()
        {
            try
            {
                if (!_pendingTargetAllProcesses && _pendingProcessIds.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"ApplyPendingSelection: Applying selection to {_processes.Count} processes");
                    System.Diagnostics.Debug.WriteLine($"ApplyPendingSelection: Pending IDs={_pendingProcessIds.Count}");
                    
                    // Mark processes as selected based on saved process IDs only
                    foreach (var process in _processes)
                    {
                        process.IsSelected = _pendingProcessIds.Contains(process.ProcessId);
                        
                        if (process.IsSelected)
                        {
                            System.Diagnostics.Debug.WriteLine($"ApplyPendingSelection: Selected process {process.ProcessId} ({process.ProcessName})");
                        }
                    }
                    UpdateStatus();
                    
                    System.Diagnostics.Debug.WriteLine($"ApplyPendingSelection: Applied selection - SelectedCount={_processes.Count(p => p.IsSelected)}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ApplyPendingSelection: Skipping - TargetAll={_pendingTargetAllProcesses}, IDs={_pendingProcessIds.Count}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ApplyPendingSelection: {ex.Message}");
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ProcessInfo : INotifyPropertyChanged
    {
        public static event EventHandler? SelectionChanged;
        
        private bool _isSelected;
        private int _processId;
        private string _processName = string.Empty;
        private string _windowTitle = string.Empty;

        public int ProcessId
        {
            get => _processId;
            set => SetField(ref _processId, value);
        }

        public string ProcessName
        {
            get => _processName;
            set => SetField(ref _processName, value);
        }

        public string WindowTitle
        {
            get => _windowTitle;
            set => SetField(ref _windowTitle, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetField(ref _isSelected, value))
                {
                    // Bring the selected process window into focus
                    if (value) // Only when selecting (not deselecting)
                    {
                        FocusProcessWindow();
                    }
                    
                    // Notify parent when selection changes
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                    
                    // Fire static event for ProcessSelector to handle
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void FocusProcessWindow()
        {
            try
            {
                // Bring the specific process window into focus
                var process = Process.GetProcessById(ProcessId);
                if (process != null && !process.HasExited && process.MainWindowHandle != IntPtr.Zero)
                {
                    // Set focus to the EQGame window
                    SetForegroundWindow(process.MainWindowHandle);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error focusing process {ProcessId}: {ex.Message}");
            }
        }

        // Windows API imports for window focus management
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public string ProcessDescription => string.IsNullOrEmpty(WindowTitle) 
            ? $"PID: {ProcessId}" 
            : $"{WindowTitle} (PID: {ProcessId})";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}