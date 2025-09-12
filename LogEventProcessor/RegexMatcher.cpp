#include "RegexMatcher.h"
#include <iostream>
#include <algorithm>

RegexMatcher::RegexMatcher() : _matchCount(0) {
    // Set default action callback
    _actionCallback = [this](const LogEventPtr& event, const RegexRule& rule, const std::smatch& matches) {
        defaultAction(event, rule, matches);
    };
}

RegexMatcher::~RegexMatcher() {
}

void RegexMatcher::addRule(const RegexRule& rule) {
    _rules.push_back(rule);
    compilePatterns();
}

void RegexMatcher::addRule(const std::string& name, const std::string& pattern, 
                          const std::string& description, bool enabled) {
    _rules.emplace_back(name, pattern, description, enabled, 0);
    compilePatterns();
}

void RegexMatcher::addRule(const std::string& name, const std::string& pattern,
                          const std::string& description, bool enabled, int cooldownMs) {
    _rules.emplace_back(name, pattern, description, enabled, cooldownMs);
    compilePatterns();
}

bool RegexMatcher::removeRule(const std::string& name) {
    auto it = std::find_if(_rules.begin(), _rules.end(), 
                          [&name](const RegexRule& rule) { return rule.name == name; });
    
    if (it != _rules.end()) {
        size_t index = std::distance(_rules.begin(), it);
        _rules.erase(it);
        _compiledPatterns.erase(_compiledPatterns.begin() + index);
        return true;
    }
    return false;
}

bool RegexMatcher::setRuleEnabled(const std::string& name, bool enabled) {
    auto it = std::find_if(_rules.begin(), _rules.end(), 
                          [&name](const RegexRule& rule) { return rule.name == name; });
    
    if (it != _rules.end()) {
        it->enabled = enabled;
        return true;
    }
    return false;
}

bool RegexMatcher::processEvent(const LogEventPtr& event) {
    if (!event) {
        return false;
    }
    
    bool anyMatch = false;
    
    for (size_t i = 0; i < _rules.size(); ++i) {
        if (!_rules[i].enabled) {
            continue;
        }
        
        std::smatch matches;
        if (std::regex_search(event->data, matches, _compiledPatterns[i])) {
            if (_actionCallback) {
                _actionCallback(event, _rules[i], matches);
            }
            _matchCount++;
            anyMatch = true;
        }
    }
    
    return anyMatch;
}

void RegexMatcher::setActionCallback(ActionCallback callback) {
    _actionCallback = callback;
}

const RegexRule* RegexMatcher::getRule(size_t index) const {
    if (index < _rules.size()) {
        return &_rules[index];
    }
    return nullptr;
}

const RegexRule* RegexMatcher::getRuleByName(const std::string& name) const {
    for (const auto& r : _rules) {
        if (r.name == name) return &r;
    }
    return nullptr;
}

void RegexMatcher::clearRules() {
    _rules.clear();
    _compiledPatterns.clear();
}

void RegexMatcher::compilePatterns() {
    _compiledPatterns.clear();
    
    for (const auto& rule : _rules) {
        try {
            _compiledPatterns.emplace_back(rule.pattern, std::regex_constants::ECMAScript | std::regex_constants::optimize | std::regex_constants::icase);
        } catch (const std::regex_error& e) {
            std::cerr << "Error compiling regex pattern '" << rule.pattern 
                     << "' for rule '" << rule.name << "': " << e.what() << std::endl;
            // Add an empty regex as placeholder
            _compiledPatterns.emplace_back("");
        }
    }
}

void RegexMatcher::defaultAction(const LogEventPtr& event, const RegexRule& rule, const std::smatch& matches) {
    std::cout << "[MATCH] Rule: " << rule.name;
    if (!rule.description.empty()) {
        std::cout << " (" << rule.description << ")";
    }
    std::cout << " | Line " << event->lineNumber << ": " << event->data << std::endl;
    
    // Print capture groups if any
    if (matches.size() > 1) {
        std::cout << "  Capture groups:";
        for (size_t i = 1; i < matches.size(); ++i) {
            std::cout << " [" << i << "]=" << matches[i].str();
        }
        std::cout << std::endl;
    }
}
