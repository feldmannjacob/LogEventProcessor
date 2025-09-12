using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ConfigEditor.Models;

namespace ConfigEditor.Presets
{
    public partial class ChoosePresetWindow : Window
    {
        private readonly string[] _allCommands;
        private readonly List<RegexRule> _allPresets;
        public ObservableCollection<RegexRule> SelectedRules { get; } = new ObservableCollection<RegexRule>();
        public ObservableCollection<string> SelectedCommands { get; } = new ObservableCollection<string>();

        public ChoosePresetWindow(string[] commands, IEnumerable<RegexRule> presetRules)
        {
            InitializeComponent();
            _allCommands = commands;
            _allPresets = presetRules.ToList();
            CommandsList.ItemsSource = _allCommands.OrderBy(x => x).ToList();
            PresetsList.ItemsSource = _allPresets;
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            var q = SearchText.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(q))
            {
                CommandsList.ItemsSource = _allCommands.OrderBy(x => x).ToList();
                return;
            }
            CommandsList.ItemsSource = _allCommands.Where(c => c.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(x => x).ToList();
        }

        private void Insert_Click(object sender, RoutedEventArgs e)
        {
            SelectedRules.Clear();
            SelectedCommands.Clear();
            foreach (var obj in PresetsList.SelectedItems)
            {
                if (obj is RegexRule r)
                {
                    SelectedRules.Add(r);
                }
            }
            foreach (var obj in CommandsList.SelectedItems)
            {
                if (obj is string cmd && !string.IsNullOrWhiteSpace(cmd))
                {
                    SelectedCommands.Add(cmd);
                }
            }
            if (SelectedRules.Count == 0 && SelectedCommands.Count == 0)
            {
                MessageBox.Show(this, "Select at least one preset or EQ command.", "Nothing selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}


