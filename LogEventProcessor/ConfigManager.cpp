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
    int currentCooldownMs = 0;
    
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
    auto collapseDoubleBackslashes = [](std::string s) {
        // Convert "\\\\" -> "\\" repeatedly to normalize overly escaped patterns from config samples
        for (size_t pos = 0; (pos = s.find("\\\\", pos)) != std::string::npos; ) {
            s.replace(pos, 2, "\\");
            pos += 1;
        }
        return s;
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
        
        if (inRegexRules && line.rfind("- name:", 0) == 0) {
            // Save previous rule if exists
            if (!currentRule.empty() && !currentPattern.empty()) {
                // Flush any pending step in actions list
                if (inActionsList) {
                    if (!currentStep.actionType.empty() || !currentStep.actionValue.empty()) {
                        currentSteps.push_back(currentStep);
                    }
                }
                std::cout << "[PARSE] Adding rule name='" << currentRule << "' pattern='" << currentPattern << "'"
                          << (inActionsList ? " with steps" : " with single action") << std::endl;
                matcher.addRule(currentRule, currentPattern, "", currentEnabled, currentCooldownMs);
                if (inActionsList && !currentSteps.empty()) {
                    // Ensure ruleName is set on each step
                    for (auto& s : currentSteps) { s.ruleName = currentRule; }
                    actionManager.addActionSequence(currentRule, currentSteps);
                } else {
                    actionManager.addActionMapping(currentRule, currentActionType, currentActionValue, currentModifiers, currentEnabled);
                }
            } else if (!currentRule.empty()) {
                std::cout << "[PARSE] Skipping rule name='" << currentRule << "' due to empty pattern" << std::endl;
            }
            
            // Start new rule
            // Extract value after ':' and strip optional quotes
            size_t colon = line.find(':');
            std::string raw = colon != std::string::npos ? line.substr(colon + 1) : std::string();
            // trim
            raw.erase(0, raw.find_first_not_of(" \t"));
            raw.erase(raw.find_last_not_of(" \t") + 1);
            if (!raw.empty() && (raw.front()=='"' || raw.front()=='\'')) {
                char q = raw.front();
                if (raw.size() >= 2 && raw.back()==q) raw = raw.substr(1, raw.size()-2);
            }
            currentRule = raw;
            currentPattern = "";
            currentActionType = "keystroke";
            currentActionValue = "";
            currentModifiers = 0;
            currentEnabled = true;
            currentCooldownMs = 0;
            inActionsList = false;
            currentSteps.clear();
            currentStep = ActionMapping();
        }
        else if (inRegexRules && line.rfind("pattern:", 0) == 0) {
            size_t colon = line.find(':');
            std::string raw = colon != std::string::npos ? line.substr(colon + 1) : std::string();
            raw.erase(0, raw.find_first_not_of(" \t"));
            raw.erase(raw.find_last_not_of(" \t") + 1);
            if (!raw.empty() && (raw.front()=='"' || raw.front()=='\'')) {
                char q = raw.front();
                if (raw.size() >= 2 && raw.back()==q) raw = raw.substr(1, raw.size()-2);
            }
            if (!raw.empty()) {
                raw = collapseDoubleBackslashes(raw);
                currentPattern = templateToRegex(raw);
            }
        }
        else if (inRegexRules && line.rfind("cooldown_ms:", 0) == 0) {
            size_t colon = line.find(':');
            std::string v = colon != std::string::npos ? line.substr(colon + 1) : std::string();
            v.erase(0, v.find_first_not_of(" \t"));
            v.erase(v.find_last_not_of(" \t") + 1);
            try { currentCooldownMs = std::stoi(v); } catch (...) { currentCooldownMs = 0; }
            if (currentCooldownMs < 0) currentCooldownMs = 0;
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
            auto extractStr = [](const std::string& l, const char* key) -> std::string {
                size_t pos = l.find(key);
                if (pos == std::string::npos) return std::string();
                pos = l.find(':', pos);
                if (pos == std::string::npos) return std::string();
                std::string v = l.substr(pos + 1);
                v.erase(0, v.find_first_not_of(" \t"));
                v.erase(v.find_last_not_of(" \t") + 1);
                if (!v.empty() && (v.front()=='"' || v.front()=='\'')) {
                    char q = v.front();
                    if (v.size() >= 2 && v.back()==q) v = v.substr(1, v.size()-2);
                }
                return v;
            };
            std::string t = extractStr(line, "type"); if (!t.empty()) currentStep.actionType = t;
            std::string val = extractStr(line, "value"); if (!val.empty()) currentStep.actionValue = val;
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
            if (line.rfind("type:", 0) == 0) {
                size_t colon = line.find(':');
                std::string v = line.substr(colon + 1);
                v.erase(0, v.find_first_not_of(" \t")); v.erase(v.find_last_not_of(" \t") + 1);
                if (!v.empty() && (v.front()=='"' || v.front()=='\'')) { char q=v.front(); if (v.back()==q) v=v.substr(1, v.size()-2);} 
                currentStep.actionType = v;
            } else if (line.rfind("value:", 0) == 0) {
                size_t colon = line.find(':');
                std::string v = line.substr(colon + 1);
                v.erase(0, v.find_first_not_of(" \t")); v.erase(v.find_last_not_of(" \t") + 1);
                if (!v.empty() && (v.front()=='"' || v.front()=='\'')) { char q=v.front(); if (v.back()==q) v=v.substr(1, v.size()-2);} 
                currentStep.actionValue = v;
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
        else if (inRegexRules && line.rfind("action_type:", 0) == 0) {
            size_t colon = line.find(':');
            std::string v = line.substr(colon + 1);
            v.erase(0, v.find_first_not_of(" \t")); v.erase(v.find_last_not_of(" \t") + 1);
            if (!v.empty() && (v.front()=='"' || v.front()=='\'')) { char q=v.front(); if (v.back()==q) v=v.substr(1, v.size()-2);} 
            currentActionType = v;
        }
        else if (inRegexRules && line.rfind("action_value:", 0) == 0) {
            size_t colon = line.find(':');
            std::string v = line.substr(colon + 1);
            v.erase(0, v.find_first_not_of(" \t")); v.erase(v.find_last_not_of(" \t") + 1);
            if (!v.empty() && (v.front()=='"' || v.front()=='\'')) { char q=v.front(); if (v.back()==q) v=v.substr(1, v.size()-2);} 
            currentActionValue = v;
        }
        else if (inRegexRules && line.rfind("modifiers:", 0) == 0) {
            size_t start = line.find("modifiers:") + 10;
            while (start < line.length() && (line[start] == ' ' || line[start] == '\t')) start++;
            try {
                currentModifiers = std::stoi(line.substr(start));
            } catch (const std::exception&) {
                currentModifiers = 0;
            }
        }
        else if (inRegexRules && line.rfind("enabled:", 0) == 0) {
            size_t start = line.find("enabled:") + 8;
            while (start < line.length() && (line[start] == ' ' || line[start] == '\t')) start++;
            std::string v = line.substr(start);
            // normalize
            std::transform(v.begin(), v.end(), v.begin(), ::tolower);
            currentEnabled = (v == "true" || v == "1" || v == "yes");
        }
    }
    
    // Save the last rule
    if (!currentRule.empty() && !currentPattern.empty()) {
        if (inActionsList) {
            if (!currentStep.actionType.empty() || !currentStep.actionValue.empty()) {
                currentSteps.push_back(currentStep);
            }
        }
        std::cout << "[PARSE] Adding rule name='" << currentRule << "' pattern='" << currentPattern << "'"
                  << (inActionsList ? " with steps" : " with single action") << std::endl;
        matcher.addRule(currentRule, currentPattern, "", currentEnabled, currentCooldownMs);
        if (inActionsList && !currentSteps.empty()) {
            for (auto& s : currentSteps) { s.ruleName = currentRule; }
            actionManager.addActionSequence(currentRule, currentSteps);
        } else {
            actionManager.addActionMapping(currentRule, currentActionType, currentActionValue, currentModifiers, currentEnabled);
        }
    } else if (!currentRule.empty()) {
        std::cout << "[PARSE] Skipping rule name='" << currentRule << "' due to empty pattern" << std::endl;
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
