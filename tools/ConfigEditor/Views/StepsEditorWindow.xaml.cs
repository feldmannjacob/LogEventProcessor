using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ConfigEditor.Models;

namespace ConfigEditor.Views
{
    public partial class StepsEditorWindow : Window
    {
        private readonly RegexRule _rule;
        public ObservableCollection<ActionStep> Steps { get; } = new ObservableCollection<ActionStep>();

        public StepsEditorWindow(RegexRule rule)
        {
            InitializeComponent();
            _rule = rule;
            if (_rule.Actions != null)
            {
                foreach (var s in _rule.Actions)
                    Steps.Add(s);
            }
            StepsGrid.ItemsSource = Steps;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            Steps.Add(new ActionStep { Enabled = true, Type = "keystroke", Value = "f1", DelayMs = 0, Modifiers = 0 });
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var sel = StepsGrid.SelectedItems.Cast<ActionStep>().ToList();
            foreach (var s in sel)
                Steps.Remove(s);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            _rule.Actions = Steps.ToList();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}


