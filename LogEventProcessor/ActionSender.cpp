#include "ActionSender.h"
#include <iostream>
#include <algorithm>
#include <thread>
#include <chrono>
#include <tlhelp32.h>
#include <map>

ActionSender::ActionSender() 
    : _isReady(false), _successCount(0), _failureCount(0), 
      _processId(0), _windowHandle(NULL), _processHandle(NULL) {
}

ActionSender::~ActionSender() {
    if (_processHandle && _processHandle != INVALID_HANDLE_VALUE) {
        CloseHandle(_processHandle);
    }
}

bool ActionSender::initialize() {
    std::lock_guard<std::mutex> lock(_mutex);
    
    if (!findProcess()) {
        std::cerr << "Error: Could not find eqgame.exe process" << std::endl;
        return false;
    }
    
    if (!findWindow()) {
        std::cerr << "Error: Could not find eqgame.exe window" << std::endl;
        return false;
    }
    
    // Also enumerate all matching targets for multi-instance support
    findAllTargets();
    
    _isReady = true;
    std::cout << "ActionSender initialized successfully. Process ID: " << _processId << std::endl;
    return true;
}

bool ActionSender::findProcess() {
    HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (hSnapshot == INVALID_HANDLE_VALUE) {
        return false;
    }
    
    PROCESSENTRY32 pe32;
    pe32.dwSize = sizeof(PROCESSENTRY32);
    
    if (Process32First(hSnapshot, &pe32)) {
        do {
            if (wcscmp(pe32.szExeFile, L"eqgame.exe") == 0) {
                _processId = pe32.th32ProcessID;
                _processHandle = OpenProcess(PROCESS_ALL_ACCESS, FALSE, _processId);
                CloseHandle(hSnapshot);
                return _processHandle != NULL;
            }
        } while (Process32Next(hSnapshot, &pe32));
    }
    
    CloseHandle(hSnapshot);
    return false;
}

bool ActionSender::findAllTargets() {
    _targets.clear();
    HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (hSnapshot == INVALID_HANDLE_VALUE) {
        return false;
    }
    PROCESSENTRY32 pe32; pe32.dwSize = sizeof(PROCESSENTRY32);
    if (Process32First(hSnapshot, &pe32)) {
        do {
            if (wcscmp(pe32.szExeFile, L"eqgame.exe") == 0) {
                DWORD pid = pe32.th32ProcessID;
                HWND hwndFound = NULL;
                auto data = std::make_pair(pid, &hwndFound);
                EnumWindows([](HWND hwnd, LPARAM lParam) -> BOOL {
                    auto dataPtr = reinterpret_cast<std::pair<DWORD, HWND*>*>(lParam);
                    DWORD procId = 0; GetWindowThreadProcessId(hwnd, &procId);
                    if (procId == dataPtr->first) { *(dataPtr->second) = hwnd; return FALSE; }
                    return TRUE; }, reinterpret_cast<LPARAM>(&data));
                if (hwndFound) {
                    _targets.push_back(Target{pid, hwndFound});
                }
            }
        } while (Process32Next(hSnapshot, &pe32));
    }
    CloseHandle(hSnapshot);
    if (!_targets.empty()) {
        std::cout << "[TARGETS] Found " << _targets.size() << " eqgame.exe instances" << std::endl;
        for (auto& t : _targets) {
            std::cout << "[TARGET] pid=" << t.pid << " hwnd=" << (void*)t.hwnd << std::endl;
        }
    }
    return !_targets.empty();
}

bool ActionSender::findWindow() {
    _windowHandle = NULL;
    
    EnumWindows([](HWND hwnd, LPARAM lParam) -> BOOL {
        ActionSender* sender = reinterpret_cast<ActionSender*>(lParam);
        DWORD processId;
        GetWindowThreadProcessId(hwnd, &processId);
        
        if (processId == sender->_processId) {
            char className[256];
            GetClassNameA(hwnd, className, sizeof(className));
            
            // Look for EverQuest window class
            if (strstr(className, "EverQuest") != nullptr || 
                strstr(className, "EQ") != nullptr ||
                GetWindowTextLengthA(hwnd) > 0) {
                sender->_windowHandle = hwnd;
                return FALSE; // Stop enumeration
            }
        }
        return TRUE; // Continue enumeration
    }, reinterpret_cast<LPARAM>(this));
    
    return _windowHandle != NULL;
}

bool ActionSender::sendKeystroke(int key, int modifiers) {
    if (!_isReady.load()) {
        std::cerr << "ActionSender not ready" << std::endl;
        _failureCount.fetch_add(1);
        return false;
    }
    
    std::lock_guard<std::mutex> lock(_mutex);
    // Multi-instance: iterate all targets (including primary)
    if (_targets.empty()) {
        _targets.push_back(Target{_processId, _windowHandle});
    }
    bool anySuccess = false;
    for (const auto& tgt : _targets) {
        std::cout << "[SEND] Preparing keystroke vk=" << key
                  << " mods=" << modifiers
                  << " pid=" << tgt.pid
                  << " hwnd=" << (void*)tgt.hwnd << std::endl;
        if (!bringToForeground(tgt.hwnd, tgt.pid)) {
            std::cerr << "Failed to bring window to foreground pid=" << tgt.pid << std::endl;
            continue;
        }
    
    bool success = true;
    
    // Send modifier keys down
    if (modifiers & MOD_CONTROL) {
        success &= sendKeyDown(VK_CONTROL);
    }
    if (modifiers & MOD_ALT) {
        success &= sendKeyDown(VK_MENU);
    }
    if (modifiers & MOD_SHIFT) {
        success &= sendKeyDown(VK_SHIFT);
    }
    
        // Send main key using scancodes (games often prefer raw scancodes)
        success &= sendKeyScan(key, false);
        std::this_thread::sleep_for(std::chrono::milliseconds(10));
        success &= sendKeyScan(key, true);
    
    // Send modifier keys up
    if (modifiers & MOD_SHIFT) {
        success &= sendKeyUp(VK_SHIFT);
    }
    if (modifiers & MOD_ALT) {
        success &= sendKeyUp(VK_MENU);
    }
    if (modifiers & MOD_CONTROL) {
        success &= sendKeyUp(VK_CONTROL);
    }
    
        if (success) {
            _successCount.fetch_add(1);
            anySuccess = true;
            std::cout << "[SEND] Keystroke success vk=" << key
                      << " mods=" << modifiers
                      << " pid=" << tgt.pid << std::endl;
        } else {
            _failureCount.fetch_add(1);
            std::cerr << "[SEND] Keystroke FAILED vk=" << key
                      << " mods=" << modifiers
                      << " pid=" << tgt.pid << std::endl;
        }
    }
    return anySuccess;
}

bool ActionSender::sendText(const std::string& text) {
    if (!_isReady.load()) {
        std::cerr << "ActionSender not ready" << std::endl;
        _failureCount.fetch_add(1);
        return false;
    }
    
    std::lock_guard<std::mutex> lock(_mutex);
    
    if (_targets.empty()) {
        _targets.push_back(Target{_processId, _windowHandle});
    }
    bool anySuccess = false;
    for (const auto& tgt : _targets) {
        std::cout << "[SEND] Preparing text len=" << text.size()
                  << " pid=" << tgt.pid
                  << " hwnd=" << (void*)tgt.hwnd
                  << " text='" << text << "'" << std::endl;
        if (!bringToForeground(tgt.hwnd, tgt.pid)) {
            std::cerr << "Failed to bring target window to foreground pid=" << tgt.pid << std::endl;
            continue;
        }
        bool success = true;
        for (char c : text) {
            int vk = charToVk(c);
            if (vk != 0) {
                success &= sendKeyScan(vk, false);
                std::this_thread::sleep_for(std::chrono::milliseconds(10));
                success &= sendKeyScan(vk, true);
                std::this_thread::sleep_for(std::chrono::milliseconds(10));
            }
        }
        if (success) {
            _successCount.fetch_add(1);
            anySuccess = true;
            std::cout << "[SEND] Text success len=" << text.size() << " pid=" << tgt.pid << std::endl;
        } else {
            _failureCount.fetch_add(1);
            std::cerr << "[SEND] Text FAILED len=" << text.size() << " pid=" << tgt.pid << std::endl;
        }
    }
    return anySuccess;
}

bool ActionSender::sendKeystrokeSequence(const std::vector<int>& keys, int modifiers) {
    if (!_isReady.load()) {
        std::cerr << "ActionSender not ready" << std::endl;
        _failureCount.fetch_add(1);
        return false;
    }
    
    std::cout << "[SEND] Preparing keystroke sequence count=" << keys.size()
              << " mods=" << modifiers
              << " pid=" << _processId
              << " hwnd=" << (void*)_windowHandle
              << std::endl;
    bool success = true;
    for (int key : keys) {
        success &= sendKeystroke(key, modifiers);
        std::this_thread::sleep_for(std::chrono::milliseconds(50));
    }
    std::cout << "[SEND] Sequence " << (success ? "success" : "FAILED")
              << " count=" << keys.size() << " pid=" << _processId << std::endl;
    
    return success;
}

bool ActionSender::sendChord(const std::vector<int>& keys, int modifiers, bool pressTogether) {
    if (!_isReady.load()) {
        std::cerr << "ActionSender not ready" << std::endl;
        _failureCount.fetch_add(1);
        return false;
    }

    std::lock_guard<std::mutex> lock(_mutex);
    if (_targets.empty()) {
        _targets.push_back(Target{_processId, _windowHandle});
    }
    bool anySuccess = false;
    for (const auto& tgt : _targets) {
        if (!bringToForeground(tgt.hwnd, tgt.pid)) {
            std::cerr << "Failed to bring target window to foreground pid=" << tgt.pid << std::endl;
            continue;
        }
        bool success = true;
        // Hold modifiers
        if (modifiers & MOD_CONTROL) success &= sendKeyScan(VK_CONTROL, false);
        if (modifiers & MOD_ALT)     success &= sendKeyScan(VK_MENU, false);
        if (modifiers & MOD_SHIFT)   success &= sendKeyScan(VK_SHIFT, false);

        // Press keys
        for (int vk : keys) {
            success &= sendKeyScan(vk, false);
            if (!pressTogether) std::this_thread::sleep_for(std::chrono::milliseconds(10));
        }
        // Release keys in reverse
        for (auto it = keys.rbegin(); it != keys.rend(); ++it) {
            success &= sendKeyScan(*it, true);
            if (!pressTogether) std::this_thread::sleep_for(std::chrono::milliseconds(10));
        }

        // Release modifiers
        if (modifiers & MOD_SHIFT)   success &= sendKeyScan(VK_SHIFT, true);
        if (modifiers & MOD_ALT)     success &= sendKeyScan(VK_MENU, true);
        if (modifiers & MOD_CONTROL) success &= sendKeyScan(VK_CONTROL, true);

        if (success) {
            _successCount.fetch_add(1);
            anySuccess = true;
        } else {
            _failureCount.fetch_add(1);
        }
    }
    return anySuccess;
}

bool ActionSender::sendCommand(const std::string& command) {
    // Send the command as text (EQ commands start with /)
    std::string fullCommand = "/" + command;
    std::cout << "[SEND] Command '/" << command << "' pid=" << _processId << std::endl;
    bool textOk = sendText(fullCommand);
    // Always press Enter after sending a command
    bool enterOk = sendKeystroke(VK_RETURN, 0);
    return textOk && enterOk;
}

bool ActionSender::refreshProcess() {
    std::lock_guard<std::mutex> lock(_mutex);
    
    if (_processHandle && _processHandle != INVALID_HANDLE_VALUE) {
        CloseHandle(_processHandle);
    }
    
    _processId = 0;
    _windowHandle = NULL;
    _isReady = false;
    
    return initialize();
}

bool ActionSender::sendKeyDown(int key, int modifiers) {
    INPUT input = {};
    input.type = INPUT_KEYBOARD;
    input.ki.wVk = key;
    input.ki.dwFlags = 0;
    
    if (modifiers & MOD_CONTROL) {
        input.ki.wVk = VK_CONTROL;
        SendInput(1, &input, sizeof(INPUT));
        input.ki.wVk = key;
    }
    if (modifiers & MOD_ALT) {
        input.ki.wVk = VK_MENU;
        SendInput(1, &input, sizeof(INPUT));
        input.ki.wVk = key;
    }
    if (modifiers & MOD_SHIFT) {
        input.ki.wVk = VK_SHIFT;
        SendInput(1, &input, sizeof(INPUT));
        input.ki.wVk = key;
    }
    
    return SendInput(1, &input, sizeof(INPUT)) == 1;
}

bool ActionSender::sendKeyUp(int key, int modifiers) {
    INPUT input = {};
    input.type = INPUT_KEYBOARD;
    input.ki.wVk = key;
    input.ki.dwFlags = KEYEVENTF_KEYUP;
    
    if (modifiers & MOD_CONTROL) {
        input.ki.wVk = VK_CONTROL;
        SendInput(1, &input, sizeof(INPUT));
        input.ki.wVk = key;
    }
    if (modifiers & MOD_ALT) {
        input.ki.wVk = VK_MENU;
        SendInput(1, &input, sizeof(INPUT));
        input.ki.wVk = key;
    }
    if (modifiers & MOD_SHIFT) {
        input.ki.wVk = VK_SHIFT;
        SendInput(1, &input, sizeof(INPUT));
        input.ki.wVk = key;
    }
    
    return SendInput(1, &input, sizeof(INPUT)) == 1;
}

int ActionSender::charToVk(char c) const {
    if (c >= 'A' && c <= 'Z') {
        return c;
    }
    if (c >= 'a' && c <= 'z') {
        return c - 32; // Convert to uppercase
    }
    if (c >= '0' && c <= '9') {
        return c;
    }
    
    // Special characters
    switch (c) {
        case ' ': return VK_SPACE;
        case '\t': return VK_TAB;
        case '\r':
        case '\n': return VK_RETURN;
        case '.': return VK_DECIMAL;
        case ',': return VK_OEM_COMMA;
        case ';': return VK_OEM_1;
        case '/': return VK_OEM_2;
        case '`': return VK_OEM_3;
        case '[': return VK_OEM_4;
        case '\\': return VK_OEM_5;
        case ']': return VK_OEM_6;
        case '\'': return VK_OEM_7;
        case '-': return VK_OEM_MINUS;
        case '=': return VK_OEM_PLUS;
        default: return 0;
    }
}

int ActionSender::getModifierFlags(int key) const {
    int flags = 0;
    
    if (key >= 'A' && key <= 'Z') {
        flags |= MOD_SHIFT; // Uppercase letters need shift
    }
    
    return flags;
}

bool ActionSender::bringToForeground() {
    if (!_windowHandle) return false;

    // Restore if minimized
    if (IsIconic(_windowHandle)) {
        ShowWindow(_windowHandle, SW_RESTORE);
        std::this_thread::sleep_for(std::chrono::milliseconds(50));
    }

    // Attach thread inputs to bypass foreground lock timeout
    DWORD targetThread = GetWindowThreadProcessId(_windowHandle, nullptr);
    DWORD currentThread = GetCurrentThreadId();
    bool attached = AttachThreadInput(currentThread, targetThread, TRUE);

    std::cout << "[FOCUS] Bringing window to foreground pid=" << _processId
              << " hwnd=" << (void*)_windowHandle << std::endl;
    SetForegroundWindow(_windowHandle);
    SetFocus(_windowHandle);
    SetActiveWindow(_windowHandle);

    if (attached) {
        AttachThreadInput(currentThread, targetThread, FALSE);
    }

    // Verify foreground
    HWND fg = GetForegroundWindow();
    if (fg != _windowHandle) {
        // Try forcing topmost toggle
        SetWindowPos(_windowHandle, HWND_TOPMOST, 0, 0, 0, 0,
                     SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        std::this_thread::sleep_for(std::chrono::milliseconds(10));
        SetWindowPos(_windowHandle, HWND_NOTOPMOST, 0, 0, 0, 0,
                     SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
    }

    std::this_thread::sleep_for(std::chrono::milliseconds(50));
    bool ok = GetForegroundWindow() == _windowHandle;
    std::cout << "[FOCUS] Foreground " << (ok ? "ok" : "FAILED") << " pid=" << _processId << std::endl;
    return ok;
}

bool ActionSender::bringToForeground(HWND hwnd, DWORD pid) {
    if (!hwnd) return false;
    std::cout << "[FOCUS] Bringing window to foreground pid=" << pid
              << " hwnd=" << (void*)hwnd << std::endl;
    if (IsIconic(hwnd)) {
        ShowWindow(hwnd, SW_RESTORE);
        std::this_thread::sleep_for(std::chrono::milliseconds(50));
    }
    DWORD targetThread = GetWindowThreadProcessId(hwnd, nullptr);
    DWORD currentThread = GetCurrentThreadId();
    bool attached = AttachThreadInput(currentThread, targetThread, TRUE);
    SetForegroundWindow(hwnd);
    SetFocus(hwnd);
    SetActiveWindow(hwnd);
    if (attached) AttachThreadInput(currentThread, targetThread, FALSE);
    HWND fg = GetForegroundWindow();
    if (fg != hwnd) {
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        std::this_thread::sleep_for(std::chrono::milliseconds(10));
        SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
    }
    std::this_thread::sleep_for(std::chrono::milliseconds(50));
    bool ok = GetForegroundWindow() == hwnd;
    std::cout << "[FOCUS] Foreground " << (ok ? "ok" : "FAILED") << " pid=" << pid << std::endl;
    return ok;
}

bool ActionSender::sendKeyScan(int vk, bool keyUp) const {
    // Map virtual key to scancode
    UINT scan = MapVirtualKeyA((UINT)vk, MAPVK_VK_TO_VSC);
    if (scan == 0) return false;

    INPUT input = {};
    input.type = INPUT_KEYBOARD;
    input.ki.wVk = 0; // Using scancode
    input.ki.wScan = (WORD)scan;
    input.ki.dwFlags = KEYEVENTF_SCANCODE | (keyUp ? KEYEVENTF_KEYUP : 0);
    return SendInput(1, &input, sizeof(INPUT)) == 1;
}

void ActionSender::sendModifiers(int modifiers, bool keyUp) {
    if (modifiers & MOD_CONTROL) {
        sendKeyScan(VK_CONTROL, keyUp);
    }
    if (modifiers & MOD_ALT) {
        sendKeyScan(VK_MENU, keyUp);
    }
    if (modifiers & MOD_SHIFT) {
        sendKeyScan(VK_SHIFT, keyUp);
    }
}

