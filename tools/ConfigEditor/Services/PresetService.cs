using System.Collections.Generic;
using ConfigEditor.Models;

namespace ConfigEditor.Services
{
    public class PresetService
    {
        public string[] GetEqCommands()
        {
            // Based on EverQuest commands list as of Aug 2025
            // Source: https://everquest.allakhazam.com/wiki/eq:commands
            return new[]
            {
                "achievements","advloot","afk","aggro","assist","attack","auction","autobank","autoconsent","autofire","autoinventory","autoskill","autosplit","barter","bazaar","book","bugreport","buyer","camp","cast","channel","charinfo","clearchat","clearallchat","clickthrough","combatmusic","compare","consent","consider","copylayout","corpse","corpsedrag","corpsedrop","decline","disband","discipline","dismount","doability","duel","dzhelp","dzquit","emote","exit","faction","feedback","filter","finditem","findpc","follow","friends","fullscreen","gsay","groupleader","grouproles","guidehelp","help","hideafk","hidecorpses","hidemodels","hotbutton","ignore","inspect","invite","itemoverflow","keys","language","lfgroup","lfguild","loadskin","log","loginterval","loot","lootnodrop","makeleader","makeraidleader","map","marketplace","marknpc","me","melody","memspellset","memspellslot","mercassist","merclog","mercswitch","mercwindows","motd","mousespeed","msg","netstats","note","offlinemode","ooc","open","outputfile","overseer","pet","petition","pickzone","played","queuemelody","quit","raiddisband","random","reply","report","rewind","rmarknpc","roleplay","run","say","send","servertransfer","shadows","shield","shieldgroup","shout","showgrass","shownames","showspelleffects","sit","split","stance","stand","stopcast","stopdisc","stopsocial","stopsong","stoptracking","surname","system","target","targetoftarget","tell","testcopy","time","toggleinspect","trader","tribute","trophy","ttell","useadvlooting","useitem","usercolor","usetarget","viewport","waypoint","who","whotarget","xtarget","yell"
            };
        }

        public IEnumerable<RegexRule> GetPresets()
        {
            // Provide a few helpful starters modeled after your existing examples
            yield return new RegexRule
            {
                Name = "hello_world_response",
                Pattern = ".*hello # world.*",
                ActionType = "command",
                ActionValue = "g # Goodbye",
                Modifiers = 0,
                Enabled = true
            };

            yield return new RegexRule
            {
                Name = "error_detection",
                Pattern = ".*\\[ERROR\\].*",
                ActionType = "keystroke",
                ActionValue = "f1",
                Enabled = true
            };

            yield return new RegexRule
            {
                Name = "warning_detection",
                Pattern = ".*\\[WARNING\\].*",
                ActionType = "keystroke",
                ActionValue = "f2",
                Enabled = true
            };

            yield return new RegexRule
            {
                Name = "attack_minions",
                Pattern = ".*Attack my minions.*",
                Actions = new List<ActionStep>
                {
                    new ActionStep { Type = "keystroke", Value = "Ctrl + 1", Modifiers = 0, DelayMs = 2500, Enabled = true },
                    new ActionStep { Type = "command", Value = "g at delaying", DelayMs = 0, Enabled = true },
                },
                Enabled = true
            };
        }
    }
}


