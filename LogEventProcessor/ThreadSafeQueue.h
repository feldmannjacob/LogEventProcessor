#pragma once

#include <queue>
#include <mutex>
#include <condition_variable>
#include <memory>
#include <atomic>

/**
 * @class ThreadSafeQueue
 * @brief A thread-safe queue for passing events between threads
 * @tparam T The type of data to store in the queue
 */
template<typename T>
class ThreadSafeQueue {
public:
    ThreadSafeQueue() : _stop(false) {}
    
    /**
     * @brief Add an item to the queue
     * @param item The item to add
     */
    void push(T item) {
        std::lock_guard<std::mutex> lock(_mutex);
        _queue.push(std::move(item));
        _condition.notify_one();
    }
    
    /**
     * @brief Wait for an item and pop it from the queue
     * @param item Reference to store the popped item
     * @return true if an item was popped, false if the queue was stopped
     */
    bool wait_and_pop(T& item) {
        std::unique_lock<std::mutex> lock(_mutex);
        _condition.wait(lock, [this] { return !_queue.empty() || _stop; });
        
        if (_stop && _queue.empty()) {
            return false;
        }
        
        item = std::move(_queue.front());
        _queue.pop();
        return true;
    }
    
    /**
     * @brief Try to pop an item without waiting
     * @param item Reference to store the popped item
     * @return true if an item was popped, false if queue is empty
     */
    bool try_pop(T& item) {
        std::lock_guard<std::mutex> lock(_mutex);
        if (_queue.empty()) {
            return false;
        }
        
        item = std::move(_queue.front());
        _queue.pop();
        return true;
    }
    
    /**
     * @brief Check if the queue is empty
     * @return true if empty, false otherwise
     */
    bool empty() const {
        std::lock_guard<std::mutex> lock(_mutex);
        return _queue.empty();
    }
    
    /**
     * @brief Get the current size of the queue
     * @return Number of items in the queue
     */
    size_t size() const {
        std::lock_guard<std::mutex> lock(_mutex);
        return _queue.size();
    }
    
    /**
     * @brief Stop the queue and wake up all waiting threads
     */
    void stop() {
        std::lock_guard<std::mutex> lock(_mutex);
        _stop = true;
        _condition.notify_all();
    }
    
    /**
     * @brief Check if the queue has been stopped
     * @return true if stopped, false otherwise
     */
    bool is_stopped() const {
        return _stop.load();
    }

private:
    mutable std::mutex _mutex;
    std::queue<T> _queue;
    std::condition_variable _condition;
    std::atomic<bool> _stop;
};
