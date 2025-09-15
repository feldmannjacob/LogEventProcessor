#pragma once

#include <windows.h>
#include <string>
#include <vector>
#include <memory>
#include <mutex>
#include <atomic>
#include <map>
#include <chrono>

/**
 * @class ActionSender
 * @brief Sends keystrokes to the eqgame.exe process
 */
class ActionSender {
public:
    ActionSender();
    ~ActionSender();
    
    /**
     * @brief Initialize the action sender and find the eqgame.exe process
     * @return true if initialization successful, false otherwise
     */
    bool initialize();
    
    /**
     * @brief Check if the action sender is ready to send keystrokes
     * @return true if ready, false otherwise
     */
    bool isReady() const { return _isReady.load(); }
    
    /**
     * @brief Send a single keystroke to the process
     * @param key The virtual key code to send
     * @param modifiers Modifier keys (Ctrl, Alt, Shift) - can be combined with |
     * @return true if keystroke was sent successfully, false otherwise
     */
    bool sendKeystroke(int key, int modifiers = 0);
    
    /**
     * @brief Send a string of characters to the process
     * @param text The text to send
     * @return true if text was sent successfully, false otherwise
     */
    bool sendText(const std::string& text);
    
    /**
     * @brief Send a sequence of keystrokes
     * @param keys Vector of virtual key codes
     * @param modifiers Modifier keys for the sequence
     * @return true if sequence was sent successfully, false otherwise
     */
    bool sendKeystrokeSequence(const std::vector<int>& keys, int modifiers = 0);
    
    /**
     * @brief Send a command (common EQ commands)
     * @param command The command to send (e.g., "sit", "stand", "follow")
     * @return true if command was sent successfully, false otherwise
     */
    bool sendCommand(const std::string& command);

    /**
     * @brief Send SMS (tell message) via email
     * @param logLine The log line containing the tell message
     * @return true if SMS was sent successfully, false otherwise
     */
    bool sendSms(const std::string& logLine);

    /**
     * @brief Send a chord: hold modifiers, press each key in order, then release modifiers
     * @param keys Vector of virtual key codes to press
     * @param modifiers Modifier keys (Ctrl, Alt, Shift)
     * @param pressTogether If true and feasible, attempt minimal delay to simulate simultaneity
     * @return true if the chord was sent successfully
     */
    bool sendChord(const std::vector<int>& keys, int modifiers = 0, bool pressTogether = false);
    
    /**
     * @brief Refresh the process handle (useful if process restarts)
     * @return true if process was found and handle refreshed, false otherwise
     */
    bool refreshProcess();
    
    /**
     * @brief Get the process ID of the target process
     * @return Process ID or 0 if not found
     */
    DWORD getProcessId() const { return _processId; }
    
    /**
     * @brief Get the window handle of the target process
     * @return Window handle or NULL if not found
     */
    HWND getWindowHandle() const { return _windowHandle; }
    
    /**
     * @brief Get the number of successful keystrokes sent
     * @return Count of successful keystrokes
     */
    size_t getSuccessCount() const { return _successCount.load(); }
    
    /**
     * @brief Get the number of failed keystrokes
     * @return Count of failed keystrokes
     */
    size_t getFailureCount() const { return _failureCount.load(); }
    
    /**
     * @brief Check for email responses and send them as commands
     */
    bool checkEmailResponses();

    /**
     * @brief Send acknowledgment email for processed response
     * @param response The response that was processed
     * @return true if acknowledgment was sent successfully, false otherwise
     */
    bool sendAcknowledgmentEmail(const std::string& response);

private:
    struct Target {
        DWORD pid;
        HWND hwnd;
    };

    std::atomic<bool> _isReady;
    std::atomic<size_t> _successCount;
    std::atomic<size_t> _failureCount;
    std::chrono::steady_clock::time_point _startupTime;
    
    DWORD _processId;
    HWND _windowHandle;
    HANDLE _processHandle;
    std::vector<Target> _targets;
    
    mutable std::mutex _mutex;
    
    /**
     * @brief Find the eqgame.exe process
     * @return true if process found, false otherwise
     */
    bool findProcess();
    bool findAllTargets();
    
    /**
     * @brief Find the main window of the process
     * @return true if window found, false otherwise
     */
    bool findWindow();
    
    /**
     * @brief Send a key down event
     * @param key Virtual key code
     * @param modifiers Modifier keys
     * @return true if successful, false otherwise
     */
    bool sendKeyDown(int key, int modifiers = 0);
    
    /**
     * @brief Send a key up event
     * @param key Virtual key code
     * @param modifiers Modifier keys
     * @return true if successful, false otherwise
     */
    bool sendKeyUp(int key, int modifiers = 0);
    
    /**
     * @brief Convert character to virtual key code
     * @param c Character to convert
     * @return Virtual key code
     */
    int charToVk(char c) const;
    
    /**
     * @brief Get modifier flags for a key
     * @param key Virtual key code
     * @return Modifier flags
     */
    int getModifierFlags(int key) const;

    /**
     * @brief Try to bring target window to foreground reliably
     */
    bool bringToForeground();
    bool bringToForeground(HWND hwnd, DWORD pid);

    /**
     * @brief Send a key using scancodes for better game compatibility
     */
    bool sendKeyScan(int vk, bool keyUp = false) const;
    

    /**
     * @brief Press or release modifier virtual keys based on flags
     */
    void sendModifiers(int modifiers, bool keyUp);
    
private:
    // Private members will be added here
    
};
