using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HtmlAgilityPack;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
// using System.Net;
// using System.Net.Http;

class Program
{
    static void Main(string[] args)
    {
        // Define all spell classes and their directories
        var spellClasses = new Dictionary<string, string>
        {
            { "CLR", @"C:\Users\Jake\Downloads\Spells\Cleric Spells" },
            { "SHM", @"C:\Users\Jake\Downloads\Spells\Shaman Spells" },
            { "DRU", @"C:\Users\Jake\Downloads\Spells\Druid Spells" },
            { "ENC", @"C:\Users\Jake\Downloads\Spells\Enchanter Spells" },
            { "MAG", @"C:\Users\Jake\Downloads\Spells\Magician Spells" },
            { "NEC", @"C:\Users\Jake\Downloads\Spells\Necromancer Spells" },
            { "RNG", @"C:\Users\Jake\Downloads\Spells\Ranger Spells" },
            { "WIZ", @"C:\Users\Jake\Downloads\Spells\Wizard Spells" }
        };

        var outBase = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "tools", "ConfigEditor", "spells"));
        Directory.CreateDirectory(outBase);

        foreach (var kvp in spellClasses)
        {
            var classCode = kvp.Key;
            var classDir = kvp.Value;
            var className = GetClassName(classCode);
            
            Console.WriteLine($"Processing {className} spells...");
            var spells = ParseLocalDir(classDir, classCode);
            
            var outputFile = Path.Combine(outBase, $"{className.ToLower()}_spells.json");
            File.WriteAllText(outputFile, JsonConvert.SerializeObject(spells, Formatting.Indented));
            Console.WriteLine($"Wrote {spells.Count} {className.ToLower()} spells to {outputFile}");
        }
    }

    static string GetClassName(string classCode)
    {
        return classCode switch
        {
            "CLR" => "cleric",
            "SHM" => "shaman", 
            "DRU" => "druid",
            "ENC" => "enchanter",
            "MAG" => "magician",
            "NEC" => "necromancer",
            "RNG" => "ranger",
            "WIZ" => "wizard",
            _ => classCode.ToLower()
        };
    }

    class SpellRow { public int id; public string name = ""; public int level; }

    static List<SpellRow> ParseLocalDir(string dir, string classCode)
    {
        var results = new List<SpellRow>();
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return results;
        foreach (var file in Directory.EnumerateFiles(dir, "*.htm*") )
        {
            try
            {
                var html = File.ReadAllText(file);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var trs = doc.DocumentNode.SelectNodes("//tr");
                int rows = 0, added = 0, nameLinks = 0;
                if (trs == null)
                {
                    Console.WriteLine($"[SCRAPER] {classCode}: {Path.GetFileName(file)} rows=0 nameLinks=0 added=0");
                }
                else
                {
                    foreach (var tr in trs)
                    {
                        rows++;
                        var tds = tr.SelectNodes("td");
                        if (tds == null || tds.Count < 4) continue;
                        var nameLink = tds[1].SelectSingleNode(".//a[contains(@href,'spell.html')]");
                        if (nameLink == null) continue;
                        var href = nameLink.GetAttributeValue("href", "");
                        // Ensure it's a spell link with id param
                        if (!href.Contains("spell.html") || !href.Contains("id=")) continue;
                        nameLinks++;
                        int id = 0;
                        try
                        {
                            var uri = new Uri(href, UriKind.RelativeOrAbsolute);
                            if (!uri.IsAbsoluteUri) uri = new Uri(new Uri("https://lucy.allakhazam.com/"), href);
                            var query = uri.Query.TrimStart('?').Split('&');
                            foreach (var kv in query)
                            {
                                var parts = kv.Split('=');
                                if (parts.Length == 2 && parts[0] == "id") { int.TryParse(parts[1], out id); break; }
                            }
                        }
                        catch { }
                        if (id <= 0) continue;
                        var name = HtmlEntity.DeEntitize(nameLink.InnerText).Trim();
                        var classesText = HtmlEntity.DeEntitize(tds[3].InnerText).Replace('\n', ' ').Replace("&nbsp;", " ");
                        var pairMatches = Regex.Matches(classesText, @"\b([A-Z]{3})\s*/\s*(\d{1,3})\b");
                        if (pairMatches.Count == 0) continue;
                        int lvl = -1;
                        foreach (Match pm in pairMatches)
                        {
                            var code = pm.Groups[1].Value.ToUpperInvariant();
                            if (code == classCode.ToUpperInvariant()) { int.TryParse(pm.Groups[2].Value, out lvl); break; }
                        }
                        if (lvl < 0 || lvl > 70) continue;
                        results.Add(new SpellRow { id = id, name = name, level = lvl });
                        added++;
                    }
                    Console.WriteLine($"[SCRAPER] {classCode}: {Path.GetFileName(file)} rows={rows} nameLinks={nameLinks} added={added}");
                }
            }
            catch { }
        }
        return results
            .GroupBy(s => s.id)
            .Select(g => g.OrderBy(x => x.level).ThenBy(x => x.name).First())
            .OrderBy(s => s.level).ThenBy(s => s.name)
            .ToList();
    }
}


