#include <iostream>
#include <string>
#include <memory>
#include <csignal>
#include <atomic>
#include "ConfigManager.h"
#include "LogReader.h"
#include "EventProcessor.h"
#include "ThreadSafeQueue.h"
#include "LogEvent.h"
#include "RegexMatcher.h"
#include "ActionManager.h"

// Global flag for graceful shutdown
std::atomic<bool> g_running(true);

// Signal handler for graceful shutdown
void signalHandler(int signal) {
    std::cout << "\nReceived signal " << signal << ". Shutting down gracefully..." << std::endl;
    g_running = false;
}

// Global instances
std::unique_ptr<RegexMatcher> g_regexMatcher;
std::unique_ptr<ActionManager> g_actionManager;

// Custom event handler for processing log events
void customEventHandler(const LogEventPtr& event) {
    if (!event) {
        return;
    }
    
    // First, try action manager (which includes regex matching and action execution)
    if (g_actionManager && g_actionManager->processEvent(event)) {
        // Action manager handled the event and executed actions
        return;
    }
    
    // Fallback to simple text matching for non-regex events
    const std::string& data = event->data;
    
    if (data.find("ERROR") != std::string::npos) {
        std::cout << "[ERROR] Line " << event->lineNumber << ": " << data << std::endl;
    } else if (data.find("WARNING") != std::string::npos) {
        std::cout << "[WARNING] Line " << event->lineNumber << ": " << data << std::endl;
    } else if (data.find("INFO") != std::string::npos) {
        std::cout << "[INFO] Line " << event->lineNumber << ": " << data << std::endl;
    } else {
        std::cout << "[LOG] Line " << event->lineNumber << ": " << data << std::endl;
    }
}

int main(int argc, char* argv[]) {
    // Set up signal handlers
    std::signal(SIGINT, signalHandler);
    std::signal(SIGTERM, signalHandler);
    
    std::cout << "=== Log Event Processor ===" << std::endl;
    std::cout << "A multi-threaded log file monitoring application" << std::endl;
    std::cout << "Press Ctrl+C to exit gracefully" << std::endl << std::endl;
    
    // Determine config file path: prefer ./config/config.yaml, then LogEventProcessor/config.yaml, then ./config.yaml, unless overridden by argv
    std::string configPath;
    if (argc > 1) {
        configPath = argv[1];
    } else {
        // Probe common locations
        const char* candidates[] = {
            "config/config.yaml",
            "LogEventProcessor/config.yaml",
            "config.yaml"
        };
        for (const char* c : candidates) {
            std::ifstream f(c);
            if (f.good()) { configPath = c; break; }
        }
        if (configPath.empty()) {
            configPath = "config.yaml"; // fallback
        }
    }
    
    // Load configuration
    ConfigManager config;
    if (!config.loadConfig(configPath)) {
        std::cerr << "Failed to load configuration. Using default settings." << std::endl;
    }
    
    // Get configuration values
    std::string logFilePath = config.getLogFilePath();
    std::string outputDir = config.getOutputDirectory();
    int pollingInterval = config.getPollingInterval();
    
    std::cout << "Configuration:" << std::endl;
    std::cout << "  Log file: " << logFilePath << std::endl;
    std::cout << "  Output directory: " << outputDir << std::endl;
    std::cout << "  Polling interval: " << pollingInterval << "ms" << std::endl;
    
    // Initialize regex matcher and action manager
    g_regexMatcher = std::make_unique<RegexMatcher>();
    g_actionManager = std::make_unique<ActionManager>();
    
    if (g_actionManager->initialize()) {
        g_actionManager->setRegexMatcher(g_regexMatcher.get());
        
        // Load regex rules and actions from configuration
        if (config.loadRegexRulesAndActions(*g_regexMatcher, *g_actionManager)) {
            std::cout << "  Regex rules: " << g_regexMatcher->getRuleCount() << " loaded" << std::endl;
            std::cout << "  Action mappings: " << g_actionManager->getMappingCount() << " loaded" << std::endl;
        } else {
            std::cout << "  Configuration: Failed to load regex rules and actions" << std::endl;
        }
    } else {
        std::cout << "  Action manager: Failed to initialize" << std::endl;
    }
    std::cout << std::endl;
    
    // Create the thread-safe queue
    ThreadSafeQueue<LogEventPtr> eventQueue;
    
    // Create log reader and event processor
    LogReader logReader(logFilePath, eventQueue);
    EventProcessor eventProcessor(eventQueue);
    
    // Set custom event handler (used in non-parallel mode)
    eventProcessor.setEventHandler(customEventHandler);
    
    try {
        // Start the components
        logReader.start();
        // Enable parallel regex matching with ordered execution based on config
        bool parallelProcessing = config.getBool("parallel_processing", false);
        size_t workerCount = std::thread::hardware_concurrency() ? std::thread::hardware_concurrency() : 4;
        if (parallelProcessing) {
            eventProcessor.enableParallelProcessing(true, workerCount);
            eventProcessor.setActionManager(g_actionManager.get());
        } else {
            eventProcessor.enableParallelProcessing(false, 0);
        }
        // Start processor (works for both modes)
        eventProcessor.start();
        
        std::cout << "Application started successfully!" << std::endl;
        std::cout << "Monitoring log file for new events..." << std::endl << std::endl;
        
        // Main loop - wait for shutdown signal
        while (g_running.load()) {
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            
            // Print status every 10 seconds
            static auto lastStatusTime = std::chrono::steady_clock::now();
            auto now = std::chrono::steady_clock::now();
            if (std::chrono::duration_cast<std::chrono::seconds>(now - lastStatusTime).count() >= 10) {
                std::cout << "Status: Line " << logReader.getCurrentLineNumber() 
                         << ", Processed " << eventProcessor.getProcessedEventCount() 
                         << " events, Queue size: " << eventQueue.size();
                if (g_regexMatcher) {
                    std::cout << ", Regex matches: " << g_regexMatcher->getMatchCount();
                }
                if (g_actionManager) {
                    std::cout << ", Actions executed: " << g_actionManager->getExecutedActionCount()
                             << ", Failed: " << g_actionManager->getFailedActionCount();
                }
                std::cout << std::endl;
                lastStatusTime = now;
            }
        }
        
    } catch (const std::exception& e) {
        std::cerr << "Error: " << e.what() << std::endl;
        return 1;
    }
    
    // Graceful shutdown
    std::cout << "Shutting down components..." << std::endl;
    
    // Stop the log reader first to prevent new events
    logReader.stop();
    
    // Process remaining events in the queue
    std::cout << "Processing remaining events in queue..." << std::endl;
    std::this_thread::sleep_for(std::chrono::milliseconds(1000));
    
    // Stop the event processor
    eventProcessor.stop();
    
    std::cout << "Application shutdown complete." << std::endl;
    return 0;
}
