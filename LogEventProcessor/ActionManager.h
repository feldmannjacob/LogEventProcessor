#pragma once

#include <string>
#include <map>
#include <memory>
#include <mutex>
#include <vector>
#include <unordered_map>
#include <chrono>
#include "ActionSender.h"
#include "RegexMatcher.h"
#include "LogEvent.h"

/**
 * @struct ActionMapping
 * @brief Maps regex rule names to actions
 */
struct ActionMapping {
    std::string ruleName;
    std::string actionType;  // "keystroke", "command", "text", "sms"
    std::string actionValue; // The actual action to perform
    std::string logLine;     // The original log line (for SMS action type)
    int modifiers;           // Modifier keys for keystrokes
    bool enabled;
    int delayMs;             // Delay after executing this step (ms)
    
    ActionMapping() : modifiers(0), enabled(false), delayMs(0) {}
    
    ActionMapping(const std::string& rule, const std::string& type, 
                  const std::string& value, int mods = 0, bool isEnabled = true)
        : ruleName(rule), actionType(type), actionValue(value), modifiers(mods), enabled(isEnabled), delayMs(0) {}
};

/**
 * @class ActionManager
 * @brief Manages the connection between regex matching and action execution
 */
class ActionManager {
public:
    ActionManager();
    ~ActionManager();
    
    /**
     * @brief Initialize the action manager
     * @return true if initialization successful, false otherwise
     */
    bool initialize();
    
    /**
     * @brief Add an action mapping for a regex rule
     * @param mapping The action mapping to add
     */
    void addActionMapping(const ActionMapping& mapping);
    
    /**
     * @brief Add an action mapping with parameters
     * @param ruleName Name of the regex rule
     * @param actionType Type of action ("keystroke", "command", "text")
     * @param actionValue The action to perform
     * @param modifiers Modifier keys (for keystrokes)
     * @param enabled Whether the mapping is enabled
     */
    void addActionMapping(const std::string& ruleName, const std::string& actionType,
                         const std::string& actionValue, int modifiers = 0, bool enabled = true);

    // Add multiple action steps for a rule at once
    void addActionSequence(const std::string& ruleName, const std::vector<ActionMapping>& steps);
    
    /**
     * @brief Process a log event and execute actions if rules match
     * @param event The log event to process
     * @return true if any actions were executed, false otherwise
     */
    bool processEvent(const LogEventPtr& event);
    
    /**
     * @brief Set the regex matcher to use
     * @param matcher Pointer to the regex matcher
     */
    void setRegexMatcher(RegexMatcher* matcher);
    
    /**
     * @brief Get the action sender instance
     * @return Reference to the action sender
     */
    ActionSender& getActionSender() { return _actionSender; }
    
    /**
     * @brief Enable or disable an action mapping
     * @param ruleName Name of the rule
     * @param enabled Whether to enable the mapping
     * @return true if mapping was found and updated, false otherwise
     */
    bool setActionEnabled(const std::string& ruleName, bool enabled);
    
    /**
     * @brief Remove an action mapping
     * @param ruleName Name of the rule to remove
     * @return true if mapping was found and removed, false otherwise
     */
    bool removeActionMapping(const std::string& ruleName);
    
    /**
     * @brief Get the number of action mappings
     * @return Number of mappings
     */
    size_t getMappingCount() const { return _actionMappings.size(); }
    
    /**
     * @brief Get the number of actions executed
     * @return Count of executed actions
     */
    size_t getExecutedActionCount() const { return _executedActionCount.load(); }
    
    /**
     * @brief Get the number of failed actions
     * @return Count of failed actions
     */
    size_t getFailedActionCount() const { return _failedActionCount.load(); }
    
    /**
     * @brief Clear all action mappings
     */
    void clearActionMappings();

    /**
     * @brief Collect actions that should be executed for a given event without executing them
     * @param event The log event to evaluate
     * @param outActions Output vector of ActionMapping in deterministic order
     * @return true if any actions were collected
     */
    bool getActionsForEvent(const LogEventPtr& event, std::vector<ActionMapping>& outActions) const;

    /**
     * @brief Execute a list of actions sequentially
     * @param actions Actions to execute
     * @return true if all actions succeeded
     */
    bool executeActions(const std::vector<ActionMapping>& actions);

private:
    ActionSender _actionSender;
    RegexMatcher* _regexMatcher;
    std::map<std::string, std::vector<ActionMapping>> _actionMappings;
    std::atomic<size_t> _executedActionCount;
    std::atomic<size_t> _failedActionCount;
    mutable std::mutex _mutex;
    // Cooldown tracking per rule
    std::unordered_map<std::string, std::chrono::steady_clock::time_point> _lastRuleFireTime;
    mutable std::mutex _cooldownMutex;
    
    /**
     * @brief Execute an action based on the mapping
     * @param mapping The action mapping to execute
     * @return true if action was executed successfully, false otherwise
     */
    bool executeAction(const ActionMapping& mapping);
    
    /**
     * @brief Parse a keystroke string (e.g., "ctrl+a", "f1", "enter")
     * @param keystrokeString The string to parse
     * @param key Output parameter for the virtual key code
     * @param modifiers Output parameter for modifier keys
     * @return true if parsing was successful, false otherwise
     */
    bool parseKeystroke(const std::string& keystrokeString, int& key, int& modifiers);
    // Parse possibly multiple keys like "ctrl+1+2" or "alt + f1 + f2"; returns list of keys and modifiers
    bool parseChord(const std::string& keystrokeString, std::vector<int>& keys, int& modifiers);
    
    /**
     * @brief Get virtual key code from string
     * @param keyString The key string (e.g., "f1", "enter", "space")
     * @return Virtual key code or 0 if not found
     */
    int getVirtualKeyCode(const std::string& keyString) const;
};
