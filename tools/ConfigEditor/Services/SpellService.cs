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
        private IReadOnlyList<Spell>? _clericSpells;
        private IReadOnlyList<Spell>? _shamanSpells;
        private const string ClericUrl = "https://lucy.allakhazam.com/spelllist.html?classes=CLR&source=Live";

        public IReadOnlyList<Spell> GetClericSpells()
        {
            if (_clericSpells != null) return _clericSpells;

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dir = Path.Combine(baseDir, "spells");
            Directory.CreateDirectory(dir);
            var cachePath = Path.Combine(dir, "cleric_spells.json");

            // Try cache first
            if (File.Exists(cachePath))
            {
                try
                {
                    var cached = JsonConvert.DeserializeObject<List<Spell>>(File.ReadAllText(cachePath));
                    if (cached != null && cached.Count > 0)
                    {
                        _clericSpells = cached.Where(s => s.Level <= 70).OrderByDescending(s => s.Level).ThenBy(s => s.Name).ToList();
                        return _clericSpells;
                    }
                }
                catch { }
            }

            // Fetch from web
            var web = new HtmlWeb();
            var doc = web.Load(ClericUrl);

            var spells = new List<Spell>();
            // The Lucy table typically contains rows with columns: Spell, Classes, ...
            var rows = doc.DocumentNode.SelectNodes("//table//tr");
            if (rows != null)
            {
                foreach (var tr in rows.Skip(1)) // skip header
                {
                    var tds = tr.SelectNodes("td");
                    if (tds == null || tds.Count < 2) continue;
                    var nameNode = tds[0].SelectSingleNode(".//a") ?? tds[0];
                    var name = HtmlEntity.DeEntitize(nameNode.InnerText).Trim();
                    var classesText = HtmlEntity.DeEntitize(tds[1].InnerText).Trim();
                    // Expect formats like "1: CLR" or "65: CLR/DRU"; we take the first number for CLR
                    int level = ParseClericLevel(classesText);
                    if (level <= 0) continue;
                    // Filter to <= 70 as requested
                    if (level <= 70)
                    {
                        var id = ExtractSpellId(nameNode.GetAttributeValue("href", ""));
                        spells.Add(new Spell { Name = name, Level = level, Id = id });
                    }
                }
            }

            spells = spells.OrderByDescending(s => s.Level).ThenBy(s => s.Name).ToList();
            // Cache
            try { File.WriteAllText(cachePath, JsonConvert.SerializeObject(spells, Formatting.Indented)); } catch { }
            _clericSpells = spells;
            return _clericSpells;
        }

        public IReadOnlyList<Spell> GetShamanSpells()
        {
            if (_shamanSpells != null) return _shamanSpells;
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, "spells", "shaman_spells.json");
            if (!File.Exists(path)) return _shamanSpells = new List<Spell>();
            try
            {
                var all = JsonConvert.DeserializeObject<List<Spell>>(File.ReadAllText(path)) ?? new List<Spell>();
                _shamanSpells = all.Where(s => s.Level <= 70).OrderByDescending(s => s.Level).ThenBy(s => s.Name).ToList();
            }
            catch { _shamanSpells = new List<Spell>(); }
            return _shamanSpells;
        }

        public IReadOnlyList<Spell> FilterByLevel(IReadOnlyList<Spell> source, int? minLevel, int? maxLevel)
        {
            IEnumerable<Spell> q = source;
            if (minLevel.HasValue) q = q.Where(s => s.Level >= minLevel.Value);
            if (maxLevel.HasValue) q = q.Where(s => s.Level <= maxLevel.Value);
            return q.OrderByDescending(s => s.Level).ThenBy(s => s.Name).ToList();
        }

        private static int ParseClericLevel(string classesText)
        {
            // Look for a pattern like "<num>" optionally followed by ':' then contains 'CLR'
            // We'll split on '/' and ',' and spaces to find tokens with numbers and 'CLR'
            // Example snippets: "1: CLR", "65: CLR/DRU"
            var parts = classesText.Split(new[] { '/', ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var s = p.Trim();
                int colon = s.IndexOf(':');
                string left = colon >= 0 ? s.Substring(0, colon).Trim() : new string(s.TakeWhile(char.IsDigit).ToArray());
                string right = colon >= 0 ? s.Substring(colon + 1) : s;
                if (int.TryParse(left, out int lv) && right.ToUpperInvariant().Contains("CLR"))
                {
                    return lv;
                }
            }
            return 0;
        }

        private static int ExtractSpellId(string href)
        {
            // Example href: spell.html?id=12345
            if (string.IsNullOrEmpty(href)) return 0;
            try
            {
                var uri = new Uri(new Uri("https://lucy.allakhazam.com/"), href);
                var query = uri.Query; // ?id=12345
                if (query.StartsWith("?")) query = query.Substring(1);
                foreach (var kv in query.Split('&'))
                {
                    var parts = kv.Split('=');
                    if (parts.Length == 2 && string.Equals(parts[0], "id", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(parts[1], out int id)) return id;
                    }
                }
            }
            catch { }
            return 0;
        }
    }
}
