#include "EventProcessor.h"
#include <iostream>
#include <iomanip>
#include <chrono>
#include "ActionManager.h"

EventProcessor::EventProcessor(ThreadSafeQueue<LogEventPtr>& eventQueue)
    : _eventQueue(eventQueue), _isRunning(false), _shouldStop(false), _processedEventCount(0) {
    // Set default event handler
    _eventHandler = [this](const LogEventPtr& event) {
        defaultEventHandler(event);
    };
}

EventProcessor::~EventProcessor() {
    stop();
}

void EventProcessor::start() {
    if (_isRunning.load()) {
        std::cout << "EventProcessor is already running." << std::endl;
        return;
    }
    
    _shouldStop = false;
    _isRunning = true;
    _processorThread = std::thread(&EventProcessor::processLoop, this);
    
    std::cout << "EventProcessor started." << std::endl;
}

void EventProcessor::stop() {
    if (!_isRunning.load()) {
        return;
    }
    
    _shouldStop = true;
    _eventQueue.stop(); // Wake up any waiting threads
    
    if (_processorThread.joinable()) {
        _processorThread.join();
    }
    
    _isRunning = false;
    std::cout << "EventProcessor stopped. Processed " << _processedEventCount.load() << " events." << std::endl;
}

void EventProcessor::setEventHandler(EventHandler handler) {
    _eventHandler = handler;
}

void EventProcessor::setActionManager(ActionManager* manager) {
    _actionManagerRef = manager;
}

void EventProcessor::enableParallelProcessing(bool enabled, size_t workerCount) {
    _parallelEnabled = enabled;
    _workerCount = workerCount;
}

void EventProcessor::processLoop() {
    LogEventPtr event;
    if (!_parallelEnabled) {
        while (!_shouldStop.load()) {
            if (_eventQueue.wait_and_pop(event)) {
                if (event && _eventHandler) {
                    _eventHandler(event);
                    _processedEventCount.fetch_add(1);
                }
            }
        }
        return;
    }

    // Parallel mode: start workers and dispatcher
    if (_actionManagerRef == nullptr) {
        std::cerr << "Parallel mode requires ActionManager reference" << std::endl;
        return;
    }

    _nextSequenceToExecute.store(1);
    // Start worker threads
    for (size_t i = 0; i < _workerCount; ++i) {
        _workers.emplace_back(&EventProcessor::workerLoop, this);
    }
    // Start dispatcher thread
    std::thread dispatcher(&EventProcessor::resultDispatcherLoop, this);

    size_t seqCounter = 1;
    while (!_shouldStop.load()) {
        if (_eventQueue.wait_and_pop(event)) {
            _matchQueue.push(MatchTask{seqCounter++, event});
        }
    }

    // Stop queues and join
    _matchQueue.stop();
    for (auto& t : _workers) { if (t.joinable()) t.join(); }
    _resultQueue.stop();
    if (dispatcher.joinable()) dispatcher.join();
}

void EventProcessor::workerLoop() {
    MatchTask task;
    while (_matchQueue.wait_and_pop(task)) {
        std::vector<ActionMapping> actions;
        bool any = _actionManagerRef->getActionsForEvent(task.event, actions);
        if (any) {
            _resultQueue.push(MatchResult{task.seq, std::move(actions)});
        } else {
            // Push empty to advance sequence through dispatcher
            _resultQueue.push(MatchResult{task.seq, {}});
        }
    }
}

void EventProcessor::resultDispatcherLoop() {
    MatchResult res;
    while (_resultQueue.wait_and_pop(res)) {
        size_t expected = _nextSequenceToExecute.load();
        if (res.seq == expected) {
            if (!res.actions.empty()) {
                _actionManagerRef->executeActions(res.actions);
            }
            _nextSequenceToExecute.fetch_add(1);
            // Drain contiguous pending
            std::lock_guard<std::mutex> lock(_pendingMutex);
            while (true) {
                auto it = _pending.find(_nextSequenceToExecute.load());
                if (it == _pending.end()) break;
                if (!it->second.empty()) {
                    _actionManagerRef->executeActions(it->second);
                }
                _pending.erase(it);
                _nextSequenceToExecute.fetch_add(1);
            }
        } else {
            std::lock_guard<std::mutex> lock(_pendingMutex);
            _pending.emplace(res.seq, std::move(res.actions));
        }
    }
}

void EventProcessor::defaultEventHandler(const LogEventPtr& event) {
    if (!event) {
        return;
    }
    
    // Convert timestamp to readable format
    auto time_t = std::chrono::system_clock::to_time_t(event->timestamp);
    auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(
        event->timestamp.time_since_epoch()) % 1000;
    
    std::tm timeinfo;
    localtime_s(&timeinfo, &time_t);
    
    std::cout << "[" << std::put_time(&timeinfo, "%Y-%m-%d %H:%M:%S");
    std::cout << "." << std::setfill('0') << std::setw(3) << ms.count() << "] ";
    std::cout << "Line " << event->lineNumber << ": " << event->data << std::endl;
}
