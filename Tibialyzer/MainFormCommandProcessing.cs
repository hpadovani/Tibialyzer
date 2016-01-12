﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Numerics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Net;
using System.Text.RegularExpressions;
using System.Data.SQLite;

namespace Tibialyzer {
    public partial class MainForm : Form {
        public ParseMemoryResults lastResults;
        public static char commandSymbol = '@';
        public bool ExecuteCommand(string command, ParseMemoryResults parseMemoryResults = null) {
            if (parseMemoryResults == null) {
                parseMemoryResults = lastResults;
            }
            string comp = command.Trim().ToLower();
            Console.WriteLine(command);
            if (comp.StartsWith("creature" + MainForm.commandSymbol)) { //creature@
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                Creature cr = getCreature(parameter);
                if (cr != null) {
                    ShowCreatureDrops(cr, command);
                } else {
                    List<TibiaObject> creatures = searchCreature(parameter, 50);
                    if (creatures.Count == 1) {
                        ShowCreatureDrops(creatures[0] as Creature, command);
                    } else if (creatures.Count > 1) {
                        ShowCreatureList(creatures, "Creature List", "creature" + MainForm.commandSymbol, command);
                    }
                }
            } else if (comp.StartsWith("look" + MainForm.commandSymbol)) { //look@
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                if (parameter == "on") {
                    if (!settings.ContainsKey("LookMode")) settings.Add("LookMode", new List<string>());
                    settings["LookMode"].Clear(); settings["LookMode"].Add("True");
                    saveSettings();
                } else if (parameter == "off") {
                    if (!settings.ContainsKey("LookMode")) settings.Add("LookMode", new List<string>());
                    settings["LookMode"].Clear(); settings["LookMode"].Add("False");
                    saveSettings();
                } else {
                    List<string> times = getLatestTimes(5);
                    List<TibiaObject> items = new List<TibiaObject>();
                    foreach (string t in times) {
                        if (!totalLooks.ContainsKey(t)) continue;
                        foreach (string message in totalLooks[t]) {
                            string itemName = parseLookItem(message).ToLower();
                            Item item = getItem(itemName);

                            if (item != null) {
                                items.Add(item);
                            } else {
                                Creature cr = getCreature(itemName);
                                if (cr != null) {
                                    items.Add(cr);
                                }
                            }
                        }
                    }
                    if (items.Count == 1) {
                        if (items[0] is Item) {
                            ShowItemNotification("item" + MainForm.commandSymbol + items[0].GetName().ToLower());
                        } else if (items[0] is Creature) {
                            ShowCreatureDrops(items[0] as Creature, command);
                        }
                    } else if (items.Count > 1) {
                        ShowCreatureList(items, "Looked At Items", "item" + MainForm.commandSymbol, command);
                    }
                }
            } else if (comp.StartsWith("stats" + MainForm.commandSymbol)) { //stats@
                string name = command.Split(commandSymbol)[1].Trim().ToLower();
                Creature cr = getCreature(name);
                if (cr != null) {
                    ShowCreatureStats(cr, command);
                }
            } else if (comp.StartsWith("close" + MainForm.commandSymbol)) { //close@
                // close all notifications
                if (tooltipForm != null) {
                    tooltipForm.Close();
                }
                ClearSimpleNotifications();
            } else if (comp.StartsWith("delete" + MainForm.commandSymbol)) { //delete@
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                int killCount;
                if (int.TryParse(parameter, out killCount)) {
                    deleteCreatureWithThreshold(killCount);
                } else {
                    Creature cr = getCreature(parameter);
                    if (cr != null) {
                        deleteCreatureFromLog(cr);
                    }
                }
            } else if (comp.StartsWith("skin" + MainForm.commandSymbol)) { //skin@
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                Creature cr = getCreature(parameter);
                if (cr != null) {
                    insertSkin(cr);
                } else {
                    // find creature with highest killcount with a skin and skin that
                    int kills = -1;
                    foreach (KeyValuePair<Creature, int> kvp in activeHunt.loot.killCount) {
                        if (kvp.Value > kills && kvp.Key.skin != null) {
                            cr = kvp.Key;
                        }
                    }
                    if (cr != null) {
                        insertSkin(cr);
                    }
                }
            } else if (comp.StartsWith("city" + MainForm.commandSymbol)) { //city@
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                if (cityNameMap.ContainsKey(parameter)) {
                    City city = cityNameMap[parameter];
                    ShowCityDisplayForm(city, command);
                }
            } else if (comp.StartsWith("damage" + MainForm.commandSymbol) && parseMemoryResults != null) { //damage@
                string[] splits = command.Split(commandSymbol);
                string screenshot_path = "";
                string parameter = splits[1].Trim().ToLower();
                if (parameter == "screenshot" && splits.Length > 2) {
                    parameter = "";
                    screenshot_path = splits[2];
                }
                ShowDamageMeter(parseMemoryResults.damagePerSecond, command, parameter, screenshot_path);
            } else if (comp.StartsWith("exp" + MainForm.commandSymbol)) { //exp@
                string title = "Experience";
                string text = "Currently gaining " + (parseMemoryResults == null ? "unknown" : ((int)parseMemoryResults.expPerHour).ToString()) + " experience an hour.";
                Image image = tibia_image;
                if (!lootNotificationRich) {
                    ShowSimpleNotification(title, text, image);
                } else {
                    ShowSimpleNotification(new SimpleTextNotification(null, title, text));
                }
            } else if (comp.StartsWith("loot" + MainForm.commandSymbol) || comp.StartsWith("clipboard" + MainForm.commandSymbol)) { //loot@ clipboard@
                string[] splits = command.Split(commandSymbol);
                bool clipboard = comp.StartsWith("clipboard" + MainForm.commandSymbol);
                string screenshot_path = "";
                string parameter = splits[1].Trim().ToLower();
                if (parameter == "screenshot" && splits.Length > 2) {
                    parameter = "";
                    screenshot_path = splits[2];
                }

                bool raw = parameter == "raw"; // raw mode means 'display everything and don't convert anything to gold'
                bool all = parameter == "all" || raw; //all mode means 'display everything'

                // first handle creature kills
                string[] creatures = activeHunt.trackedCreatures.Split('\n');
                for (int i = 0; i < creatures.Length; i++) {
                    creatures[i] = creatures[i].ToLower();
                }

                Dictionary<Creature, int> creatureKills;
                Creature lootCreature = getCreature(parameter);
                if (lootCreature != null) {
                    //the command is loot@<creature>, so we only display the kills and loot from the specified creature
                    creatureKills = new Dictionary<Creature, int>();
                    if (activeHunt.loot.killCount.ContainsKey(lootCreature)) {
                        creatureKills.Add(lootCreature, activeHunt.loot.killCount[lootCreature]);
                    } else {
                        return true; // if there are no kills of the specified creature, just skip the command
                    }
                } else if (activeHunt.trackAllCreatures || activeHunt.trackedCreatures.Length == 0) {
                    creatureKills = activeHunt.loot.killCount; //display all creatures
                } else {
                    // only display tracked creatures
                    creatureKills = new Dictionary<Creature, int>();
                    foreach (string creature in creatures) {
                        Creature cr = getCreature(creature.ToLower());
                        if (cr == null) continue;
                        if (!activeHunt.loot.killCount.ContainsKey(cr)) continue;

                        creatureKills.Add(cr, activeHunt.loot.killCount[cr]);
                    }

                }

                // now handle item drops, gather a count for every item
                List<Tuple<Item, int>> itemDrops = new List<Tuple<Item, int>>();
                Dictionary<Item, int> itemCounts = new Dictionary<Item, int>();
                foreach (KeyValuePair<Creature, Dictionary<Item, int>> kvp in activeHunt.loot.creatureLoot) {
                    if (lootCreature != null && kvp.Key != lootCreature) continue; // if lootCreature is specified, only consider loot from the specified creature
                    if (!activeHunt.trackAllCreatures && activeHunt.trackedCreatures.Length > 0 && !creatures.Contains(kvp.Key.name.ToLower())) continue;
                    foreach (KeyValuePair<Item, int> kvp2 in kvp.Value) {
                        Item item = kvp2.Key;
                        int value = kvp2.Value;
                        if (!itemCounts.ContainsKey(item)) itemCounts.Add(item, value);
                        else itemCounts[item] += value;
                    }
                }
                // now we do item conversion
                int extraGold = 0;
                foreach (KeyValuePair<Item, int> kvp in itemCounts) {
                    Item item = kvp.Key;
                    int count = kvp.Value;
                    // discard items that are set to be discarded (as long as all/raw mode is not enabled)
                    if (item.discard && !all) continue;
                    // convert items to gold (as long as raw mode is not enabled), always gather up all the gold coins found
                    if ((!raw && item.convert_to_gold) || item.name == "gold coin" || item.name == "platinum coin" || item.name == "crystal coin") {
                        extraGold += Math.Max(item.actual_value, item.vendor_value) * count;
                    } else {
                        itemDrops.Add(new Tuple<Item, int>(item, count));
                    }
                }

                // handle coin drops, we always convert the gold to the highest possible denomination (so if gold = 10K, we display a crystal coin)
                int currentGold = extraGold;
                if (currentGold > 10000) {
                    itemDrops.Add(new Tuple<Item, int>(getItem("crystal coin"), currentGold / 10000));
                    currentGold = currentGold % 10000;
                }
                if (currentGold > 100) {
                    itemDrops.Add(new Tuple<Item, int>(getItem("platinum coin"), currentGold / 100));
                    currentGold = currentGold % 100;
                }
                if (currentGold > 0) {
                    itemDrops.Add(new Tuple<Item, int>(getItem("gold coin"), currentGold));
                }

                // now order by value so most valuable items are placed first
                // we use a special value for the gold coins so the gold is placed together in the order crystal > platinum > gold
                // gold coins = <gold total> - 2, platinum coins = <gold total> - 1, crystal coins = <gold total>
                itemDrops = itemDrops.OrderByDescending(o => o.Item1.name == "gold coin" ? extraGold - 2 : (o.Item1.name == "platinum coin" ? extraGold - 1 : (o.Item1.name == "crystal coin" ? extraGold : Math.Max(o.Item1.actual_value, o.Item1.vendor_value) * o.Item2))).ToList();

                if (clipboard) {
                    // Copy loot message to the clipboard
                    // clipboard@<creature> copies the loot of a specific creature to the clipboard
                    // clipboard@ copies all loot to the clipboard
                    string lootString = "";
                    if (creatureKills.Count == 1) {
                        foreach (KeyValuePair<Creature, int> kvp in creatureKills) {
                            lootString = "Total Loot of " + kvp.Value.ToString() + " " + kvp.Key.name + (kvp.Value > 1 ? "s" : "") + ": ";
                        }
                    } else {
                        int totalKills = 0;
                        foreach (KeyValuePair<Creature, int> kvp in creatureKills) {
                            totalKills += kvp.Value;
                        }
                        lootString = "Total Loot of " + totalKills + " Kills: ";
                    }
                    foreach (Tuple<Item, int> kvp in itemDrops) {
                        lootString += kvp.Item2 + " " + kvp.Item1.name + (kvp.Item2 > 1 ? "s" : "") + ", ";
                    }
                    lootString = lootString.Substring(0, lootString.Length - 2) + ".";
                    Clipboard.SetText(lootString);
                } else {
                    // display loot notification
                    ShowLootDrops(creatureKills, itemDrops, command, screenshot_path);
                }
            } else if (comp.StartsWith("reset" + MainForm.commandSymbol)) { //reset@
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                int time = 0;
                if (parameter == "old") {
                    clearOldLog(activeHunt);
                } else if (int.TryParse(parameter, out time) && time > 0) {
                    clearOldLog(activeHunt, time);
                } else {
                    //reset@ loot deletes all loot from the currently active hunt
                    resetHunt(activeHunt);
                }
                refreshHunts();
                ignoreStamp = createStamp();
            } else if (comp.StartsWith("drop" + MainForm.commandSymbol)) { //drop@
                //show all creatures that drop the specified item
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                Item item = getItem(parameter);
                if (item == null) return true;

                List<ItemDrop> itemDrops = new List<ItemDrop>();
                foreach (ItemDrop itemDrop in item.itemdrops) {
                    itemDrops.Add(itemDrop);
                }

                itemDrops.OrderByDescending(o => o.percentage);

                Dictionary<Creature, float> creatures = new Dictionary<Creature, float>();
                foreach (ItemDrop itemDrop in itemDrops) {
                    creatures.Add(getCreature(itemDrop.creatureid), itemDrop.percentage);
                }

                ShowItemView(item, null, null, creatures, command);
            } else if (comp.StartsWith("item" + MainForm.commandSymbol)) { //item@
                //show the item with all the NPCs that sell it
                ShowItemNotification(command);
            } else if (comp.StartsWith("hunt" + MainForm.commandSymbol)) { //hunt@
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                if (cities.Contains(parameter)) {
                    List<HuntingPlace> huntingPlaces = getHuntsInCity(parameter);
                    ShowHuntList(huntingPlaces, "Hunts in " + parameter, command);
                    return true;
                }
                HuntingPlace h = getHunt(parameter);
                if (h != null) {
                    ShowHuntingPlace(h, command);
                    return true;
                }
                Creature cr = getCreature(parameter);
                if (cr != null) {
                    List<HuntingPlace> huntingPlaces = getHuntsForCreature(cr.id);
                    ShowHuntList(huntingPlaces, "Hunts containing creature " + ToTitle(parameter), command);
                    return true;
                }
                int minlevel = -1, maxlevel = -1;
                int level;
                if (int.TryParse(parameter, out level)) {
                    minlevel = (int)(level * 0.8);
                    maxlevel = (int)(level * 1.2);
                } else if (parameter.Contains('-')) {
                    string[] split = parameter.Split('-');
                    int.TryParse(split[0].Trim(), out minlevel);
                    int.TryParse(split[1].Trim(), out maxlevel);
                }
                if (minlevel >= 0 && maxlevel >= 0) {
                    List<HuntingPlace> huntingPlaces = getHuntsForLevels(minlevel, maxlevel);
                    huntingPlaces = huntingPlaces.OrderBy(o => o.level).ToList();
                    ShowHuntList(huntingPlaces, "Hunts between levels " + minlevel.ToString() + "-" + maxlevel.ToString(), command);
                    return true;
                } else {
                    string title;
                    List<HuntingPlace> huntList = searchHunt(parameter);
                    title = "Hunts Containing \"" + parameter + "\"";
                    if (huntList.Count == 1) {
                        ShowHuntGuideNotification(huntList[0], command);
                    } else if (huntList.Count > 1) {
                        ShowHuntList(huntList, title, command);
                    }
                }
            } else if (comp.StartsWith("npc" + MainForm.commandSymbol)) {
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                NPC npc = getNPC(parameter);
                if (npc != null) {
                    ShowNPCForm(npc, command);
                } else if (cities.Contains(parameter)) {
                    ShowCreatureList(getNPCWithCity(parameter), "NPC List", "npc@", command);
                } else {
                    int count = 0;
                    ShowCreatureList(searchNPC(parameter, 50), "NPC List", "npc@", command);
                }
            } else if (comp.StartsWith("savelog" + MainForm.commandSymbol)) {
                saveLog(activeHunt, command.Split(commandSymbol)[1].Trim().Replace("'", "\\'"));
            } else if (comp.StartsWith("loadlog" + MainForm.commandSymbol)) {
                loadLog(activeHunt, command.Split(commandSymbol)[1].Trim().Replace("'", "\\'"));
            } else if (comp.StartsWith("setdiscardgoldratio" + MainForm.commandSymbol)) {
                double val;
                if (double.TryParse(command.Split(commandSymbol)[1].Trim(), out val)) {
                    setGoldRatio(val);
                }
            } else if (comp.StartsWith("wiki" + MainForm.commandSymbol)) {
                string parameter = command.Split(commandSymbol)[1].Trim();
                string response = "";
                using (WebClient client = new WebClient()) {
                    response = client.DownloadString(String.Format("http://tibia.wikia.com/api/v1/Search/List?query={0}&limit=1&minArticleQuality=10&batch=1&namespaces=0", parameter));
                }
                Regex regex = new Regex("\"url\":\"([^\"]+)\"");
                Match m = regex.Match(response);
                var gr = m.Groups[1];
                OpenUrl(gr.Value.Replace("\\/", "/"));
            } else if (comp.StartsWith("char" + MainForm.commandSymbol)) {
                string parameter = command.Split(commandSymbol)[1].Trim();
                OpenUrl("https://secure.tibia.com/community/?subtopic=characters&name=" + parameter);
            } else if (comp.StartsWith("setconvertgoldratio" + MainForm.commandSymbol)) {
                string parameter = command.Split(commandSymbol)[1].Trim();
                string[] split = parameter.Split('-');
                if (split.Length < 2) return true;
                int stackable = 0;
                if (split[0] == "1") stackable = 1;
                double val;
                if (double.TryParse(split[1], out val)) {
                    setConvertRatio(val, stackable == 1);
                }
            } else if (comp.StartsWith("recent" + MainForm.commandSymbol) || comp.StartsWith("url" + MainForm.commandSymbol) || comp.StartsWith("last" + MainForm.commandSymbol)) {
                bool url = comp.StartsWith("url" + MainForm.commandSymbol);
                int type = url ? 1 : 0;
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                if (comp.StartsWith("last" + MainForm.commandSymbol)) parameter = "1";
                List<Command> command_list = getRecentCommands(type).Select(o => new Command() { player = o.Item1, command = o.Item2 }).ToList();
                command_list.Reverse();
                int number;
                //recent@<number> opens the last <number> command, so recent@1 opens the last command
                if (int.TryParse(parameter, out number)) {
                    if (number > 0 && number <= command_list.Count) {
                        ListNotification.OpenCommand(command_list[number - 1].command, type); ;
                        return true;
                    }
                } else {
                    //recent@<player> opens the last 
                    bool found = false;
                    foreach (Command comm in command_list) {
                        if (comm.player.ToLower() == parameter) {
                            ListNotification.OpenCommand(command_list[number].command, type);
                            found = true;
                            break;
                        }
                    }
                    if (found) return true;
                }
                ShowListNotification(command_list, type, command);
            } else if (comp.StartsWith("spell" + MainForm.commandSymbol)) { // spell@
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                Spell spell = getSpell(parameter);
                if (spell != null) {
                    ShowSpellNotification(spell, command);
                } else {
                    List<TibiaObject> spellList = new List<TibiaObject>();
                    string title;
                    if (vocationImages.Keys.Contains(parameter)) {
                        spellList = getSpellsForVocation(parameter);
                        title = ToTitle(parameter) + " Spells";
                    } else {
                        spellList = searchSpell(parameter);
                        title = "Spells Containing \"" + parameter + "\"";
                    }
                    spellList = spellList.OrderBy(o => (o as Spell).levelrequired).ToList<TibiaObject>();
                    if (spellList.Count == 1) {
                        ShowSpellNotification(spellList[0] as Spell, command);
                    } else if (spellList.Count > 1) {
                        ShowCreatureList(spellList, title, "spell" + MainForm.commandSymbol, command);
                    }
                }
            } else if (comp.StartsWith("outfit" + MainForm.commandSymbol)) { // outfit@
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                Outfit outfit = getOutfit(parameter);
                if (outfit != null) {
                    ShowOutfitNotification(outfit, command);
                } else {
                    string title;
                    List<TibiaObject> outfitList = searchOutfit(parameter);
                    title = "Outfits Containing \"" + parameter + "\"";
                    if (outfitList.Count == 1) {
                        ShowOutfitNotification(outfitList[0] as Outfit, command);
                    } else if (outfitList.Count > 1) {
                        ShowCreatureList(outfitList, title, "outfit" + MainForm.commandSymbol, command);
                    }
                }
            } else if (comp.StartsWith("quest" + MainForm.commandSymbol)) { // quest@
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                List<Quest> questList = new List<Quest>();
                if (questNameMap.ContainsKey(parameter)) {
                    ShowQuestNotification(questNameMap[parameter], command);
                } else  {
                    string title;
                    if (cities.Contains(parameter)) {
                        title = "Quests In " + parameter;
                        foreach (Quest q in questIdMap.Values) {
                            if (q.city.ToLower() == parameter) {
                                questList.Add(q);
                            }
                        }
                    } else {
                        title = "Quests Containing \"" + parameter + "\"";
                        string[] splitStrings = parameter.Split(' ');
                        foreach (Quest quest in questIdMap.Values) {
                            bool found = true;
                            foreach (string str in splitStrings) {
                                if (!quest.name.ToLower().Contains(str)) {
                                    found = false;
                                    break;
                                }
                            }
                            if (found) {
                                questList.Add(quest);
                            }
                        }
                    }
                    if (questList.Count == 1) {
                        ShowQuestNotification(questList[0], command);
                    } else if (questList.Count > 1) {
                        ShowQuestList(questList, title, command);
                    }
                }
            } else if (comp.StartsWith("guide" + MainForm.commandSymbol)) { // guide@
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                List<Quest> questList = new List<Quest>();
                if (questNameMap.ContainsKey(parameter)) {
                    ShowQuestGuideNotification(questNameMap[parameter], command);
                } else {
                    string title;
                    foreach (Quest quest in questIdMap.Values) {
                        if (quest.name.ToLower().Contains(parameter)) {
                            questList.Add(quest);
                        }
                    }
                    title = "Quests Containing \"" + parameter + "\"";
                    if (questList.Count == 1) {
                        ShowQuestGuideNotification(questList[0], command);
                    } else if (questList.Count > 1) {
                        ShowQuestList(questList, title, command);
                    }
                }
            } else if (comp.StartsWith("direction" + MainForm.commandSymbol)) { // direction@
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                List<HuntingPlace> huntList = new List<HuntingPlace>();
                HuntingPlace h = getHunt(parameter);
                if (h != null) {
                    ShowHuntGuideNotification(h, command);
                } else {
                    string title;
                    huntList = searchHunt(parameter);
                    title = "Hunts Containing \"" + parameter + "\"";
                    if (huntList.Count == 1) {
                        ShowHuntGuideNotification(huntList[0], command);
                    } else if (huntList.Count > 1) {
                        ShowHuntList(huntList, title, command);
                    }
                }
            } else if (comp.StartsWith("mount" + MainForm.commandSymbol)) { // mount@
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                Mount m = getMount(parameter);
                if (m != null) {
                    ShowMountNotification(m, command);
                } else {
                    string title;
                    List<TibiaObject> mountList = searchMount(parameter);
                    title = "Mounts Containing \"" + parameter + "\"";
                    if (mountList.Count == 1) {
                        ShowMountNotification(mountList[0] as Mount, command);
                    } else if (mountList.Count > 1) {
                        ShowCreatureList(mountList, title, "mount" + MainForm.commandSymbol, command);
                    }
                }
            } else if (comp.StartsWith("pickup" + MainForm.commandSymbol)) {
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                Item item = getItem(parameter);
                if (item != null) { 
                    setItemDiscard(item, false);
                }
            } else if (comp.StartsWith("nopickup" + MainForm.commandSymbol)) {
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                Item item = getItem(parameter);
                if (item != null) {
                    setItemDiscard(item, true);
                }
            } else if (comp.StartsWith("convert" + MainForm.commandSymbol)) {
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                Item item = getItem(parameter);
                if (item != null) {
                    setItemConvert(item, true);
                }
            } else if (comp.StartsWith("noconvert" + MainForm.commandSymbol)) {
                string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
                Item item = getItem(parameter);
                if (item != null) {
                    setItemConvert(item, false);
                }
            } else if (comp.StartsWith("setval" + MainForm.commandSymbol)) {
                string parameter = command.Split(commandSymbol)[1].Trim();
                if (!parameter.Contains('=')) return true;
                string[] split = parameter.Split('=');
                string item = split[0].Trim().ToLower().Replace("'", "\\'");
                int value = 0;
                if (int.TryParse(split[1].Trim(), out value)) {
                    Item it = getItem(parameter);
                    if (it != null) {
                        setItemValue(it, value);
                    }
                }
            } else if (comp.StartsWith("screenshot" + MainForm.commandSymbol)) {
                saveScreenshot("Screenshot", takeScreenshot());
            } else {
                bool found = false;
                foreach (string city in cities) {
                    if (comp.StartsWith(city + MainForm.commandSymbol)) {
                        string itemName = command.Split(commandSymbol)[1].Trim().ToLower();
                        Item item = getItem(itemName);
                        if (item != null) {
                            NPC npc = getNPCSellingItemInCity(item.id, city);
                            if (npc != null) {
                                ShowNPCForm(npc, command);
                            }
                        } else {
                            Spell spell = getSpell(itemName);
                            if (spell != null) {
                                NPC npc = getNPCTeachingSpellInCity(spell.id, city);
                                if (npc != null) {
                                    ShowNPCForm(npc, command);
                                }
                            }
                        }

                        found = true;
                    }
                }
                if (found) return true;
                //if we get here we didn't find any command
                return false;
            }
            return true;
        }

        private bool ScanMemory() {
            ReadMemoryResults readMemoryResults = ReadMemory();
            ParseMemoryResults parseMemoryResults = ParseLogResults(readMemoryResults);

            if (parseMemoryResults != null) {
                lastResults = parseMemoryResults;
            }
            if (readMemoryResults != null && readMemoryResults.newAdvances.Count > 0) {
                if (getSettingBool("AutoScreenshotAdvance")) {
                    this.Invoke((MethodInvoker)delegate {
                        saveScreenshot("Advance", takeScreenshot());
                    });
                }
                if (copyAdvances) {
                    foreach (object obj in readMemoryResults.newAdvances) {
                        this.Invoke((MethodInvoker)delegate {
                            Clipboard.SetText(obj.ToString());
                        });
                    }
                }
                readMemoryResults.newAdvances.Clear();
            }

            if (parseMemoryResults != null && parseMemoryResults.death) {
                if (getSettingBool("AutoScreenshotDeath")) {
                    this.Invoke((MethodInvoker)delegate {
                        saveScreenshot("Death", takeScreenshot());
                    });
                }
                parseMemoryResults.death = false;
            }

            if (getSettingBool("LookMode") && readMemoryResults != null) {
                foreach (string msg in parseMemoryResults.newLooks) {
                    string itemName = parseLookItem(msg).ToLower();
                    if (itemExists(itemName)) {
                        this.Invoke((MethodInvoker)delegate {
                            ShowItemNotification("item@" + itemName);
                        });
                    } else if (creatureExists(itemName) ||
                        (itemName.Contains("dead ") && (itemName = itemName.Replace("dead ", "")) != null && creatureExists(itemName)) ||
                        (itemName.Contains("slain ") && (itemName = itemName.Replace("slain ", "")) != null && creatureExists(itemName))) {
                        this.Invoke((MethodInvoker)delegate {
                            ShowCreatureDrops(getCreature(itemName), "");
                        });
                    } else {
                        NPC npc = getNPC(itemName);
                        if (npc != null) {
                            this.Invoke((MethodInvoker)delegate {
                                ShowNPCForm(npc, "");
                            });
                        }
                    }
                }
                parseMemoryResults.newLooks.Clear();
            }

            List<string> commands = parseMemoryResults == null ? new List<string>() : parseMemoryResults.newCommands.ToArray().ToList();
            commands.Reverse();

            foreach (string command in commands) {
                this.Invoke((MethodInvoker)delegate {
                    if (!ExecuteCommand(command, parseMemoryResults)) {
                        ShowSimpleNotification("Unrecognized command", "Unrecognized command: " + command, tibia_image);
                    }
                });
            }
            if (parseMemoryResults != null) {
                foreach (Tuple<Creature, List<Tuple<Item, int>>> tpl in parseMemoryResults.newItems) {
                    Creature cr = tpl.Item1;
                    List<Tuple<Item, int>> items = tpl.Item2;
                    bool showNotification = false;
                    if (getSettingBool("AlwaysShowLoot")) {
                        // If AlwaysShowLoot is enabled, we always show a notification, as long as the creature is part of the hunts' creature list
                        if (activeHunt.trackAllCreatures) {
                            showNotification = true;
                        } else {
                            string[] creatures = activeHunt.trackedCreatures.Split('\n');
                            for (int i = 0; i < creatures.Length; i++) {
                                creatures[i] = creatures[i].ToLower();
                            }
                            if (creatures.Contains(cr.GetName().ToLower())) {
                                showNotification = true;
                            }
                        }
                    }

                    foreach (Tuple<Item, int> tpl2 in items) {
                        Item item = tpl2.Item1;
                        if ((Math.Max(item.actual_value, item.vendor_value) >= notification_value && showNotificationsValue) || (showNotificationsSpecific && settings["NotificationItems"].Contains(item.name.ToLower()))) {
                            showNotification = true;
                            if (getSettingBool("AutoScreenshotItemDrop")) {
                                // Take a screenshot if Tibialyzer is set to take screenshots of valuable loot
                                Bitmap screenshot = takeScreenshot();
                                if (screenshot == null) continue;
                                // Add a notification to the screenshot
                                SimpleLootNotification screenshotNotification = new SimpleLootNotification(cr, items);
                                Bitmap notification = new Bitmap(screenshotNotification.Width, screenshotNotification.Height);
                                screenshotNotification.DrawToBitmap(notification, new Rectangle(0, 0, screenshotNotification.Width, screenshotNotification.Height));
                                foreach(Control c in screenshotNotification.Controls) {
                                    c.DrawToBitmap(notification, new Rectangle(c.Location, c.Size));
                                }
                                screenshotNotification.Dispose();
                                if (screenshot.Width > notification.Width && screenshot.Height > notification.Height) {
                                    using(Graphics gr = Graphics.FromImage(screenshot)) {
                                        gr.DrawImage(notification, new Point(screenshot.Width - notification.Width, screenshot.Height - notification.Height));
                                    }
                                }
                                notification.Dispose();
                                this.Invoke((MethodInvoker)delegate {
                                    saveScreenshot("Loot", screenshot);
                                });
                            }
                            if (this.showNotifications && !lootNotificationRich) {
                                ShowSimpleNotification(cr.displayname, cr.displayname + " dropped a " + item.displayname + ".", cr.image);
                            }
                        }
                    }
                    if (this.showNotifications && showNotification && lootNotificationRich) {
                        this.Invoke((MethodInvoker)delegate {
                            ShowSimpleNotification(new SimpleLootNotification(cr, items));
                        });
                    }
                }
            }
            return readMemoryResults != null;
        }

        private void ShowItemNotification(string command) {
            string parameter = command.Split(commandSymbol)[1].Trim().ToLower();
            Item item = getItem(parameter);
            if (item == null) {
                int count = 0;
                List<TibiaObject> items = searchItem(parameter, 100);
                if (items.Count == 0) {
                    return;
                } else if (items.Count > 1) {
                    ShowCreatureList(items, "Item List", "item@", command);
                    return;
                } else {
                    item = items[0] as Item;
                }
            }

            Dictionary<NPC, int> sellNPCs = new Dictionary<NPC, int>();
            Dictionary<NPC, int> buyNPCs = new Dictionary<NPC, int>();

            foreach (ItemSold itemSold in item.buyItems) {
                NPC npc = getNPC(itemSold.npcid);
                buyNPCs.Add(npc, itemSold.price);
            }
            foreach (ItemSold itemSold in item.sellItems) {
                NPC npc = getNPC(itemSold.npcid);
                sellNPCs.Add(npc, itemSold.price);
            }

            ShowItemView(item, buyNPCs, sellNPCs, null, command);
        }
    }
}
