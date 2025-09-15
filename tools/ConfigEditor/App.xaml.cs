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
                var errorMessage = $"Unhandled error: {e.Exception.Message}\n\nStack trace:\n{e.Exception.StackTrace}";
                MessageBox.Show(errorMessage, "Unhandled error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                var errorMessage = ex != null ? $"Fatal error: {ex.Message}\n\nStack trace:\n{ex.StackTrace}" : "Unknown fatal error";
                MessageBox.Show(errorMessage, "Fatal error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                var errorMessage = e.Exception != null ? $"Task error: {e.Exception.Message}\n\nStack trace:\n{e.Exception.StackTrace}" : "Unknown task error";
                MessageBox.Show(errorMessage, "Task error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.SetObserved();
            };
        }
    }
}


