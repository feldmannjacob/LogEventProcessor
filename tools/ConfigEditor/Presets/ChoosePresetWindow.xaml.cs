using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ConfigEditor.Models;
using ConfigEditor.Services;

namespace ConfigEditor.Presets
{
    public partial class ChoosePresetWindow : Window
    {
        private readonly string[] _allCommands;
        private readonly List<RegexRule> _allPresets;
        private readonly SpellService _spells = new SpellService();
        public ObservableCollection<RegexRule> SelectedRules { get; } = new ObservableCollection<RegexRule>();
        public ObservableCollection<string> SelectedCommands { get; } = new ObservableCollection<string>();
        public ObservableCollection<Spell> SelectedSpells { get; } = new ObservableCollection<Spell>();
        private IReadOnlyList<Spell> _cleric = Array.Empty<Spell>();
        private IReadOnlyList<Spell> _shaman = Array.Empty<Spell>();

        public ChoosePresetWindow(string[] commands, IEnumerable<RegexRule> presetRules)
        {
            InitializeComponent();
            _allCommands = commands;
            _allPresets = presetRules.ToList();
            CommandsList.ItemsSource = _allCommands.OrderBy(x => x).ToList();
            PresetsList.ItemsSource = _allPresets;
            _cleric = _spells.GetClericSpells();
            _shaman = _spells.GetShamanSpells();
            if (SpellClassFilter != null) SpellClassFilter.SelectedIndex = 0;
            if (MinLevelFilter != null) MinLevelFilter.Text = "1";
            if (MaxLevelFilter != null) MaxLevelFilter.Text = "70";
            ApplySpellFilter();
            if (ApplySpellFilterButton != null) ApplySpellFilterButton.Click += (s, e) => ApplySpellFilter();
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
            SelectedSpells.Clear();
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
            foreach (var obj in SpellsList.SelectedItems)
            {
                if (obj is Spell s)
                {
                    SelectedSpells.Add(s);
                }
            }
            if (SelectedRules.Count == 0 && SelectedCommands.Count == 0 && SelectedSpells.Count == 0)
            {
                MessageBox.Show(this, "Select at least one preset or EQ command.", "Nothing selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
        }

        private void ApplySpellFilter()
        {
            int.TryParse(MinLevelFilter?.Text ?? "1", out var min);
            int.TryParse(MaxLevelFilter?.Text ?? "70", out var max);
            var minLevel = min > 0 ? (int?)min : null;
            var maxLevel = max > 0 ? (int?)max : null;
            var cls = (SpellClassFilter?.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Cleric";
            var baseList = cls.Equals("Shaman", StringComparison.OrdinalIgnoreCase) ? _shaman : _cleric;
            var filtered = _spells.FilterByLevel(baseList, minLevel, maxLevel);
            SpellsList.ItemsSource = filtered;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}


