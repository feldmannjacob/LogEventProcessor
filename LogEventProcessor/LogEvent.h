#pragma once

#include <string>
#include <chrono>
#include <memory>

/**
 * @struct LogEvent
 * @brief Represents a single log event with timestamp and data
 */
struct LogEvent {
    std::string data;
    std::chrono::system_clock::time_point timestamp;
    size_t lineNumber;
    
    LogEvent(const std::string& logData, size_t lineNum) 
        : data(logData), lineNumber(lineNum), timestamp(std::chrono::system_clock::now()) {}
    
    LogEvent() : lineNumber(0), timestamp(std::chrono::system_clock::now()) {}
};

using LogEventPtr = std::shared_ptr<LogEvent>;
