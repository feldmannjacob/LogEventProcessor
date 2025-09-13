using System.IO;
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
            var cfg = _deserializer.Deserialize<ConfigRoot>(yaml) ?? new ConfigRoot();
            if (cfg.RegexRules == null) cfg.RegexRules = new System.Collections.Generic.List<RegexRule>();
            return cfg;
        }

        public void Save(string path, ConfigRoot config)
        {
            var yaml = _serializer.Serialize(config);
            File.WriteAllText(path, yaml);
        }
    }
}


