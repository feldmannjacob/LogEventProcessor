#include "LogReader.h"
#include <fstream>
#include <iostream>
#include <chrono>
#include <thread>

LogReader::LogReader(const std::string& logFilePath, ThreadSafeQueue<LogEventPtr>& eventQueue)
    : _logFilePath(logFilePath), _eventQueue(eventQueue), _isRunning(false), _shouldStop(false), _currentLineNumber(0) {
}

LogReader::~LogReader() {
    stop();
}

void LogReader::start() {
    if (_isRunning.load()) {
        std::cout << "LogReader is already running." << std::endl;
        return;
    }
    
    if (!isFileAccessible()) {
        std::cerr << "Error: Cannot access log file: " << _logFilePath << std::endl;
        return;
    }
    
    _shouldStop = false;
    _isRunning = true;
    _readerThread = std::thread(&LogReader::readLoop, this);
    
    std::cout << "LogReader started monitoring: " << _logFilePath << std::endl;
}

void LogReader::stop() {
    if (!_isRunning.load()) {
        return;
    }
    
    _shouldStop = true;
    _eventQueue.stop(); // Wake up any waiting threads
    
    if (_readerThread.joinable()) {
        _readerThread.join();
    }
    
    _isRunning = false;
    std::cout << "LogReader stopped." << std::endl;
}

void LogReader::readLoop() {
    std::ifstream file(_logFilePath);
    if (!file.is_open()) {
        std::cerr << "Error: Could not open log file for reading: " << _logFilePath << std::endl;
        _isRunning = false;
        return;
    }
    
    // Start from the end of the file to monitor only new lines
    file.seekg(0, std::ios::end);
    std::streampos lastPosition = file.tellg();
    file.close();
    
    std::cout << "Monitoring for new lines in: " << _logFilePath << std::endl;
    
    while (!_shouldStop.load()) {
        bool hadNewEvents = false;
        std::streampos newPosition = readNewLines(lastPosition, hadNewEvents);
        
        if (hadNewEvents) {
            // New events were read, update position and continue immediately
            lastPosition = newPosition;
        } else {
            // No new events, sleep briefly before checking again
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
        }
    }
}

std::streampos LogReader::readNewLines(std::streampos lastPosition, bool& hadNewEvents) {
    hadNewEvents = false;
    std::ifstream file(_logFilePath);
    if (!file.is_open()) {
        return lastPosition;
    }
    
    // Check if file has grown
    file.seekg(0, std::ios::end);
    std::streampos currentSize = file.tellg();
    
    if (currentSize <= lastPosition) {
        // No new content
        file.close();
        return lastPosition;
    }
    
    // Read ALL new content in one burst
    file.seekg(lastPosition);
    std::string line;
    int eventsRead = 0;
    
    // Read all available new lines at once
    while (std::getline(file, line) && !_shouldStop.load()) {
        if (!line.empty()) {
            auto event = std::make_shared<LogEvent>(line, _currentLineNumber.load() + 1);
            _eventQueue.push(event);
            _currentLineNumber.fetch_add(1);
            eventsRead++;
            hadNewEvents = true;
        }
    }
    
    if (eventsRead > 0) {
        std::cout << "BURST: Read " << eventsRead << " new events in one go (total: " << _currentLineNumber.load() << ")" << std::endl;
    }
    
    // After reading to EOF, tellg() may return -1. Fall back to end-of-file size.
    std::streampos newPosition = file.tellg();
    if (newPosition == std::streampos(-1)) {
        file.clear();
        file.seekg(0, std::ios::end);
        newPosition = file.tellg();
    }
    file.close();
    return newPosition;
}

bool LogReader::isFileAccessible() const {
    std::ifstream file(_logFilePath);
    return file.good();
}
