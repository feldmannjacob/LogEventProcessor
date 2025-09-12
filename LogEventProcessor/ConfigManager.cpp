#include "ConfigManager.h"
#include "RegexMatcher.h"
#include "ActionManager.h"
#include <fstream>
#include <sstream>
#include <algorithm>
#include <iostream>
#include <unordered_set>

ConfigManager::ConfigManager() : _isLoaded(false), _lastConfigPath("") {
}

ConfigManager::~ConfigManager() {
}

bool ConfigManager::loadConfig(const std::string& configPath) {
    std::ifstream file(configPath);
    if (!file.is_open()) {
        std::cerr << "Error: Could not open configuration file: " << configPath << std::endl;
        return false;
    }
    
    std::stringstream buffer;
    buffer << file.rdbuf();
    file.close();
    
    parseYAML(buffer.str());
    _isLoaded = true;
    _lastConfigPath = configPath;
    
    std::cout << "Configuration loaded successfully from: " << configPath << std::endl;
    return true;
}

std::string ConfigManager::getString(const std::string& key, const std::string& defaultValue) const {
    auto it = _config.find(key);
    if (it != _config.end()) {
        return it->second;
    }
    return defaultValue;
}

int ConfigManager::getInt(const std::string& key, int defaultValue) const {
    auto it = _config.find(key);
    if (it != _config.end()) {
        try {
            return std::stoi(it->second);
        } catch (const std::exception&) {
            return defaultValue;
        }
    }
    return defaultValue;
}

bool ConfigManager::getBool(const std::string& key, bool defaultValue) const {
    auto it = _config.find(key);
    if (it != _config.end()) {
        std::string value = it->second;
        std::transform(value.begin(), value.end(), value.begin(), ::tolower);
        return value == "true" || value == "1" || value == "yes";
    }
    return defaultValue;
}

std::string ConfigManager::getLogFilePath() const {
    return getString("log_file_path", "application.log");
}

std::string ConfigManager::getOutputDirectory() const {
    return getString("output_directory", "./output");
}

int ConfigManager::getPollingInterval() const {
    return getInt("polling_interval_ms", 1000);
}

bool ConfigManager::loadRegexRulesAndActions(RegexMatcher& matcher, ActionManager& actionManager, const std::string& configPath) const {
    if (!_isLoaded) {
        std::cerr << "Configuration not loaded. Cannot load regex rules." << std::endl;
        return false;
    }
    
    // Use provided configPath or fall back to last loaded config
    std::string filePath = configPath.empty() ? _lastConfigPath : configPath;
    if (filePath.empty()) {
        std::cerr << "No configuration file path available for parsing regex rules" << std::endl;
        return false;
    }
    
    // Parse the regex rules from the configuration
    // This is a simplified parser - in a full implementation you'd use a proper YAML library
    
    // Load the config file content
    std::ifstream file(filePath);
    if (!file.is_open()) {
        std::cerr << "Could not open " << filePath << " for parsing regex rules" << std::endl;
        return false;
    }
    
    std::string content((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
    file.close();
    
    // Simple parsing of regex rules section
    std::istringstream stream(content);
    std::string line;
    bool inRegexRules = false;
    std::string currentRule;
    std::string currentPattern;
    std::string currentActionType;
    std::string currentActionValue;
    int currentModifiers = 0;
    bool currentEnabled = true;
    bool inActionsList = false;
    std::vector<ActionMapping> currentSteps;
    ActionMapping currentStep;
    
    // Helper: convert template with '#' into regex by only replacing '#' with a capture of non-space
    auto templateToRegex = [](const std::string& templ) -> std::string {
        if (templ.find('#') == std::string::npos) {
            return templ; // no placeholder, treat as provided (may already be regex)
        }
        std::string out;
        out.reserve(templ.size() + 8);
        for (char c : templ) {
            if (c == '#') {
                out += "([^\\s]+)"; // capture contiguous non-space
            } else {
                // Leave other characters (including regex metacharacters like . * + etc.) intact
                out.push_back(c);
            }
        }
        return out;
    };

    while (std::getline(stream, line)) {
        // Trim whitespace
        line.erase(0, line.find_first_not_of(" \t"));
        line.erase(line.find_last_not_of(" \t") + 1);
        
        if (line == "regex_rules:") {
            inRegexRules = true;
            continue;
        }
        
        if (inRegexRules && line.empty()) {
            continue;
        }
        
        // Do not prematurely break; continue scanning until EOF
        
        if (inRegexRules && line.find("- name:") != std::string::npos) {
            // Save previous rule if exists
            if (!currentRule.empty() && !currentPattern.empty()) {
                // Flush any pending step in actions list
                if (inActionsList) {
                    if (!currentStep.actionType.empty() || !currentStep.actionValue.empty()) {
                        currentSteps.push_back(currentStep);
                    }
                }
                matcher.addRule(currentRule, currentPattern, "", currentEnabled);
                if (inActionsList && !currentSteps.empty()) {
                    // Ensure ruleName is set on each step
                    for (auto& s : currentSteps) { s.ruleName = currentRule; }
                    actionManager.addActionSequence(currentRule, currentSteps);
                } else {
                    actionManager.addActionMapping(currentRule, currentActionType, currentActionValue, currentModifiers, currentEnabled);
                }
            }
            
            // Start new rule
            size_t start = line.find('"') + 1;
            size_t end = line.find('"', start);
            currentRule = line.substr(start, end - start);
            currentPattern = "";
            currentActionType = "keystroke";
            currentActionValue = "";
            currentModifiers = 0;
            currentEnabled = true;
            inActionsList = false;
            currentSteps.clear();
            currentStep = ActionMapping();
        }
        else if (inRegexRules && line.find("pattern:") != std::string::npos) {
            size_t start = line.find('"') + 1;
            size_t end = line.find('"', start);
            if (start > 0 && end != std::string::npos) {
                std::string rawPattern = line.substr(start, end - start);
                currentPattern = templateToRegex(rawPattern);
            }
        }
        else if (inRegexRules && line == "actions:") {
            inActionsList = true;
            currentSteps.clear();
            currentStep = ActionMapping();
        }
        else if (inRegexRules && inActionsList && line.rfind("- ", 0) == 0) {
            // Starting a new step; flush previous if any
            if (!currentStep.actionType.empty() || !currentStep.actionValue.empty()) {
                currentSteps.push_back(currentStep);
            }
            currentStep = ActionMapping();
            currentStep.ruleName = currentRule;
            // Parse inline key on same line if present (e.g., - type: "command")
            if (line.find("type:") != std::string::npos) {
                size_t start = line.find('"') + 1;
                size_t end = line.find('"', start);
                if (start > 0 && end != std::string::npos) {
                    currentStep.actionType = line.substr(start, end - start);
                }
            }
            if (line.find("value:") != std::string::npos) {
                size_t start = line.find('"') + 1;
                size_t end = line.find('"', start);
                if (start > 0 && end != std::string::npos) {
                    currentStep.actionValue = line.substr(start, end - start);
                }
            }
            if (line.find("modifiers:") != std::string::npos) {
                size_t pos = line.find("modifiers:"); pos += 10;
                while (pos < line.length() && (line[pos] == ' ' || line[pos] == '\t')) pos++;
                try { currentStep.modifiers = std::stoi(line.substr(pos)); } catch (...) { currentStep.modifiers = 0; }
            }
            if (line.find("delay_ms:") != std::string::npos) {
                size_t pos = line.find("delay_ms:"); pos += 9;
                while (pos < line.length() && (line[pos] == ' ' || line[pos] == '\t')) pos++;
                try { currentStep.delayMs = std::stoi(line.substr(pos)); } catch (...) { currentStep.delayMs = 0; }
            }
            if (line.find("enabled:") != std::string::npos) {
                size_t pos = line.find("enabled:"); pos += 8;
                while (pos < line.length() && (line[pos] == ' ' || line[pos] == '\t')) pos++;
                std::string v = line.substr(pos);
                currentStep.enabled = (v == "true");
            } else {
                currentStep.enabled = true; // default
            }
        }
        else if (inRegexRules && inActionsList) {
            // Continuation lines for current step
            if (line.find("type:") != std::string::npos) {
                size_t start = line.find('"') + 1;
                size_t end = line.find('"', start);
                if (start > 0 && end != std::string::npos) {
                    currentStep.actionType = line.substr(start, end - start);
                }
            } else if (line.find("value:") != std::string::npos) {
                size_t start = line.find('"') + 1;
                size_t end = line.find('"', start);
                if (start > 0 && end != std::string::npos) {
                    currentStep.actionValue = line.substr(start, end - start);
                }
            } else if (line.find("modifiers:") != std::string::npos) {
                size_t pos = line.find("modifiers:"); pos += 10;
                while (pos < line.length() && (line[pos] == ' ' || line[pos] == '\t')) pos++;
                try { currentStep.modifiers = std::stoi(line.substr(pos)); } catch (...) { currentStep.modifiers = 0; }
            } else if (line.find("delay_ms:") != std::string::npos) {
                size_t pos = line.find("delay_ms:"); pos += 9;
                while (pos < line.length() && (line[pos] == ' ' || line[pos] == '\t')) pos++;
                try { currentStep.delayMs = std::stoi(line.substr(pos)); } catch (...) { currentStep.delayMs = 0; }
            } else if (line.find("enabled:") != std::string::npos) {
                size_t pos = line.find("enabled:"); pos += 8;
                while (pos < line.length() && (line[pos] == ' ' || line[pos] == '\t')) pos++;
                std::string v = line.substr(pos);
                currentStep.enabled = (v == "true");
            }
        }
        else if (inRegexRules && line.find("action_type:") != std::string::npos) {
            size_t start = line.find('"') + 1;
            size_t end = line.find('"', start);
            if (start > 0 && end != std::string::npos) {
                currentActionType = line.substr(start, end - start);
            }
        }
        else if (inRegexRules && line.find("action_value:") != std::string::npos) {
            size_t start = line.find('"') + 1;
            size_t end = line.find('"', start);
            if (start > 0 && end != std::string::npos) {
                currentActionValue = line.substr(start, end - start);
            }
        }
        else if (inRegexRules && line.find("modifiers:") != std::string::npos) {
            size_t start = line.find("modifiers:") + 10;
            while (start < line.length() && (line[start] == ' ' || line[start] == '\t')) start++;
            try {
                currentModifiers = std::stoi(line.substr(start));
            } catch (const std::exception&) {
                currentModifiers = 0;
            }
        }
        else if (inRegexRules && line.find("enabled:") != std::string::npos) {
            size_t start = line.find("enabled:") + 8;
            while (start < line.length() && (line[start] == ' ' || line[start] == '\t')) start++;
            currentEnabled = (line.substr(start) == "true");
        }
    }
    
    // Save the last rule
    if (!currentRule.empty() && !currentPattern.empty()) {
        if (inActionsList) {
            if (!currentStep.actionType.empty() || !currentStep.actionValue.empty()) {
                currentSteps.push_back(currentStep);
            }
        }
        matcher.addRule(currentRule, currentPattern, "", currentEnabled);
        if (inActionsList && !currentSteps.empty()) {
            for (auto& s : currentSteps) { s.ruleName = currentRule; }
            actionManager.addActionSequence(currentRule, currentSteps);
        } else {
            actionManager.addActionMapping(currentRule, currentActionType, currentActionValue, currentModifiers, currentEnabled);
        }
    }
    
    std::cout << "Loaded " << matcher.getRuleCount() << " regex rules with actions." << std::endl;
    return true;
}

void ConfigManager::parseYAML(const std::string& content) {
    std::istringstream stream(content);
    std::string line;
    
    while (std::getline(stream, line)) {
        // Skip empty lines and comments
        if (line.empty() || line[0] == '#') {
            continue;
        }
        
        // Find the colon separator
        size_t colonPos = line.find(':');
        if (colonPos == std::string::npos) {
            continue;
        }
        
        // Extract key and value
        std::string key = trim(line.substr(0, colonPos));
        std::string value = trim(line.substr(colonPos + 1));
        
        // Remove quotes if present
        if (value.length() >= 2 && value[0] == '"' && value[value.length() - 1] == '"') {
            value = value.substr(1, value.length() - 2);
        }
        
        if (!key.empty()) {
            _config[key] = value;
        }
    }
}

std::string ConfigManager::trim(const std::string& str) const {
    size_t first = str.find_first_not_of(' ');
    if (first == std::string::npos) {
        return "";
    }
    size_t last = str.find_last_not_of(' ');
    return str.substr(first, (last - first + 1));
}
