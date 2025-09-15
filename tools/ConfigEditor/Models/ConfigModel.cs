using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ConfigEditor.Models
{
    public class ConfigRoot
    {
        [YamlMember(Alias = "log_file_path")] public string? LogFilePath { get; set; } = string.Empty;
        [YamlMember(Alias = "output_directory")] public string? OutputDirectory { get; set; } = "./output";
        [YamlMember(Alias = "polling_interval_ms")] public int PollingIntervalMs { get; set; } = 1000;
        [YamlMember(Alias = "parallel_processing")] public bool ParallelProcessing { get; set; } = false;
        [YamlMember(Alias = "debug_mode")] public bool DebugMode { get; set; } = false;
        [YamlMember(Alias = "max_queue_size")] public int MaxQueueSize { get; set; } = 0;
        [YamlMember(Alias = "process_errors")] public bool ProcessErrors { get; set; } = true;
        [YamlMember(Alias = "process_warnings")] public bool ProcessWarnings { get; set; } = true;
        [YamlMember(Alias = "process_info")] public bool ProcessInfo { get; set; } = true;

        // Email configuration for SMS action type
        [YamlMember(Alias = "email_smtp_server")] public string? EmailSmtpServer { get; set; }
        [YamlMember(Alias = "email_smtp_port")] public int EmailSmtpPort { get; set; } = 587;
        [YamlMember(Alias = "email_username")] public string? EmailUsername { get; set; }
        [YamlMember(Alias = "email_password")] public string? EmailPassword { get; set; }
        [YamlMember(Alias = "email_from")] public string? EmailFrom { get; set; }
        [YamlMember(Alias = "email_to")] public string? EmailTo { get; set; }
        [YamlMember(Alias = "email_enable_ssl")] public bool EmailEnableSsl { get; set; } = true;
        [YamlMember(Alias = "email_poll_interval_seconds")] public int EmailPollIntervalSeconds { get; set; } = 30;

        [YamlMember(Alias = "regex_rules")] public List<RegexRule> RegexRules { get; set; } = new List<RegexRule>();
    }

    public class RegexRule : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _pattern = string.Empty;
        private string? _actionType;
        private string? _actionValue;
        private int _modifiers;
        private bool _enabled = true;
        private int _cooldownMs;
        private List<ActionStep>? _actions;

        [YamlMember(Alias = "name")] public string Name { get => _name; set => SetField(ref _name, value); }
        [YamlMember(Alias = "pattern")] public string Pattern { get => _pattern; set => SetField(ref _pattern, value); }

        [YamlMember(Alias = "action_type")] public string? ActionType { get => _actionType; set => SetField(ref _actionType, value); }
        [YamlMember(Alias = "action_value")] public string? ActionValue { get => _actionValue; set => SetField(ref _actionValue, value); }
        [YamlMember(Alias = "modifiers")] public int Modifiers { get => _modifiers; set => SetField(ref _modifiers, value); }
        [YamlMember(Alias = "enabled")] public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }
        [YamlMember(Alias = "cooldown_ms")] public int CooldownMs { get => _cooldownMs; set => SetField(ref _cooldownMs, value); }

        // Optional multi-step support
        [YamlMember(Alias = "actions")] public List<ActionStep>? Actions { get => _actions; set => SetField(ref _actions, value); }

        public RegexRule Clone()
        {
            return new RegexRule
            {
                Name = Name,
                Pattern = Pattern,
                ActionType = ActionType,
                ActionValue = ActionValue,
                Modifiers = Modifiers,
                Enabled = Enabled,
                CooldownMs = CooldownMs,
                Actions = Actions?.Select(a => a.Clone()).ToList()
            };
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class ActionStep : INotifyPropertyChanged
    {
        private string? _type;
        private string? _value;
        private int _modifiers;
        private int _delayMs;
        private bool _enabled = true;

        [YamlMember(Alias = "type")] public string? Type { get => _type; set => SetField(ref _type, value); }
        [YamlMember(Alias = "value")] public string? Value { get => _value; set => SetField(ref _value, value); }
        [YamlMember(Alias = "modifiers")] public int Modifiers { get => _modifiers; set => SetField(ref _modifiers, value); }
        [YamlMember(Alias = "delay_ms")] public int DelayMs { get => _delayMs; set => SetField(ref _delayMs, value); }
        [YamlMember(Alias = "enabled")] public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }

        public ActionStep Clone() => new ActionStep
        {
            Type = Type,
            Value = Value,
            Modifiers = Modifiers,
            DelayMs = DelayMs,
            Enabled = Enabled
        };
        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}


