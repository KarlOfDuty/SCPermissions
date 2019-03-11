using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SCPermissions.Properties;
using ServerMod2.API;
using Smod2;
using Smod2.API;
using Smod2.Attributes;
using Smod2.Commands;
using Smod2.EventHandlers;
using Smod2.Events;
using Smod2.Permissions;
using YamlDotNet.Serialization;

namespace SCPermissions
{
    [PluginDetails(
        author = "Karl Essinger",
        name = "SCPermissions",
        description = "A permissions system. Secure, Contain, Permit.",
        id = "karlofduty.scpermissions",
        version = "0.2.0",
        SmodMajor = 3,
        SmodMinor = 3,
        SmodRevision = 0
    )]
    public class SCPermissions : Plugin, IPermissionsHandler
    {
        // Contains all registered players steamid and list of the ranks they have
        private Dictionary<string, HashSet<string>> playerRanks = new Dictionary<string, HashSet<string>>();

        // A json object representing the permissions section in the config
        private JObject permissions = null;

        // Other config options
        public bool verbose = false;
        public bool debug = false;
        private string defaultRank = "default";

        // Called by the permissions manager when any plugin checks the permissions of a player
        public short CheckPermission(Player player, string permissionName)
        {
            return CheckPermission(player.SteamId, permissionName);
        }

        // I've split this up so I can easily provide a steamid without joining when debugging
        public short CheckPermission(string steamID, string permissionName)
        {
            this.Debug("Checking permission '" + permissionName + "' on " + steamID + ".");

            if (permissions == null)
            {
                this.Warn("Tried to check permision node '" + permissionName + "' but permissions had not been loaded yet.");
                return 0;
            }

            // Check if player is registered in the rank system
            if (playerRanks.ContainsKey(steamID))
            {
                this.Debug("Ranks: " + string.Join(", ", playerRanks[steamID]));
                // Check every rank from the rank system in the order they are registered in the config until an instance of the permission is found
                JProperty[] ranks = permissions.Properties().ToArray();
                foreach (JProperty rankProperty in ranks)
                {
                    // Check if the player has the rank
                    if ((playerRanks[steamID].Contains(rankProperty.Name) || rankProperty.Name == defaultRank))
                    {
                        try
                        {
                            // Checks if the rank has this permission listed single line format
                            JToken singleLiner = permissions.SelectToken(rankProperty.Name + "['" + permissionName + "']");
                            if (singleLiner != null)
                            {
                                // Returns 1 if permission is allowed, returns -1 if permission is forbidden
                                if (singleLiner.Value<bool>())
                                {
                                    this.Debug("Returned singleline 1 for " + rankProperty.Name);
                                    return 1;
                                }
                                else
                                {
                                    this.Debug("Returned singleline -1 for " + rankProperty.Name);
                                    return -1;
                                }
                            }

                            // Checks if the rank has this permission listed in multiline format
                            JToken multiLiner = permissions.SelectToken(rankProperty.Name + "." + permissionName);
                            if (multiLiner != null)
                            {
                                // Returns 1 if permission is allowed, returns -1 if permission is forbidden
                                if (multiLiner.Value<bool>())
                                {
                                    this.Debug("Returned multiline 1 for " + rankProperty.Name);
                                    return 1;
                                }
                                else
                                {
                                    this.Debug("Returned multiline -1 for " + rankProperty.Name);
                                    return -1;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            this.Verbose("Error attempting to parse permission node " + permissionName + " in rank " + rankProperty.Name + ": " + e.Message);
                        }
                    }
                }
            }
            else
            {
                // Checks only the default rank as the player was not registered
                JToken permissionNode = permissions.SelectToken(defaultRank + "." + permissionName);
                if (permissionNode != null)
                {
                    try
                    {
                        // Checks if the rank has this permission listed single line format
                        JToken singleLiner = permissions.SelectToken(defaultRank + "['" + permissionName + "']");
                        if (singleLiner != null)
                        {
                            // Returns 1 if permission is allowed, returns -1 if permission is forbidden
                            if (singleLiner.Value<bool>())
                            {
                                this.Debug("Returned singleline 1 for default rank " + defaultRank);
                                return 1;
                            }
                            else
                            {
                                this.Debug("Returned singleline -1 for default rank " + defaultRank);
                                return -1;
                            }
                        }

                        // Checks if the rank has this permission listed in multiline format
                        JToken multiLiner = permissions.SelectToken(defaultRank + "." + permissionName);
                        if (multiLiner != null)
                        {
                            // Returns 1 if permission is allowed, returns -1 if permission is forbidden
                            if (multiLiner.Value<bool>())
                            {
                                this.Debug("Returned multiline 1 for default rank " + defaultRank);
                                return 1;
                            }
                            else
                            {
                                this.Debug("Returned multiline -1 for default rank " + defaultRank);
                                return -1;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        this.Verbose("Error attempting to parse permission node " + permissionName + " in rank " + defaultRank + ": " + e.Message);
                    }
                }
            }
            this.Debug("Returned end 0");
            return 0;
        }

        public override void OnDisable()
        {
            // Useless function
        }

        public override void OnEnable()
        {
            this.AddCommand("scperms_reload", new ReloadCommand(this));
            this.AddCommand("scperms_giverank", new GiveRankCommand(this));
            this.AddCommand("scperms_removerank", new RemoveRankCommand(this));
            this.AddCommand("scperms_verbose", new VerboseCommand(this));
            this.AddCommand("scperms_debug", new DebugCommand(this));

            this.AddCommand("scpermissions_reload", new ReloadCommand(this));
            this.AddCommand("scpermissions_giverank", new GiveRankCommand(this));
            this.AddCommand("scpermissions_removerank", new RemoveRankCommand(this));
            this.AddCommand("scpermissions_verbose", new VerboseCommand(this));
            this.AddCommand("scpermissions_debug", new DebugCommand(this));

            new Task(async () =>
            {
                await Task.Delay(4000);
                LoadConfig();
                LoadPlayerData();
                this.AddEventHandlers(new PlayerJoinHandler(this), Priority.High);
                this.Info("Special Containment Permissions loaded.");
            }).Start();
        }

        public override void Register()
        {
            AddPermissionsHandler(this);
            this.AddConfig(new Smod2.Config.ConfigSetting("scpermissions_config", "config.yml", Smod2.Config.SettingType.STRING, true, "Name of the config file to use, by default 'config.yml'"));
            this.AddConfig(new Smod2.Config.ConfigSetting("scpermissions_config_global", true, Smod2.Config.SettingType.BOOL, true, "Whether or not the config should be placed in the global config directory, by default true."));
            this.AddConfig(new Smod2.Config.ConfigSetting("scpermissions_playerdata", "players.yml", Smod2.Config.SettingType.STRING, true, "Name of the player data file to use, by default 'players.yml'"));
            this.AddConfig(new Smod2.Config.ConfigSetting("scpermissions_playerdata_global", true, Smod2.Config.SettingType.BOOL, true, "Whether or not the player data file should be placed in the global config directory, by default true."));
        }

        private void LoadConfig()
        {
            if (!Directory.Exists(FileManager.GetAppFolder(GetConfigBool("scpermissions_config_global")) + "SCPermissions"))
            {
                Directory.CreateDirectory(FileManager.GetAppFolder(GetConfigBool("scpermissions_config_global")) + "SCPermissions");
            }

            if (!File.Exists(FileManager.GetAppFolder(GetConfigBool("scpermissions_config_global")) + "SCPermissions/" + GetConfigString("scpermissions_config")))
            {
                File.WriteAllText(FileManager.GetAppFolder(GetConfigBool("scpermissions_config_global")) + "SCPermissions/" + GetConfigString("scpermissions_config"), Encoding.UTF8.GetString(Resources.config));
            }

            // Reads config contents into FileStream
            FileStream stream = File.OpenRead(FileManager.GetAppFolder(GetConfigBool("scpermissions_config_global")) + "SCPermissions/" + GetConfigString("scpermissions_config"));

            // Converts the FileStream into a YAML Dictionary object
            IDeserializer deserializer = new DeserializerBuilder().Build();
            object yamlObject = deserializer.Deserialize(new StreamReader(stream));

            // Converts the YAML Dictionary into JSON String
            ISerializer serializer = new SerializerBuilder().JsonCompatible().Build();
            string jsonString = serializer.Serialize(yamlObject);

            JObject json = JObject.Parse(jsonString);

            permissions = (JObject)json.SelectToken("permissions");
            verbose = json.SelectToken("verbose").Value<bool>();
            debug = json.SelectToken("debug").Value<bool>();
            defaultRank = json.SelectToken("defaultRank").Value<string>();

            this.Debug("JSON Actual: " + jsonString);
            this.Info("Config \"" + FileManager.GetAppFolder(GetConfigBool("scpermissions_config_global")) + "SCPermissions/" + GetConfigString("scpermissions_config") + "\" loaded.");
        }

        private void LoadPlayerData()
        {
            if (!Directory.Exists(FileManager.GetAppFolder(GetConfigBool("scpermissions_playerdata_global")) + "SCPermissions"))
            {
                Directory.CreateDirectory(FileManager.GetAppFolder(GetConfigBool("scpermissions_playerdata_global")) + "SCPermissions");
            }
            if (!File.Exists(FileManager.GetAppFolder(GetConfigBool("scpermissions_playerdata_global")) + "SCPermissions/" + GetConfigString("scpermissions_playerdata")))
            {
                File.WriteAllText(FileManager.GetAppFolder(GetConfigBool("scpermissions_playerdata_global")) + "SCPermissions/" + GetConfigString("scpermissions_playerdata"), Encoding.UTF8.GetString(Resources.players));
            }

            // Reads config contents into FileStream
            FileStream stream = File.OpenRead(FileManager.GetAppFolder(GetConfigBool("scpermissions_playerdata_global")) + "SCPermissions/" + GetConfigString("scpermissions_playerdata"));

            // Converts the FileStream into a YAML Dictionary object
            IDeserializer deserializer = new DeserializerBuilder().Build();
            playerRanks = deserializer.Deserialize<Dictionary<string, HashSet<string>>>(new StreamReader(stream));

            this.Info("Player data \"" + FileManager.GetAppFolder(GetConfigBool("scpermissions_playerdata_global")) + "SCPermissions/" + GetConfigString("scpermissions_playerdata") + "\" loaded.");
        }

        private void SavePlayerData()
        {
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, HashSet<string>> playerRanks in playerRanks)
            {
                if(playerRanks.Value.Count > 0)
                {
                    builder.Append(playerRanks.Key + ": [ \"" + string.Join("\", \"", playerRanks.Value) + "\" ]\n");
                }
            }
            File.WriteAllText(FileManager.GetAppFolder(GetConfigBool("scpermissions_playerdata_global")) + "SCPermissions/" + GetConfigString("scpermissions_playerdata"), builder.ToString());
        }

        public bool RankIsHigherThan(string highRankSteamID, string lowRankSteamID)
        {
            if(!playerRanks.ContainsKey(highRankSteamID))
            {
                return false;
            }

            if(!playerRanks.ContainsKey(lowRankSteamID))
            {
                return true;
            }

            JProperty[] ranks = permissions.Properties().ToArray();
            foreach (JProperty rankProperty in ranks)
            {
                // If this rank is found first 
                if (playerRanks[lowRankSteamID].Contains(rankProperty.Name))
                {
                    return false;
                }
                else if (playerRanks[highRankSteamID].Contains(rankProperty.Name))
                {
                    return true;
                }
            }
            return false;
        }

        public bool RankExists(string rank)
        {
            JProperty[] ranks = permissions.Properties().ToArray();
            foreach (JProperty rankProperty in ranks)
            {
                if(rank == rankProperty.Name)
                {
                    return true;
                }
            }
            return false;
        }

        public bool GiveRank(string steamID, string rank)
        {
            if(!RankExists(rank))
            {
                return false;
            }

            if(!playerRanks.ContainsKey(steamID))
            {
                playerRanks.Add(steamID, new HashSet<string>(new string[]{ rank }));
                SavePlayerData();
                RefreshVanillaRank(this.Server.GetPlayers(steamID).FirstOrDefault());
                return true;
            }
            else if (playerRanks[steamID].Add(rank))
            {
                SavePlayerData();
                return true;
            }
            return false;
        }

        public bool RemoveRank(string steamID, string rank)
        {
            if (playerRanks.ContainsKey(steamID))
            {
                if (playerRanks[steamID].Remove(rank))
                {
                    SavePlayerData();
                    RefreshVanillaRank(this.Server.GetPlayers(steamID).FirstOrDefault());
                    return true;
                }
            }
            return false;
        }

        public void RefreshVanillaRank(Player player)
        {
            if(player == null)
            {
                return;
            }

            if (playerRanks.ContainsKey(player.SteamId))
            {
                this.Debug("Ranks: " + string.Join(", ", playerRanks[player.SteamId]));

                // Check every rank from the rank system in the order they are registered in the config until a vanillarank entry is found
                JProperty[] ranks = permissions.Properties().ToArray();
                foreach (JProperty rankProperty in ranks)
                {
                    // Check if the player has the rank
                    if ((playerRanks[player.SteamId].Contains(rankProperty.Name) || rankProperty.Name == defaultRank))
                    {
                        try
                        {
                            // Checks if the rank has a vanillarank entry
                            string vanillarank = permissions.SelectToken(rankProperty.Name + ".vanillarank")?.Value<string>();
                            if (vanillarank != null)
                            {
                                player.SetRank(null, null, vanillarank);
                                this.Debug("Set vanilla rank for " + player.Name + " to " + rankProperty.Name);
                                return;
                            }
                        }
                        catch (Exception e)
                        {
                            this.Verbose("Error attempting to parse vanilla rank entry of rank " + rankProperty.Name + ": " + e.Message);
                        }
                    }
                }
            }
            else
            {
                // Checks only the default rank as the player was not registered
                try
                {
                    // Checks if the rank has a vanillarank entry
                    string vanillarank = permissions.SelectToken(defaultRank + ".vanillarank")?.Value<string>();
                    if (vanillarank != null)
                    {
                        player.SetRank(null, null, vanillarank);
                        this.Debug("Set vanilla rank for " + player.Name + " to " + defaultRank);
                        return;
                    }
                }
                catch (Exception e)
                {
                    this.Verbose("Error attempting to parse vanilla rank entry of rank " + defaultRank + ": " + e.Message);
                }
            }
        }

        public new void Debug(string message)
        {
            if(debug)
            {
                this.Info(message);
            }
        }

        public void Verbose(string message)
        {
            if (verbose)
            {
                this.Info(message);
            }
        }

        /////////////////////////////////
        // Commands
        ////////////////////////////////
        private class ReloadCommand : ICommandHandler
        {
            private SCPermissions plugin;

            public ReloadCommand(SCPermissions plugin)
            {
                this.plugin = plugin;
            }

            public string GetCommandDescription()
            {
                return "Reloads the config abnd player data.";
            }

            public string GetUsage()
            {
                return "scperm_reload";
            }

            public string[] OnCall(ICommandSender sender, string[] args)
            {
                if (sender is Player player)
                {
                    if (!player.HasPermission("scpermissions.reload"))
                    {
                        return new string[] { "You don't have permission to use that command." };
                    }
                }

                plugin.Info("Reloading plugin...");
                plugin.LoadConfig();
                plugin.LoadPlayerData();
                return new string[] { "Reload complete." };
            }
        }

        private class GiveRankCommand : ICommandHandler
        {
            private SCPermissions plugin;

            public GiveRankCommand(SCPermissions plugin)
            {
                this.plugin = plugin;
            }

            public string GetCommandDescription()
            {
                return "Gives a rank to a player.";
            }

            public string GetUsage()
            {
                return "scperm_giverank <rank> <steamid>";
            }

            public string[] OnCall(ICommandSender sender, string[] args)
            {
                if (args.Length > 1)
                {
                    if (sender is Player player)
                    {
                        if (!player.HasPermission("scpermissions.giverank"))
                        {
                            return new string[] { "You don't have permission to use that command." };
                        }

                        if(!plugin.RankIsHigherThan(player.SteamId, args[1]))
                        {
                            return new string[] { "You are not allowed to edit players with ranks equal or above your own." };
                        }
                    }

                    if (plugin.GiveRank(args[1], args[0]))
                    {
                        return new string[] { "Added the rank " + args[0] + " to " + args[1] + "." };
                    }
                    else
                    {
                        return new string[] { "Could not add that rank. Does the rank not exist or does the player already have it?" };
                    }

                }
                else
                {
                    return new string[] { "Not enough arguments." };
                }
            }
        }

        private class RemoveRankCommand : ICommandHandler
        {
            private SCPermissions plugin;

            public RemoveRankCommand(SCPermissions plugin)
            {
                this.plugin = plugin;
            }

            public string GetCommandDescription()
            {
                return "Revokes a rank from a player.";
            }

            public string GetUsage()
            {
                return "scperm_removerank <rank> <steamid>";
            }

            public string[] OnCall(ICommandSender sender, string[] args)
            {
                if (args.Length > 1)
                {
                    if (sender is Player player)
                    {
                        if (!player.HasPermission("scpermissions.removerank"))
                        {
                            return new string[] { "You don't have permission to use that command." };
                        }

                        if (!plugin.RankIsHigherThan(player.SteamId, args[1]))
                        {
                            return new string[] { "You are not allowed to edit players with ranks equal or above your own." };
                        }
                    }

                    if (plugin.RemoveRank(args[1], args[0]))
                    {
                        return new string[] { "Removed the rank " + args[0] + " from " + args[1] + "." };
                    }
                    else
                    {
                        return new string[] { "Could not remove that rank. Does the player not have it?" };
                    }

                }
                else
                {
                    return new string[] { "Not enough arguments." };
                }
            }
        }

        private class VerboseCommand : ICommandHandler
        {
            private SCPermissions plugin;

            public VerboseCommand(SCPermissions plugin)
            {
                this.plugin = plugin;
            }

            public string GetCommandDescription()
            {
                return "Toggles verbose messages.";
            }

            public string GetUsage()
            {
                return "scperm_verbose";
            }

            public string[] OnCall(ICommandSender sender, string[] args)
            {
                if (sender is Player player)
                {
                    if (!player.HasPermission("scpermissions.verbose"))
                    {
                        return new string[] { "You don't have permission to use that command." };
                    }
                }

                plugin.verbose = !plugin.verbose;
                return new string[] { "Verbose messages: " + plugin.verbose };
            }
        }

        private class DebugCommand : ICommandHandler
        {
            private SCPermissions plugin;

            public DebugCommand(SCPermissions plugin)
            {
                this.plugin = plugin;
            }

            public string GetCommandDescription()
            {
                return "Toggles debug messages.";
            }

            public string GetUsage()
            {
                return "scperm_debug";
            }

            public string[] OnCall(ICommandSender sender, string[] args)
            {
                if (sender is Player player)
                {
                    if (!player.HasPermission("scpermissions.debug"))
                    {
                        return new string[] { "You don't have permission to use that command." };
                    }
                }

                plugin.debug = !plugin.debug;
                return new string[] { "Debug messages: " + plugin.debug };
            }
        }
    }

    internal class PlayerJoinHandler : IEventHandlerPlayerJoin
    {
        private SCPermissions plugin;

        public PlayerJoinHandler(SCPermissions plugin)
        {
            this.plugin = plugin;
        }

        public void OnPlayerJoin(PlayerJoinEvent ev)
        {
            try
            {
                new Task(() =>
                {
                    TestPerms(ev);
                    plugin.RefreshVanillaRank(ev.Player);

                }).Start();
            }
            catch (Exception e)
            {
                plugin.Verbose(e.ToString());
            }
        }

        // Will be removed in version 1.0.0
        private void TestPerms(PlayerJoinEvent ev)
        {
            if(!plugin.debug)
            {
                return;
            }

            if (ev.Player.HasPermission("scpermissions.test1"))
            {
                plugin.Info(ev.Player.Name + " has the test permission.");
            }
            else
            {
                plugin.Info(ev.Player.Name + " doesn't have the test permission.");
            }

            if (ev.Player.HasPermission("scpermissions.test2"))
            {
                plugin.Info(ev.Player.Name + " has the test permission.");
            }
            else
            {
                plugin.Info(ev.Player.Name + " doesn't have the test permission.");
            }

            if (ev.Player.HasPermission("scpermissions.test3"))
            {
                plugin.Info(ev.Player.Name + " has the test permission.");
            }
            else
            {
                plugin.Info(ev.Player.Name + " doesn't have the test permission.");
            }

            if (ev.Player.HasPermission("scpermissions.test4"))
            {
                plugin.Info(ev.Player.Name + " has the test permission.");
            }
            else
            {
                plugin.Info(ev.Player.Name + " doesn't have the test permission.");
            }
        }
    }
}
