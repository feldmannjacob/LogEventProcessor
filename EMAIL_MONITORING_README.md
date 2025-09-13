# Email Monitoring Feature

This feature allows you to monitor incoming emails and automatically send responses as commands to EverQuest.

## How It Works

1. **Tell Message Detection**: When someone sends you a tell in EverQuest, it triggers an email notification
2. **Email Monitoring**: The EmailMonitor service checks for incoming emails periodically
3. **Response Processing**: When you reply to the email, the response is extracted and sent as a command
4. **Command Execution**: The response is automatically typed and sent in EverQuest

## Setup

### 1. Configure Email Settings
Make sure your `config.yaml` has the correct email settings:
```yaml
email_smtp_server: smtp.gmail.com
email_smtp_port: 587
email_username: your-email@gmail.com
email_password: your-app-password
email_from: your-email@gmail.com
email_to: recipient@gmail.com
email_enable_ssl: true
```

### 2. Build EmailMonitor
```bash
cd tools/EmailMonitor
dotnet build -c Release
```

### 3. Copy EmailMonitor.exe
Copy the built `EmailMonitor.exe` to your main directory alongside `LogEventProcessor.exe`.

### 4. Start the System
Use the provided batch file:
```bash
start_with_email_monitor.bat
```

Or start manually:
1. Start EmailMonitor: `EmailMonitor.exe config.yaml`
2. Start LogEventProcessor: `LogEventProcessor.exe`

## Usage

### Sending Tell Messages
When someone sends you a tell in EverQuest, you'll receive an email notification at your configured email address.

### Responding via Email
Reply to the email with your response. The EmailMonitor will:
- Extract the response from your email
- Send it as a command in EverQuest
- Process it automatically

### Response Format
The EmailMonitor looks for responses in these formats:
- `Response: your command here`
- `Command: your command here`
- Or just the command itself on a single line

## Testing

To test the system:
1. Create a file called `response.txt` in the main directory
2. Add your test command: `echo "Response: /g Hello from email!" > response.txt`
3. The system will process it and send the command to EverQuest

## File Structure

- `EmailMonitor.exe` - Monitors emails and creates response files
- `response.txt` - Temporary file containing the latest email response
- `responses/` - Directory for processing email responses (if using file-based system)

## Troubleshooting

- Make sure EmailMonitor.exe is in the same directory as LogEventProcessor.exe
- Check that your email credentials are correct
- Verify that the response.txt file is being created when you reply to emails
- Check the console output for error messages
