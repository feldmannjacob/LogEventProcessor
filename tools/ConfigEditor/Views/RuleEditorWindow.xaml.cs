using System.Windows;
using ConfigEditor.Models;
using ConfigEditor.Services;
using System.Linq;

namespace ConfigEditor.Views
{
    public partial class RuleEditorWindow : Window
    {
        public RegexRule Rule { get; }
        private bool _initialized;
        private readonly SpellService _spells = new SpellService();
        private string? _pendingSpellSelection;

        public RuleEditorWindow(RegexRule rule)
        {
            InitializeComponent();
            Rule = rule;
            DataContext = Rule;
            _initialized = true;
            // If command type, split existing ActionValue into command + argument for display
            if (string.Equals(Rule.ActionType, "command", System.StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(Rule.ActionValue))
            {
                var parts = Rule.ActionValue.Trim();
                if (parts.StartsWith("/")) parts = parts.Substring(1);
                var space = parts.IndexOf(' ');
                if (space >= 0)
                {
                    CommandNameText.Text = parts.Substring(0, space);
                    CommandArgText.Text = parts.Substring(space + 1);
                }
                else
                {
                    CommandNameText.Text = parts;
                }
            }

            // If spell type, parse existing ActionValue to select the spell
            if (string.Equals(Rule.ActionType, "spell", System.StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(Rule.ActionValue))
            {
                var actionValue = Rule.ActionValue.Trim();
                if (actionValue.StartsWith("/cast "))
                {
                    var spellName = actionValue.Substring(6); // Remove "/cast "
                    // We'll set the selected spell after the list is populated
                    _pendingSpellSelection = spellName;
                }
            }

            // Initialize spell UI defaults
            if (SpellClassCombo != null) { SpellClassCombo.SelectionChanged += (s, e) => RefreshSpellList(); SpellClassCombo.SelectedIndex = 0; }
            if (MinLevelText != null) MinLevelText.Text = "1";
            if (MaxLevelText != null) MaxLevelText.Text = "70";
            if (MinLevelText != null) MinLevelText.TextChanged += (s, e) => RefreshSpellList();
            if (MaxLevelText != null) MaxLevelText.TextChanged += (s, e) => RefreshSpellList();
            RefreshSpellList();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // For command type, compose ActionValue as "/" + command + optional argument
            if (string.Equals(Rule.ActionType, "command", System.StringComparison.OrdinalIgnoreCase))
            {
                var cmd = (CommandNameText.Text ?? string.Empty).Trim();
                var arg = (CommandArgText.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(cmd))
                {
                    MessageBox.Show(this, "Command is required for command action.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Rule.ActionValue = string.IsNullOrEmpty(arg) ? $"/{cmd}" : $"/{cmd} {arg}";
            }
            else if (string.Equals(Rule.ActionType, "spell", System.StringComparison.OrdinalIgnoreCase))
            {
                var selected = SpellNameCombo?.SelectedItem as Spell;
                if (selected == null)
                {
                    MessageBox.Show(this, "Please select a spell.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Rule.ActionType = "command";
                Rule.ActionValue = $"/cast {selected.Name}";
            }
            else if (string.Equals(Rule.ActionType, "sms", System.StringComparison.OrdinalIgnoreCase))
            {
                // For SMS action type, we don't need to set ActionValue as the native processor
                // will handle the tell message matching and email sending
                Rule.ActionValue = ""; // Empty value for SMS action type
            }
            DialogResult = true;
            Close();
        }

        private void RefreshSpellList()
        {
            if (SpellNameCombo == null || SpellClassCombo == null) return;
            var classChoice = (SpellClassCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Cleric";
            int.TryParse(MinLevelText?.Text ?? "1", out var minL);
            int.TryParse(MaxLevelText?.Text ?? "70", out var maxL);
            var minLevel = minL > 0 ? (int?)minL : null;
            var maxLevel = maxL > 0 ? (int?)maxL : null;
            
            // Get spells based on selected class
            var list = classChoice.ToLowerInvariant() switch
            {
                "shaman" => _spells.FilterByLevel(_spells.GetShamanSpells(), minLevel, maxLevel),
                "druid" => _spells.FilterByLevel(_spells.GetDruidSpells(), minLevel, maxLevel),
                "enchanter" => _spells.FilterByLevel(_spells.GetEnchanterSpells(), minLevel, maxLevel),
                "magician" => _spells.FilterByLevel(_spells.GetMagicianSpells(), minLevel, maxLevel),
                "necromancer" => _spells.FilterByLevel(_spells.GetNecromancerSpells(), minLevel, maxLevel),
                "ranger" => _spells.FilterByLevel(_spells.GetRangerSpells(), minLevel, maxLevel),
                "wizard" => _spells.FilterByLevel(_spells.GetWizardSpells(), minLevel, maxLevel),
                _ => _spells.FilterByLevel(_spells.GetClericSpells(), minLevel, maxLevel) // Default to Cleric
            };
            
            SpellNameCombo.ItemsSource = list;
            
            // If we have a pending spell selection, select it now
            if (!string.IsNullOrEmpty(_pendingSpellSelection))
            {
                var spell = list.FirstOrDefault(s => s.Name.Equals(_pendingSpellSelection, System.StringComparison.OrdinalIgnoreCase));
                if (spell != null)
                {
                    SpellNameCombo.SelectedItem = spell;
                }
                _pendingSpellSelection = null;
            }
            
            // Force refresh of the display
            SpellNameCombo.Items.Refresh();
        }
    }
}


