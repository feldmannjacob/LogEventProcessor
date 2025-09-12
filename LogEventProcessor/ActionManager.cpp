#include "ActionManager.h"
#include <iostream>
#include <sstream>
#include <algorithm>

ActionManager::ActionManager() 
    : _regexMatcher(nullptr), _executedActionCount(0), _failedActionCount(0) {
}

ActionManager::~ActionManager() {
}

bool ActionManager::initialize() {
    if (!_actionSender.initialize()) {
        std::cerr << "Failed to initialize ActionSender" << std::endl;
        return false;
    }
    
    std::cout << "ActionManager initialized successfully" << std::endl;
    return true;
}

void ActionManager::addActionMapping(const ActionMapping& mapping) {
    std::lock_guard<std::mutex> lock(_mutex);
    _actionMappings[mapping.ruleName] = mapping;
    std::cout << "Added action mapping: " << mapping.ruleName 
              << " -> " << mapping.actionType << ":" << mapping.actionValue << std::endl;
}

void ActionManager::addActionMapping(const std::string& ruleName, const std::string& actionType,
                                   const std::string& actionValue, int modifiers, bool enabled) {
    addActionMapping(ActionMapping(ruleName, actionType, actionValue, modifiers, enabled));
}

bool ActionManager::processEvent(const LogEventPtr& event) {
    if (!event || !_regexMatcher) {
        return false;
    }
    
    // Check if any regex rules match
    std::smatch matches;
    bool anyMatch = false;
    
    for (size_t i = 0; i < _regexMatcher->getRuleCount(); ++i) {
        const RegexRule* rule = _regexMatcher->getRule(i);
        if (!rule || !rule->enabled) {
            continue;
        }
        
        if (std::regex_search(event->data, matches, std::regex(rule->pattern))) {
            // Check if we have an action mapping for this rule
            std::lock_guard<std::mutex> lock(_mutex);
            auto it = _actionMappings.find(rule->name);
            if (it != _actionMappings.end() && it->second.enabled) {
                std::cout << "[ACTION] Rule '" << rule->name << "' matched, executing action: " 
                         << it->second.actionType << ":" << it->second.actionValue << std::endl;
                
                if (executeAction(it->second)) {
                    _executedActionCount.fetch_add(1);
                    anyMatch = true;
                } else {
                    _failedActionCount.fetch_add(1);
                }
            }
        }
    }
    
    return anyMatch;
}

bool ActionManager::getActionsForEvent(const LogEventPtr& event, std::vector<ActionMapping>& outActions) const {
    if (!event || !_regexMatcher) return false;
    std::smatch matches;
    bool any = false;
    // Iterate rules in index order to keep deterministic
    for (size_t i = 0; i < _regexMatcher->getRuleCount(); ++i) {
        const RegexRule* rule = _regexMatcher->getRule(i);
        if (!rule || !rule->enabled) continue;
        if (std::regex_search(event->data, matches, std::regex(rule->pattern))) {
            // Guard mappings access
            std::lock_guard<std::mutex> lock(_mutex);
            auto it = _actionMappings.find(rule->name);
            if (it != _actionMappings.end() && it->second.enabled) {
                outActions.push_back(it->second);
                any = true;
            }
        }
    }
    return any;
}

bool ActionManager::executeActions(const std::vector<ActionMapping>& actions) {
    bool allOk = true;
    for (const auto& m : actions) {
        if (executeAction(m)) {
            _executedActionCount.fetch_add(1);
        } else {
            _failedActionCount.fetch_add(1);
            allOk = false;
        }
    }
    return allOk;
}

void ActionManager::setRegexMatcher(RegexMatcher* matcher) {
    _regexMatcher = matcher;
}

bool ActionManager::setActionEnabled(const std::string& ruleName, bool enabled) {
    std::lock_guard<std::mutex> lock(_mutex);
    auto it = _actionMappings.find(ruleName);
    if (it != _actionMappings.end()) {
        it->second.enabled = enabled;
        return true;
    }
    return false;
}

bool ActionManager::removeActionMapping(const std::string& ruleName) {
    std::lock_guard<std::mutex> lock(_mutex);
    auto it = _actionMappings.find(ruleName);
    if (it != _actionMappings.end()) {
        _actionMappings.erase(it);
        return true;
    }
    return false;
}

void ActionManager::clearActionMappings() {
    std::lock_guard<std::mutex> lock(_mutex);
    _actionMappings.clear();
}

bool ActionManager::executeAction(const ActionMapping& mapping) {
    if (!_actionSender.isReady()) {
        std::cerr << "ActionSender not ready" << std::endl;
        return false;
    }
    
    if (mapping.actionType == "keystroke") {
        int key, modifiers;
        if (parseKeystroke(mapping.actionValue, key, modifiers)) {
            return _actionSender.sendKeystroke(key, modifiers);
        } else {
            std::cerr << "Failed to parse keystroke: " << mapping.actionValue << std::endl;
            return false;
        }
    } else if (mapping.actionType == "command") {
        return _actionSender.sendCommand(mapping.actionValue);
    } else if (mapping.actionType == "text") {
        return _actionSender.sendText(mapping.actionValue);
    } else {
        std::cerr << "Unknown action type: " << mapping.actionType << std::endl;
        return false;
    }
}

bool ActionManager::parseKeystroke(const std::string& keystrokeString, int& key, int& modifiers) {
    key = 0;
    modifiers = 0;
    
    std::string lower = keystrokeString;
    std::transform(lower.begin(), lower.end(), lower.begin(), ::tolower);
    
    // Parse modifiers
    if (lower.find("ctrl+") != std::string::npos) {
        modifiers |= MOD_CONTROL;
        lower = lower.substr(5);
    }
    if (lower.find("alt+") != std::string::npos) {
        modifiers |= MOD_ALT;
        lower = lower.substr(4);
    }
    if (lower.find("shift+") != std::string::npos) {
        modifiers |= MOD_SHIFT;
        lower = lower.substr(6);
    }
    
    // Parse the key
    key = getVirtualKeyCode(lower);
    return key != 0;
}

int ActionManager::getVirtualKeyCode(const std::string& keyString) const {
    // Function keys
    if (keyString == "f1") return VK_F1;
    if (keyString == "f2") return VK_F2;
    if (keyString == "f3") return VK_F3;
    if (keyString == "f4") return VK_F4;
    if (keyString == "f5") return VK_F5;
    if (keyString == "f6") return VK_F6;
    if (keyString == "f7") return VK_F7;
    if (keyString == "f8") return VK_F8;
    if (keyString == "f9") return VK_F9;
    if (keyString == "f10") return VK_F10;
    if (keyString == "f11") return VK_F11;
    if (keyString == "f12") return VK_F12;
    
    // Special keys
    if (keyString == "enter" || keyString == "return") return VK_RETURN;
    if (keyString == "space") return VK_SPACE;
    if (keyString == "tab") return VK_TAB;
    if (keyString == "escape" || keyString == "esc") return VK_ESCAPE;
    if (keyString == "backspace") return VK_BACK;
    if (keyString == "delete") return VK_DELETE;
    if (keyString == "insert") return VK_INSERT;
    if (keyString == "home") return VK_HOME;
    if (keyString == "end") return VK_END;
    if (keyString == "pageup") return VK_PRIOR;
    if (keyString == "pagedown") return VK_NEXT;
    if (keyString == "up") return VK_UP;
    if (keyString == "down") return VK_DOWN;
    if (keyString == "left") return VK_LEFT;
    if (keyString == "right") return VK_RIGHT;
    
    // Arrow keys
    if (keyString == "up") return VK_UP;
    if (keyString == "down") return VK_DOWN;
    if (keyString == "left") return VK_LEFT;
    if (keyString == "right") return VK_RIGHT;
    
    // Single character
    if (keyString.length() == 1) {
        char c = keyString[0];
        if (c >= 'a' && c <= 'z') {
            return c - 32; // Convert to uppercase
        }
        if (c >= '0' && c <= '9') {
            return c;
        }
    }
    
    return 0;
}
