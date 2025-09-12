#pragma once

#include <string>
#include <vector>
#include <regex>
#include <functional>
#include <memory>
#include "LogEvent.h"

/**
 * @struct RegexRule
 * @brief Represents a regex pattern with associated action
 */
struct RegexRule {
    std::string name;
    std::string pattern;
    std::string description;
    bool enabled;
    
    RegexRule(const std::string& ruleName, const std::string& regexPattern, 
              const std::string& ruleDescription = "", bool isEnabled = true)
        : name(ruleName), pattern(regexPattern), description(ruleDescription), enabled(isEnabled) {}
};

/**
 * @class RegexMatcher
 * @brief Matches log events against regex patterns and triggers actions
 */
class RegexMatcher {
public:
    using ActionCallback = std::function<void(const LogEventPtr&, const RegexRule&, const std::smatch&)>;
    
    RegexMatcher();
    ~RegexMatcher();
    
    /**
     * @brief Add a regex rule
     * @param rule The regex rule to add
     */
    void addRule(const RegexRule& rule);
    
    /**
     * @brief Add a regex rule with parameters
     * @param name Rule name
     * @param pattern Regex pattern
     * @param description Rule description
     * @param enabled Whether the rule is enabled
     */
    void addRule(const std::string& name, const std::string& pattern, 
                const std::string& description = "", bool enabled = true);
    
    /**
     * @brief Remove a rule by name
     * @param name Rule name to remove
     * @return true if rule was found and removed, false otherwise
     */
    bool removeRule(const std::string& name);
    
    /**
     * @brief Enable or disable a rule
     * @param name Rule name
     * @param enabled Whether to enable the rule
     * @return true if rule was found and updated, false otherwise
     */
    bool setRuleEnabled(const std::string& name, bool enabled);
    
    /**
     * @brief Process a log event against all rules
     * @param event The log event to process
     * @return true if any rule matched, false otherwise
     */
    bool processEvent(const LogEventPtr& event);
    
    /**
     * @brief Set the action callback for when rules match
     * @param callback Function to call when a rule matches
     */
    void setActionCallback(ActionCallback callback);
    
    /**
     * @brief Get the number of rules
     * @return Number of rules
     */
    size_t getRuleCount() const { return _rules.size(); }
    
    /**
     * @brief Get rule information
     * @param index Rule index
     * @return Pointer to rule or nullptr if index is invalid
     */
    const RegexRule* getRule(size_t index) const;
    
    /**
     * @brief Clear all rules
     */
    void clearRules();
    
    /**
     * @brief Get match statistics
     * @return Number of matches since last reset
     */
    size_t getMatchCount() const { return _matchCount; }
    
    /**
     * @brief Reset match statistics
     */
    void resetMatchCount() { _matchCount = 0; }

private:
    std::vector<RegexRule> _rules;
    std::vector<std::regex> _compiledPatterns;
    ActionCallback _actionCallback;
    size_t _matchCount;
    
    /**
     * @brief Compile all regex patterns
     */
    void compilePatterns();
    
    /**
     * @brief Default action callback - prints match information
     * @param event The matched event
     * @param rule The matched rule
     * @param matches Regex match results
     */
    void defaultAction(const LogEventPtr& event, const RegexRule& rule, const std::smatch& matches);
};
