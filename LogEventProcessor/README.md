# Log Event Processor

A multi-threaded C++ console application that monitors log files and processes events in real-time using a thread-safe queue system.

## Features

- **Real-time Log Monitoring**: Continuously monitors log files for new entries
- **Thread-Safe Event Processing**: Uses a thread-safe queue to ensure proper event ordering
- **YAML Configuration**: Configurable via YAML file for easy customization
- **Multi-threaded Architecture**: Separate threads for log reading and event processing
- **Custom Event Handlers**: Pluggable event processing system
- **Graceful Shutdown**: Proper cleanup and signal handling

## Architecture

The application consists of several key components:

### Core Components

1. **LogReader**: Monitors log files and generates events
2. **EventProcessor**: Consumes events from the queue and processes them
3. **ThreadSafeQueue**: Ensures thread-safe event passing between components
4. **ConfigManager**: Handles YAML configuration file parsing
5. **LogEvent**: Represents a single log event with timestamp and metadata

### Threading Model

- **Producer Thread**: LogReader continuously monitors the log file
- **Consumer Thread**: EventProcessor processes events from the queue
- **Main Thread**: Handles user input and coordinates shutdown

## Configuration

The application uses a YAML configuration file (`config.yaml`) with the following options:

```yaml
# Path to the log file to monitor
log_file_path: "application.log"

# Output directory for processed events (future use)
output_directory: "./output"

# Polling interval in milliseconds
polling_interval_ms: 1000

# Enable debug mode
debug_mode: true

# Maximum queue size (0 = unlimited)
max_queue_size: 1000

# Event processing settings
process_errors: true
process_warnings: true
process_info: true
```

## Building

### Prerequisites

- Visual Studio 2022 or later
- Windows 10/11
- C++17 compatible compiler

### Build Instructions

1. Open the solution file `LogEventProcessor.sln` in Visual Studio
2. Select Debug or Release configuration
3. Build the solution (Ctrl+Shift+B)

Or build from command line:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" LogEventProcessor.sln /p:Configuration=Debug /p:Platform=x64
```

## Usage

### Basic Usage

```bash
# Run with default configuration
LogEventProcessor.exe

# Run with custom configuration file
LogEventProcessor.exe custom_config.yaml
```

### Example Output

```
=== Log Event Processor ===
A multi-threaded log file monitoring application
Press Ctrl+C to exit gracefully

Configuration loaded successfully from: config.yaml
Configuration:
  Log file: application.log
  Output directory: ./output
  Polling interval: 1000ms

LogReader started monitoring: application.log
EventProcessor started.
Application started successfully!

[2024-01-01 10:00:00.123] Line 1: 2024-01-01 10:00:00 [INFO] Application started
[2024-01-01 10:00:01.456] Line 2: 2024-01-01 10:00:01 [INFO] Loading configuration
[2024-01-01 10:00:02.789] Line 3: 2024-01-01 10:00:02 [WARNING] Configuration file not found, using defaults
```

## Customization

### Custom Event Handlers

You can customize event processing by modifying the `customEventHandler` function in `main.cpp`:

```cpp
void customEventHandler(const LogEventPtr& event) {
    if (!event) return;
    
    const std::string& data = event->data;
    
    if (data.find("ERROR") != std::string::npos) {
        // Handle error events
        std::cout << "[ERROR] " << event->data << std::endl;
    } else if (data.find("WARNING") != std::string::npos) {
        // Handle warning events
        std::cout << "[WARNING] " << event->data << std::endl;
    }
    // Add more custom logic here
}
```

### Adding New Configuration Options

1. Add the option to `config.yaml`
2. Add a getter method to `ConfigManager.h`
3. Implement the getter in `ConfigManager.cpp`
4. Use the configuration value in your code

## Thread Safety

The application ensures thread safety through:

- **ThreadSafeQueue**: Uses mutex and condition variables for safe concurrent access
- **Atomic Variables**: For status flags and counters
- **Proper Synchronization**: Producer-consumer pattern with proper signaling

## Error Handling

- File access errors are logged and handled gracefully
- Configuration parsing errors fall back to default values
- Thread synchronization errors are caught and reported
- Graceful shutdown on Ctrl+C or termination signals

## Performance Considerations

- **Memory Usage**: Events are stored as shared pointers to minimize copying
- **Queue Size**: Configurable maximum queue size to prevent memory issues
- **Polling Interval**: Adjustable polling frequency to balance responsiveness and CPU usage
- **Thread Efficiency**: Uses condition variables to avoid busy waiting

## Future Enhancements

- Database integration for event storage
- Web interface for monitoring
- Multiple log file support
- Event filtering and routing
- Metrics and statistics collection
- Plugin system for custom processors

## License

This project is provided as-is for educational and development purposes.
