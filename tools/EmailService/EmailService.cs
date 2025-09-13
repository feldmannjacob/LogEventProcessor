using System;
using System.Net;
using System.Net.Mail;
using System.IO;

namespace EmailService
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: EmailService.exe <config_path> <message>");
                return 1;
            }

            string configPath = args[0];
            string message = args[1];

            try
            {
                // Load configuration from YAML file
                var config = LoadConfig(configPath);
                
                // Send email
                SendEmail(config, message);
                
                Console.WriteLine("Email sent successfully");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        static EmailConfig LoadConfig(string configPath)
        {
            // Simple YAML parsing for email configuration
            var lines = File.ReadAllLines(configPath);
            var config = new EmailConfig();
            
            foreach (var line in lines)
            {
                if (line.StartsWith("email_smtp_server:"))
                    config.SmtpServer = line.Split(new char[] { ':' }, 2)[1].Trim();
                else if (line.StartsWith("email_smtp_port:"))
                {
                    int port;
                    if (int.TryParse(line.Split(new char[] { ':' }, 2)[1].Trim(), out port))
                        config.SmtpPort = port;
                }
                else if (line.StartsWith("email_username:"))
                    config.Username = line.Split(new char[] { ':' }, 2)[1].Trim();
                else if (line.StartsWith("email_password:"))
                    config.Password = line.Split(new char[] { ':' }, 2)[1].Trim();
                else if (line.StartsWith("email_from:"))
                    config.From = line.Split(new char[] { ':' }, 2)[1].Trim();
                else if (line.StartsWith("email_to:"))
                    config.To = line.Split(new char[] { ':' }, 2)[1].Trim();
                else if (line.StartsWith("email_enable_ssl:"))
                    config.EnableSsl = line.Split(new char[] { ':' }, 2)[1].Trim().ToLower() == "true";
            }
            
            return config;
        }

        static void SendEmail(EmailConfig config, string message)
        {
            Console.WriteLine($"Email configuration loaded:");
            Console.WriteLine($"  SMTP Server: {config.SmtpServer}:{config.SmtpPort}");
            Console.WriteLine($"  From: {config.From}");
            Console.WriteLine($"  To: {config.To}");
            Console.WriteLine($"  Username: {config.Username}");
            Console.WriteLine($"  Enable SSL: {config.EnableSsl}");
            Console.WriteLine($"  Message: {message}");
            
            using (var client = new SmtpClient(config.SmtpServer, config.SmtpPort))
            {
                client.EnableSsl = config.EnableSsl;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(config.Username, config.Password);
                client.DeliveryMethod = SmtpDeliveryMethod.Network;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(config.From),
                    Subject = "EQ Tell Message",
                    Body = message,
                    IsBodyHtml = false
                };

                mailMessage.To.Add(config.To);

                client.Send(mailMessage);
            }
            
            Console.WriteLine("Email sent successfully");
        }
    }

    class EmailConfig
    {
        public string SmtpServer { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public bool EnableSsl { get; set; } = true;
    }
}
