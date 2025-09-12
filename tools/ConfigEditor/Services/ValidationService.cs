using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ConfigEditor.Models;

namespace ConfigEditor.Services
{
    public enum ValidationSeverity { Info, Warning, Error }

    public class ValidationIssue
    {
        public ValidationSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? RuleName { get; set; }
    }

    public class ValidationService
    {
        private static readonly HashSet<string> AllowedActionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "keystroke", "command", "text", "spell"
        };

        public IReadOnlyList<ValidationIssue> Validate(ConfigRoot config)
        {
            var issues = new List<ValidationIssue>();

            // General
            if (string.IsNullOrWhiteSpace(config.LogFilePath))
            {
                issues.Add(new ValidationIssue { Severity = ValidationSeverity.Error, Message = "Log file path is required" });
            }
            if (config.PollingIntervalMs <= 0)
            {
                issues.Add(new ValidationIssue { Severity = ValidationSeverity.Error, Message = "Polling interval must be greater than 0" });
            }
            if (config.MaxQueueSize < 0)
            {
                issues.Add(new ValidationIssue { Severity = ValidationSeverity.Error, Message = "Max queue size cannot be negative" });
            }

            // Rules
            var nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in config.RegexRules)
            {
                if (string.IsNullOrWhiteSpace(rule.Name))
                {
                    issues.Add(new ValidationIssue { Severity = ValidationSeverity.Error, RuleName = null, Message = "Rule has empty name" });
                }
                else if (!nameSet.Add(rule.Name))
                {
                    issues.Add(new ValidationIssue { Severity = ValidationSeverity.Error, RuleName = rule.Name, Message = "Duplicate rule name" });
                }

                if (string.IsNullOrWhiteSpace(rule.Pattern))
                {
                    issues.Add(new ValidationIssue { Severity = ValidationSeverity.Error, RuleName = rule.Name, Message = "Pattern is required" });
                }
                else
                {
                    try
                    {
                        _ = new Regex(rule.Pattern);
                    }
                    catch (Exception ex)
                    {
                        issues.Add(new ValidationIssue { Severity = ValidationSeverity.Error, RuleName = rule.Name, Message = $"Invalid regex: {ex.Message}" });
                    }
                }

                if (rule.CooldownMs < 0)
                {
                    issues.Add(new ValidationIssue { Severity = ValidationSeverity.Error, RuleName = rule.Name, Message = "cooldown_ms must be >= 0" });
                }

                if (rule.Actions != null && rule.Actions.Count > 0)
                {
                    // Multi-step overrides single action fields; validate each step
                    for (int i = 0; i < rule.Actions.Count; i++)
                    {
                        var step = rule.Actions[i];
                        if (string.IsNullOrWhiteSpace(step.Type) || !AllowedActionTypes.Contains(step.Type))
                        {
                            issues.Add(new ValidationIssue { Severity = ValidationSeverity.Error, RuleName = rule.Name, Message = $"Step {i + 1}: invalid type" });
                        }
                        if (string.IsNullOrWhiteSpace(step.Value))
                        {
                            issues.Add(new ValidationIssue { Severity = ValidationSeverity.Error, RuleName = rule.Name, Message = $"Step {i + 1}: value is required" });
                        }
                        if (step.DelayMs < 0)
                        {
                            issues.Add(new ValidationIssue { Severity = ValidationSeverity.Error, RuleName = rule.Name, Message = $"Step {i + 1}: delay must be >= 0" });
                        }
                        if (step.Modifiers < 0)
                        {
                            issues.Add(new ValidationIssue { Severity = ValidationSeverity.Error, RuleName = rule.Name, Message = $"Step {i + 1}: modifiers must be >= 0" });
                        }
                    }
                }
                else
                {
                    // Single action
                    if (!string.IsNullOrEmpty(rule.ActionType) || !string.IsNullOrEmpty(rule.ActionValue))
                    {
                        if (string.IsNullOrWhiteSpace(rule.ActionType) || !AllowedActionTypes.Contains(rule.ActionType!))
                        {
                            issues.Add(new ValidationIssue { Severity = ValidationSeverity.Error, RuleName = rule.Name, Message = "Invalid action_type" });
                        }
                        if (string.IsNullOrWhiteSpace(rule.ActionValue))
                        {
                            issues.Add(new ValidationIssue { Severity = ValidationSeverity.Error, RuleName = rule.Name, Message = "action_value is required when action_type is set" });
                        }
                        if (rule.Modifiers < 0)
                        {
                            issues.Add(new ValidationIssue { Severity = ValidationSeverity.Error, RuleName = rule.Name, Message = "modifiers must be >= 0" });
                        }
                    }
                }
            }

            return issues;
        }
    }
}


