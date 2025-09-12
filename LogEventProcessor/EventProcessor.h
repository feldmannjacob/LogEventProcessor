#pragma once

#include <thread>
#include <atomic>
#include <memory>
#include <functional>
#include <vector>
#include <map>
#include "ThreadSafeQueue.h"
#include "LogEvent.h"

// Forward declarations
struct ActionMapping;
class ActionManager;

/**
 * @class EventProcessor
 * @brief Processes log events from the queue
 */
class EventProcessor {
public:
    using EventHandler = std::function<void(const LogEventPtr&)>;
    
    EventProcessor(ThreadSafeQueue<LogEventPtr>& eventQueue);
    ~EventProcessor();
    
    /**
     * @brief Start processing events
     */
    void start();
    
    /**
     * @brief Stop processing events
     */
    void stop();
    
    /**
     * @brief Check if processor is running
     * @return true if running, false otherwise
     */
    bool isRunning() const { return _isRunning.load(); }
    
    /**
     * @brief Set the event handler function
     * @param handler Function to call for each event
     */
    void setEventHandler(EventHandler handler);

    // Provide ActionManager for parallel pipeline
    void setActionManager(ActionManager* manager);
    
    /**
     * @brief Get the number of processed events
     * @return Number of events processed
     */
    size_t getProcessedEventCount() const { return _processedEventCount.load(); }

    // Parallel matching API
    void enableParallelProcessing(bool enabled, size_t workerCount = 4);

private:
    ThreadSafeQueue<LogEventPtr>& _eventQueue;
    std::thread _processorThread;
    std::atomic<bool> _isRunning;
    std::atomic<bool> _shouldStop;
    std::atomic<size_t> _processedEventCount;
    EventHandler _eventHandler;

    // Parallel pipeline members
    bool _parallelEnabled = false;
    size_t _workerCount = 4;
    std::vector<std::thread> _workers;
    struct MatchTask { size_t seq; LogEventPtr event; };
    ThreadSafeQueue<MatchTask> _matchQueue{ };
    struct MatchResult { size_t seq; std::vector<ActionMapping> actions; };
    ThreadSafeQueue<MatchResult> _resultQueue{ };
    std::atomic<size_t> _nextSequenceToExecute{1};
    std::mutex _pendingMutex;
    std::map<size_t, std::vector<ActionMapping>> _pending;
    ActionManager* _actionManagerRef = nullptr; // set externally
    void workerLoop();
    void resultDispatcherLoop();
    
    /**
     * @brief Main processing loop
     */
    void processLoop();
    
    /**
     * @brief Default event handler - prints event to console
     * @param event The event to process
     */
    void defaultEventHandler(const LogEventPtr& event);
};
