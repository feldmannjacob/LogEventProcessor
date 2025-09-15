#pragma once

#include <string>
#include <map>
#include <memory>
#include <vector>

// Forward declarations
class RegexMatcher;
class ActionManager;

/**
 * @class ConfigManager
 * @brief Manages configuration settings from YAML file
 */
class ConfigManager {
public:
    ConfigManager();
    ~ConfigManager();
    
    /**
     * @brief Load configuration from YAML file
     * @param configPath Path to the YAML configuration file
     * @return true if loaded successfully, false otherwise
     */
    bool loadConfig(const std::string& configPath);
    
    /**
     * @brief Get a configuration value as string
     * @param key Configuration key
     * @param defaultValue Default value if key not found
     * @return Configuration value or default value
     */
    std::string getString(const std::string& key, const std::string& defaultValue = "") const;
    
    /**
     * @brief Get a configuration value as integer
     * @param key Configuration key
     * @param defaultValue Default value if key not found
     * @return Configuration value or default value
     */
    int getInt(const std::string& key, int defaultValue = 0) const;
    
    /**
     * @brief Get a configuration value as boolean
     * @param key Configuration key
     * @param defaultValue Default value if key not found
     * @return Configuration value or default value
     */
    bool getBool(const std::string& key, bool defaultValue = false) const;
    
    /**
     * @brief Check if configuration is loaded
     * @return true if loaded, false otherwise
     */
    bool isLoaded() const { return _isLoaded; }
    
    /**
     * @brief Get the log file path from configuration
     * @return Log file path
     */
    std::string getLogFilePath() const;
    
    /**
     * @brief Get the output directory from configuration
     * @return Output directory path
     */
    std::string getOutputDirectory() const;
    
    /**
     * @brief Get the polling interval in milliseconds
     * @return Polling interval
     */
    int getPollingInterval() const;
    
    /**
     * @brief Check if all processes should be targeted
     * @return true if targeting all processes, false if targeting specific ones
     */
    bool getTargetAllProcesses() const;
    
    /**
     * @brief Get list of target process IDs
     * @return Vector of process IDs to target
     */
    std::vector<int> getTargetProcessIds() const;
    
    /**
     * @brief Get list of target process names
     * @return Vector of process names to target
     */
    std::vector<std::string> getTargetProcessNames() const;
    
    /**
     * @brief Load regex rules and actions from configuration
     * @param matcher RegexMatcher instance to populate with rules
     * @param actionManager ActionManager instance to populate with actions
     * @param configPath Path to the configuration file (optional, uses last loaded file if not provided)
     * @return true if rules were loaded successfully, false otherwise
     */
    bool loadRegexRulesAndActions(RegexMatcher& matcher, ActionManager& actionManager, const std::string& configPath = "") const;

private:
    std::map<std::string, std::string> _config;
    bool _isLoaded;
    std::string _lastConfigPath;
    
    /**
     * @brief Simple YAML parser for basic key-value pairs
     * @param content YAML file content
     */
    void parseYAML(const std::string& content);
    
    /**
     * @brief Trim whitespace from string
     * @param str String to trim
     * @return Trimmed string
     */
    std::string trim(const std::string& str) const;
};
