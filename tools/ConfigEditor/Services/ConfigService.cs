using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ConfigEditor.Models;

namespace ConfigEditor.Services
{
    public class ConfigService
    {
        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;

        public ConfigService()
        {
            _serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
                .Build();

            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
        }

        public ConfigRoot Load(string path)
        {
            using var reader = File.OpenText(path);
            var yaml = reader.ReadToEnd();
            
            // Debug: Log the YAML content before deserialization
            System.Diagnostics.Debug.WriteLine($"ConfigService.Load: YAML content contains target_all_processes: {yaml.Contains("target_all_processes")}");
            if (yaml.Contains("target_all_processes"))
            {
                var lines = yaml.Split('\n');
                var targetLine = lines.FirstOrDefault(l => l.Contains("target_all_processes"));
                System.Diagnostics.Debug.WriteLine($"ConfigService.Load: target_all_processes line: {targetLine}");
            }
            
            var cfg = _deserializer.Deserialize<ConfigRoot>(yaml) ?? new ConfigRoot();
            
            // Debug: Check if the deserialization worked correctly
            System.Diagnostics.Debug.WriteLine($"ConfigService.Load: After deserialization - TargetAll={cfg.TargetAllProcesses}");
            if (cfg.RegexRules == null) cfg.RegexRules = new System.Collections.Generic.List<RegexRule>();
            
            // Convert empty lists to null for process targeting
            if (cfg.TargetProcessIds != null && cfg.TargetProcessIds.Count == 0)
                cfg.TargetProcessIds = null;
            if (cfg.TargetProcessNames != null && cfg.TargetProcessNames.Count == 0)
                cfg.TargetProcessNames = null;
            
            // Debug: Log the loaded process targeting values
            System.Diagnostics.Debug.WriteLine($"ConfigService.Load: Loaded TargetAll={cfg.TargetAllProcesses}, IDs={cfg.TargetProcessIds?.Count ?? 0}, Names={cfg.TargetProcessNames?.Count ?? 0}");
            
            return cfg;
        }

        public void Save(string path, ConfigRoot config)
        {
            var yaml = _serializer.Serialize(config);
            File.WriteAllText(path, yaml);
        }
    }
}


