using System.Windows;
using ConfigEditor.Models;

namespace ConfigEditor.Views
{
    public partial class RuleEditorWindow : Window
    {
        public RegexRule Rule { get; }

        public RuleEditorWindow(RegexRule rule)
        {
            InitializeComponent();
            Rule = rule;
            DataContext = Rule;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}


