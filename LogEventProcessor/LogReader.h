#pragma once

#include <string>
#include <thread>
#include <atomic>
#include <memory>
#include "ThreadSafeQueue.h"
#include "LogEvent.h"

/**
 * @class LogReader
 * @brief Reads log file and generates events for processing
 */
class LogReader {
public:
    LogReader(const std::string& logFilePath, ThreadSafeQueue<LogEventPtr>& eventQueue);
    ~LogReader();
    
    /**
     * @brief Start reading the log file
     */
    void start();
    
    /**
     * @brief Stop reading the log file
     */
    void stop();
    
    /**
     * @brief Check if reader is running
     * @return true if running, false otherwise
     */
    bool isRunning() const { return _isRunning.load(); }
    
    /**
     * @brief Get the current line number being processed
     * @return Current line number
     */
    size_t getCurrentLineNumber() const { return _currentLineNumber.load(); }
    
    /**
     * @brief Check if file exists and is readable
     * @return true if file is accessible, false otherwise
     */
    bool isFileAccessible() const;

private:
    std::string _logFilePath;
    ThreadSafeQueue<LogEventPtr>& _eventQueue;
    std::thread _readerThread;
    std::atomic<bool> _isRunning;
    std::atomic<bool> _shouldStop;
    std::atomic<size_t> _currentLineNumber;
    
    /**
     * @brief Main reading loop
     */
    void readLoop();
    
    /**
     * @brief Read new lines from the log file
     * @param lastPosition Last read position in the file
     * @param hadNewEvents Output parameter indicating if new events were read
     * @return New position in the file
     */
    std::streampos readNewLines(std::streampos lastPosition, bool& hadNewEvents);
};
