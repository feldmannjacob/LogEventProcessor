using System;
using System.IO;
using System.Threading.Tasks;

namespace EmailMonitor
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string configPath = args.Length > 0 ? args[0] : null;
            
            if (!string.IsNullOrEmpty(configPath))
            {
                Console.WriteLine($"Loading config from: {Path.GetFullPath(configPath)}");
            }
            else
            {
                Console.WriteLine("Auto-discovering config file...");
            }
            
            var monitor = EmailMonitor.LoadFromConfig(configPath);

            if (monitor == null)
            {
                Console.WriteLine("Failed to load email configuration");
                return;
            }

            // Set up event handler for responses
            monitor.OnResponseReceived += (sender, response) =>
            {
                Console.WriteLine($"[RESPONSE] {response}");
                // The EmailMonitor class already writes the response with timestamp to response.txt
                // No additional processing needed here
            };

            Console.WriteLine("Email Monitor started. Press Ctrl+C to stop.");
            
            // Handle Ctrl+C gracefully
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                monitor.StopMonitoring();
            };

            await monitor.StartMonitoringAsync();
        }
    }
}
