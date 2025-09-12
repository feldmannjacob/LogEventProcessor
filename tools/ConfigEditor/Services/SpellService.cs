using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HtmlAgilityPack;
using Newtonsoft.Json;
using ConfigEditor.Models;

namespace ConfigEditor.Services
{
    public class SpellService
    {
        private readonly Dictionary<string, IReadOnlyList<Spell>?> _spellCache = new Dictionary<string, IReadOnlyList<Spell>?>();

        public IReadOnlyList<Spell> GetClericSpells() => GetSpells("cleric");
        public IReadOnlyList<Spell> GetShamanSpells() => GetSpells("shaman");
        public IReadOnlyList<Spell> GetDruidSpells() => GetSpells("druid");
        public IReadOnlyList<Spell> GetEnchanterSpells() => GetSpells("enchanter");
        public IReadOnlyList<Spell> GetMagicianSpells() => GetSpells("magician");
        public IReadOnlyList<Spell> GetNecromancerSpells() => GetSpells("necromancer");
        public IReadOnlyList<Spell> GetRangerSpells() => GetSpells("ranger");
        public IReadOnlyList<Spell> GetWizardSpells() => GetSpells("wizard");

        private IReadOnlyList<Spell> GetSpells(string className)
        {
            if (_spellCache.TryGetValue(className, out var cached) && cached != null)
                return cached;

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, "spells", $"{className}_spells.json");
            
            if (!File.Exists(path))
            {
                _spellCache[className] = new List<Spell>();
                return _spellCache[className];
            }

            try
            {
                var all = JsonConvert.DeserializeObject<List<Spell>>(File.ReadAllText(path)) ?? new List<Spell>();
                var filtered = all.Where(s => s.Level <= 70).OrderByDescending(s => s.Level).ThenBy(s => s.Name).ToList();
                _spellCache[className] = filtered;
                return filtered;
            }
            catch 
            { 
                _spellCache[className] = new List<Spell>();
                return _spellCache[className];
            }
        }

        public IReadOnlyList<Spell> GetSpells(string className, int? minLevel, int? maxLevel)
        {
            var spells = GetSpells(className);
            return FilterByLevel(spells, minLevel, maxLevel);
        }

        public IReadOnlyList<Spell> FilterByLevel(IReadOnlyList<Spell> source, int? minLevel, int? maxLevel)
        {
            IEnumerable<Spell> q = source;
            if (minLevel.HasValue) q = q.Where(s => s.Level >= minLevel.Value);
            if (maxLevel.HasValue) q = q.Where(s => s.Level <= maxLevel.Value);
            return q.OrderByDescending(s => s.Level).ThenBy(s => s.Name).ToList();
        }
    }
}
