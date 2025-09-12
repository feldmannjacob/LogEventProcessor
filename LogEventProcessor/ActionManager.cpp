#include "ActionManager.h"
#include <iostream>
#include <sstream>
#include <algorithm>
#include <cctype>
#include <regex>
#include <thread>
#include <chrono>

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
    auto& vec = _actionMappings[mapping.ruleName];
    vec.push_back(mapping);
    std::cout << "Added action mapping: " << mapping.ruleName 
              << " -> " << mapping.actionType << ":" << mapping.actionValue << std::endl;
}

void ActionManager::addActionMapping(const std::string& ruleName, const std::string& actionType,
                                   const std::string& actionValue, int modifiers, bool enabled) {
    addActionMapping(ActionMapping(ruleName, actionType, actionValue, modifiers, enabled));
}

void ActionManager::addActionSequence(const std::string& ruleName, const std::vector<ActionMapping>& steps) {
    std::lock_guard<std::mutex> lock(_mutex);
    auto& vec = _actionMappings[ruleName];
    for (const auto& s : steps) {
        vec.push_back(s);
    }
    std::cout << "Added action sequence for rule: " << ruleName << ", steps: " << steps.size() << std::endl;
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
            if (it != _actionMappings.end()) {
                // Prepare sequence with placeholder substitution
                std::vector<ActionMapping> seq;
                seq.reserve(it->second.size());
                // Determine extracted text: first capture group if present, otherwise full match
                std::string extractedText;
                if (matches.size() > 1) {
                    extractedText = matches[1].str();
                } else if (matches.size() > 0) {
                    extractedText = matches[0].str();
                }
                for (const auto& step : it->second) {
                    if (!step.enabled) continue;
                    ActionMapping s = step;
                    if (!extractedText.empty() && s.actionValue.find('#') != std::string::npos) {
                        std::string result;
                        result.reserve(s.actionValue.size() + extractedText.size());
                        for (char c : s.actionValue) {
                            if (c == '#') { result += extractedText; } else { result.push_back(c); }
                        }
                        s.actionValue = result;
                    }
                    seq.push_back(std::move(s));
                }
                if (!seq.empty()) {
                    std::cout << "[ACTION] Rule '" << rule->name << "' matched, executing " << seq.size() << " step(s)" << std::endl;
                    if (executeActions(seq)) {
                        anyMatch = true;
                    }
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
            if (it != _actionMappings.end()) {
                std::string extractedText;
                if (matches.size() > 1) {
                    extractedText = matches[1].str();
                } else if (matches.size() > 0) {
                    extractedText = matches[0].str();
                }
                for (const auto& step : it->second) {
                    if (!step.enabled) continue;
                    ActionMapping s = step;
                    if (!extractedText.empty() && s.actionValue.find('#') != std::string::npos) {
                        std::string result;
                        result.reserve(s.actionValue.size() + extractedText.size());
                        for (char c : s.actionValue) {
                            if (c == '#') { result += extractedText; } else { result.push_back(c); }
                        }
                        s.actionValue = result;
                    }
                    outActions.push_back(std::move(s));
                    any = true;
                }
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
        if (m.delayMs > 0) {
            std::this_thread::sleep_for(std::chrono::milliseconds(m.delayMs));
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
        for (auto& step : it->second) { step.enabled = enabled; }
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
        // Try to parse as chord first (multiple keys)
        std::vector<int> keys; int modifiers = 0;
        if (parseChord(mapping.actionValue, keys, modifiers) && !keys.empty()) {
            if (keys.size() == 1) {
                return _actionSender.sendKeystroke(keys[0], modifiers);
            }
            return _actionSender.sendChord(keys, modifiers, false);
        }
        // Fallback to single key parsing
        int key, singleMods;
        if (parseKeystroke(mapping.actionValue, key, singleMods)) {
            return _actionSender.sendKeystroke(key, singleMods);
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

    // Normalize: lowercase and remove spaces to support formats like "Ctrl + 1"
    std::string normalized = keystrokeString;
    std::transform(normalized.begin(), normalized.end(), normalized.begin(), ::tolower);
    normalized.erase(std::remove_if(normalized.begin(), normalized.end(), [](unsigned char ch){ return std::isspace(ch); }), normalized.end());

    // Split by '+'
    std::vector<std::string> tokens;
    std::string current;
    for (char c : normalized) {
        if (c == '+') {
            if (!current.empty()) { tokens.push_back(current); current.clear(); }
        } else {
            current.push_back(c);
        }
    }
    if (!current.empty()) tokens.push_back(current);

    for (const auto& tok : tokens) {
        if (tok == "ctrl" || tok == "control") { modifiers |= MOD_CONTROL; continue; }
        if (tok == "alt") { modifiers |= MOD_ALT; continue; }
        if (tok == "shift") { modifiers |= MOD_SHIFT; continue; }
        if (key == 0) {
            key = getVirtualKeyCode(tok);
        } else {
            // Multiple non-modifier keys not supported in single keystroke
            // Ignore extras for now
        }
    }

    return key != 0;
}

bool ActionManager::parseChord(const std::string& keystrokeString, std::vector<int>& keys, int& modifiers) {
    keys.clear();
    modifiers = 0;
    std::string normalized = keystrokeString;
    std::transform(normalized.begin(), normalized.end(), normalized.begin(), ::tolower);
    normalized.erase(std::remove_if(normalized.begin(), normalized.end(), [](unsigned char ch){ return std::isspace(ch); }), normalized.end());

    // Split by '+'
    std::vector<std::string> tokens;
    std::string current;
    for (char c : normalized) {
        if (c == '+') {
            if (!current.empty()) { tokens.push_back(current); current.clear(); }
        } else {
            current.push_back(c);
        }
    }
    if (!current.empty()) tokens.push_back(current);

    for (const auto& tok : tokens) {
        if (tok == "ctrl" || tok == "control") { modifiers |= MOD_CONTROL; continue; }
        if (tok == "alt") { modifiers |= MOD_ALT; continue; }
        if (tok == "shift") { modifiers |= MOD_SHIFT; continue; }
        int vk = getVirtualKeyCode(tok);
        if (vk != 0) keys.push_back(vk);
    }
    return !keys.empty();
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
