using System.Windows;
using System;
using System.Threading.Tasks;

namespace ConfigEditor
{
    public partial class App : Application
    {
        public App()
        {
            this.DispatcherUnhandledException += (s, e) =>
            {
                MessageBox.Show(e.Exception.Message, "Unhandled error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                MessageBox.Show(ex != null ? ex.Message : "Unknown error", "Fatal error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                MessageBox.Show(e.Exception?.Message ?? "Task error", "Task error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.SetObserved();
            };
        }
    }
}


