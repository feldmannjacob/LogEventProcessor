using System;
using System.Net;
using System.Net.Mail;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit;
using MimeKit;

namespace EmailMonitor
{
    public class EmailMonitor
    {
        private string _smtpServer;
        private int _smtpPort;
        private string _username;
        private string _password;
        private string _fromEmail;
        private string _toEmail;
        private bool _enableSsl;
        private int _checkIntervalMs;
        private bool _isRunning;
        private CancellationTokenSource _cancellationTokenSource;
        
        // IMAP settings for receiving emails
        private string _imapServer;
        private int _imapPort;
        private string _imapUsername;
        private string _imapPassword;
        private bool _imapEnableSsl;
        private HashSet<string> _processedEmailIds;
        private DateTime _startupTime;

        public event EventHandler<string> OnResponseReceived;

        public EmailMonitor(string smtpServer, int smtpPort, string username, string password, 
                           string fromEmail, string toEmail, bool enableSsl, int checkIntervalMs = 30000,
                           string imapServer = "imap.gmail.com", int imapPort = 993, 
                           string imapUsername = null, string imapPassword = null, bool imapEnableSsl = true)
        {
            _smtpServer = smtpServer;
            _smtpPort = smtpPort;
            _username = username;
            _password = password;
            _fromEmail = fromEmail;
            _toEmail = toEmail;
            _enableSsl = enableSsl;
            _checkIntervalMs = checkIntervalMs;
            
            // IMAP settings - use SMTP settings as defaults if not provided
            _imapServer = imapServer;
            _imapPort = imapPort;
            _imapUsername = imapUsername ?? username;
            _imapPassword = imapPassword ?? password;
            _imapEnableSsl = imapEnableSsl;
            _processedEmailIds = new HashSet<string>();
            _startupTime = DateTime.Now;
        }

        public async Task StartMonitoringAsync()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            // Clear any existing response file on startup to avoid processing old responses
            if (File.Exists("response.txt"))
            {
                File.Delete("response.txt");
                Console.WriteLine($"[EMAIL MONITOR] Cleared existing response file on startup");
            }

            Console.WriteLine($"[EMAIL MONITOR] Starting email monitoring for {_toEmail}");
            Console.WriteLine($"[EMAIL MONITOR] Monitoring emails received after {_startupTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"[EMAIL MONITOR] Checking every {_checkIntervalMs / 1000} seconds");
            Console.WriteLine($"[EMAIL MONITOR] Current working directory: {Directory.GetCurrentDirectory()}");
            Console.WriteLine($"[EMAIL MONITOR] Response file will be written to: {Path.GetFullPath("response.txt")}");

            try
            {
                while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await CheckForNewEmailsAsync();
                    await Task.Delay(_checkIntervalMs, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[EMAIL MONITOR] Monitoring stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL MONITOR] Error during monitoring: {ex.Message}");
            }
        }

        public void StopMonitoring()
        {
            _isRunning = false;
            _cancellationTokenSource?.Cancel();
            Console.WriteLine("[EMAIL MONITOR] Stopping email monitoring");
        }

        private async Task CheckForNewEmailsAsync()
        {
            try
            {
                Console.WriteLine("[EMAIL MONITOR] Checking for new emails via IMAP...");
                
                // Try IMAP first, fallback to file-based if IMAP fails
                bool imapSuccess = await CheckImapEmailsAsync();
                if (!imapSuccess)
                {
                    Console.WriteLine("[EMAIL MONITOR] IMAP failed, falling back to file-based monitoring...");
                    await CheckFileBasedResponsesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL MONITOR] Error checking emails: {ex.Message}");
                // Fallback to file-based system
                await CheckFileBasedResponsesAsync();
            }
        }

        private Task CheckFileBasedResponsesAsync()
        {
            // Check for response files in a responses directory
            string responsesDir = "responses";
            string responsesDirPath = Path.GetFullPath(responsesDir);
            Console.WriteLine($"[EMAIL MONITOR] Checking file-based responses in: {responsesDirPath}");
            
            if (!Directory.Exists(responsesDir))
            {
                Directory.CreateDirectory(responsesDir);
                Console.WriteLine($"[EMAIL MONITOR] Created responses directory: {responsesDirPath}");
                return Task.CompletedTask;
            }

            var responseFiles = Directory.GetFiles(responsesDir, "*.txt");
            Console.WriteLine($"[EMAIL MONITOR] Found {responseFiles.Length} response files to process");
            
            foreach (var file in responseFiles)
            {
                try
                {
                    Console.WriteLine($"[EMAIL MONITOR] Processing file: {Path.GetFullPath(file)}");
                    string content = File.ReadAllText(file);
                    string response = ExtractResponseFromEmail(content);
                    
                    if (!string.IsNullOrEmpty(response))
                    {
                        Console.WriteLine($"[EMAIL MONITOR] File-based response received: {response}");
                        OnResponseReceived?.Invoke(this, response);
                        
                        // Append to response.txt for the main application
                        string timestampedResponse = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|{response}";
                        string responseFilePath = Path.GetFullPath("response.txt");
                        Console.WriteLine($"[EMAIL MONITOR] Appending file-based response to: {responseFilePath}");
                        File.AppendAllText(responseFilePath, timestampedResponse + Environment.NewLine);
                        Console.WriteLine($"[EMAIL MONITOR] Successfully appended file-based response");
                    }

                    // Delete the processed file
                    File.Delete(file);
                    Console.WriteLine($"[EMAIL MONITOR] Deleted processed file: {file}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EMAIL MONITOR] Error processing response file {file}: {ex.Message}");
                }
            }
            return Task.CompletedTask;
        }

        private async Task<bool> CheckImapEmailsAsync()
        {
            try
            {
                using (var client = new ImapClient())
                {
                    // Connect to IMAP server
                    await client.ConnectAsync(_imapServer, _imapPort, _imapEnableSsl);
                    Console.WriteLine($"[EMAIL MONITOR] Connected to {_imapServer}:{_imapPort}");

                    // Authenticate
                    await client.AuthenticateAsync(_imapUsername, _imapPassword);
                    Console.WriteLine($"[EMAIL MONITOR] Authenticated as {_imapUsername}");

                    // Open inbox
                    var inbox = client.Inbox;
                    await inbox.OpenAsync(FolderAccess.ReadOnly);
                    Console.WriteLine($"[EMAIL MONITOR] Opened inbox, {inbox.Count} total messages");

                    // Search for unread emails received after startup time
                    var searchQuery = SearchQuery.And(
                        SearchQuery.DeliveredAfter(_startupTime),
                        SearchQuery.NotSeen
                    );

                    var uids = await inbox.SearchAsync(searchQuery);
                    Console.WriteLine($"[EMAIL MONITOR] Found {uids.Count} unread emails received after startup ({_startupTime:yyyy-MM-dd HH:mm:ss})");

                    // Process each unread email
                    foreach (var uid in uids)
                    {
                        if (_processedEmailIds.Contains(uid.ToString()))
                        {
                            continue; // Skip already processed emails
                        }

                        var message = await inbox.GetMessageAsync(uid);
                        Console.WriteLine($"[EMAIL MONITOR] Processing email from: {message.From}");

                        // Check if email is from the configured TO email address (where tell notifications are sent)
                        bool isFromExpectedSender = false;
                        if (message.From != null)
                        {
                            var fromAddress = message.From.ToString().ToLower();
                            var toEmailLower = _toEmail.ToLower();
                            
                            // Only process emails from the configured TO address (where tell notifications are sent)
                            isFromExpectedSender = fromAddress.Contains(toEmailLower) || 
                                                 fromAddress.Contains("16185801973"); // Keep the hardcoded one as backup
                            
                            Console.WriteLine($"[EMAIL MONITOR] Checking sender: {fromAddress} against TO email: {toEmailLower}");
                        }

                        if (isFromExpectedSender)
                        {
                            Console.WriteLine($"[EMAIL MONITOR] Processing email from expected sender: {message.From}");
                            Console.WriteLine($"[EMAIL MONITOR] Email subject: {message.Subject}");
                            Console.WriteLine($"[EMAIL MONITOR] Email date: {message.Date}");
                            Console.WriteLine($"[EMAIL MONITOR] Email received after startup: {message.Date > _startupTime}");
                            
                            // Only process emails received after our startup time
                            if (message.Date <= _startupTime)
                            {
                                Console.WriteLine($"[EMAIL MONITOR] Skipping email - received before startup time ({_startupTime:yyyy-MM-dd HH:mm:ss})");
                                _processedEmailIds.Add(uid.ToString());
                                continue;
                            }
                            
                            // Extract response from email content
                            string emailContent = GetEmailContent(message);
                            string response = ExtractResponseFromEmail(emailContent);
                            
                            if (!string.IsNullOrEmpty(response))
                            {
                                Console.WriteLine($"[EMAIL MONITOR] Response received from {_toEmail}: {response}");
                                OnResponseReceived?.Invoke(this, response);
                                
                                // Append response to file with timestamp for the main application
                                string timestampedResponse = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|{response}";
                                string responseFilePath = Path.GetFullPath("response.txt");
                                Console.WriteLine($"[EMAIL MONITOR] Appending IMAP response to: {responseFilePath}");
                                Console.WriteLine($"[EMAIL MONITOR] Response content: {timestampedResponse}");
                                File.AppendAllText(responseFilePath, timestampedResponse + Environment.NewLine);
                                Console.WriteLine($"[EMAIL MONITOR] Successfully appended {timestampedResponse.Length} characters to {responseFilePath}");
                            }
                            else
                            {
                                Console.WriteLine($"[EMAIL MONITOR] No response extracted from email content");
                            }
                        }

                        // Mark as processed
                        _processedEmailIds.Add(uid.ToString());
                    }

                    // Disconnect
                    await client.DisconnectAsync(true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL MONITOR] IMAP error: {ex.Message}");
                return false;
            }
        }

        private string GetEmailContent(MimeMessage message)
        {
            try
            {
                // Try to get text content
                if (message.TextBody != null)
                {
                    return message.TextBody;
                }

                // If no text body, try HTML body
                if (message.HtmlBody != null)
                {
                    // Simple HTML tag removal (in production, use HtmlAgilityPack)
                    return System.Text.RegularExpressions.Regex.Replace(message.HtmlBody, "<[^>]*>", "");
                }

                // Fallback to subject
                return message.Subject ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL MONITOR] Error extracting email content: {ex.Message}");
                return message.Subject ?? "";
            }
        }

        private string ExtractResponseFromEmail(string emailContent)
        {
            // Extract the actual response from email content
            // Look for patterns like "Response: command" or just the command itself
            var patterns = new[]
            {
                @"Response:\s*(.+)",
                @"Command:\s*(.+)",
                @"^(.+)$" // If it's just a single line, treat it as the response
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(emailContent, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string response = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(response) && !response.StartsWith("From:") && !response.StartsWith("To:"))
                    {
                        return response;
                    }
                }
            }

            return null;
        }

        private static string FindConfigFile()
        {
            // Get the directory where the executable is located
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string exeDir = Path.GetDirectoryName(exePath);
            
            // First, try portable config next to executable
            string portable = Path.Combine(exeDir, "config.yaml");
            if (File.Exists(portable))
            {
                return portable;
            }
            
            // Try common CWD-based locations
            string[] candidates = {
                "config/config.yaml",
                "LogEventProcessor/config.yaml", 
                "config.yaml"
            };
            
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            
            // Try nearby to the exe (repo layouts)
            string[] exeCandidates = {
                Path.Combine(exeDir, "..", "config", "config.yaml"),
                Path.Combine(exeDir, "..", "LogEventProcessor", "config.yaml")
            };
            
            foreach (string candidate in exeCandidates)
            {
                string fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            
            // Final fallback: default to portable path next to exe
            return portable;
        }

        public static EmailMonitor LoadFromConfig(string configPath)
        {
            try
            {
                // Use the same config path resolution logic as the main LogEventProcessor
                if (string.IsNullOrEmpty(configPath))
                {
                    configPath = FindConfigFile();
                }
                else
                {
                    // Handle relative paths by making them relative to the current working directory
                    if (!Path.IsPathRooted(configPath))
                    {
                        configPath = Path.GetFullPath(configPath);
                    }
                }
                
                if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                {
                    Console.WriteLine($"Config file not found: {configPath}");
                    return null;
                }
                
                Console.WriteLine($"Loading email config from: {Path.GetFullPath(configPath)}");
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
                    
                    // IMAP settings - use SMTP settings as defaults if not specified
                    else if (line.StartsWith("email_imap_server:"))
                        config.ImapServer = line.Split(new char[] { ':' }, 2)[1].Trim();
                    else if (line.StartsWith("email_imap_port:"))
                    {
                        int port;
                        if (int.TryParse(line.Split(new char[] { ':' }, 2)[1].Trim(), out port))
                            config.ImapPort = port;
                    }
                    else if (line.StartsWith("email_imap_username:"))
                        config.ImapUsername = line.Split(new char[] { ':' }, 2)[1].Trim();
                    else if (line.StartsWith("email_imap_password:"))
                        config.ImapPassword = line.Split(new char[] { ':' }, 2)[1].Trim();
                    else if (line.StartsWith("email_imap_enable_ssl:"))
                        config.ImapEnableSsl = line.Split(new char[] { ':' }, 2)[1].Trim().ToLower() == "true";
                }

                // Use SMTP credentials as defaults for IMAP if IMAP credentials are not specified
                string imapUsername = !string.IsNullOrEmpty(config.ImapUsername) ? config.ImapUsername : config.Username;
                string imapPassword = !string.IsNullOrEmpty(config.ImapPassword) ? config.ImapPassword : config.Password;
                
                return new EmailMonitor(
                    config.SmtpServer, config.SmtpPort, config.Username, config.Password,
                    config.From, config.To, config.EnableSsl, 30000, // Check every 30 seconds
                    config.ImapServer, config.ImapPort, imapUsername, imapPassword, config.ImapEnableSsl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading email config: {ex.Message}");
                return null;
            }
        }
    }

    public class EmailConfig
    {
        public string SmtpServer { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public bool EnableSsl { get; set; } = true;
        
        // IMAP settings for receiving emails
        public string ImapServer { get; set; } = "imap.gmail.com";
        public int ImapPort { get; set; } = 993;
        public bool ImapEnableSsl { get; set; } = true;
        public string ImapUsername { get; set; } = "";
        public string ImapPassword { get; set; } = "";
    }
}
