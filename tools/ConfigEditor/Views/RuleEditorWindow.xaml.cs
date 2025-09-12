using System.Windows;
using ConfigEditor.Models;

namespace ConfigEditor.Views
{
    public partial class RuleEditorWindow : Window
    {
        public RegexRule Rule { get; }
        private bool _initialized;

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
            DialogResult = true;
            Close();
        }
    }
}


