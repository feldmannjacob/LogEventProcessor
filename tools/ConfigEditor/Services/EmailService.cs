using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using ConfigEditor.Models;

namespace ConfigEditor.Services
{
    public class EmailService
    {
        public async Task<bool> SendTellMessageAsync(ConfigRoot config, string tellMessage)
        {
            if (string.IsNullOrWhiteSpace(config.EmailSmtpServer) ||
                string.IsNullOrWhiteSpace(config.EmailFrom) ||
                string.IsNullOrWhiteSpace(config.EmailTo))
            {
                return false;
            }

            try
            {
                using var client = new SmtpClient(config.EmailSmtpServer, config.EmailSmtpPort);
                client.EnableSsl = config.EmailEnableSsl;
                
                if (!string.IsNullOrWhiteSpace(config.EmailUsername) && !string.IsNullOrWhiteSpace(config.EmailPassword))
                {
                    client.Credentials = new NetworkCredential(config.EmailUsername, config.EmailPassword);
                }

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(config.EmailFrom),
                    Subject = "EQ Tell Message",
                    Body = $"Tell message received:\n\n{tellMessage}",
                    IsBodyHtml = false
                };

                mailMessage.To.Add(config.EmailTo);

                await client.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EmailService: Failed to send email: {ex.Message}");
                return false;
            }
        }

        public bool SendTellMessage(ConfigRoot config, string tellMessage)
        {
            if (string.IsNullOrWhiteSpace(config.EmailSmtpServer) ||
                string.IsNullOrWhiteSpace(config.EmailFrom) ||
                string.IsNullOrWhiteSpace(config.EmailTo))
            {
                return false;
            }

            try
            {
                using var client = new SmtpClient(config.EmailSmtpServer, config.EmailSmtpPort);
                client.EnableSsl = config.EmailEnableSsl;
                
                if (!string.IsNullOrWhiteSpace(config.EmailUsername) && !string.IsNullOrWhiteSpace(config.EmailPassword))
                {
                    client.Credentials = new NetworkCredential(config.EmailUsername, config.EmailPassword);
                }

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(config.EmailFrom),
                    Subject = "EQ Tell Message",
                    Body = $"Tell message received:\n\n{tellMessage}",
                    IsBodyHtml = false
                };

                mailMessage.To.Add(config.EmailTo);

                client.Send(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EmailService: Failed to send email: {ex.Message}");
                return false;
            }
        }
    }
}
