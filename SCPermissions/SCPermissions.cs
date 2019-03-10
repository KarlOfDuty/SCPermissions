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
        version = "0.1.0",
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
        private bool verbose = false;
        private bool debug = false;
        private string defaultRank = "default";

        // Called by the permissions manager when any plugin checks the permissions of a player
        public short CheckPermission(Player player, string permissionName)
        {
            return CheckPermission(player.SteamId, permissionName);
        }

        // I've split this up so I can easily provide a steamid when debugging
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
            // TODO: Add rank command
            // TODO: Remove rank comamnd
            // TODO: Reload command

            new Task(async () =>
            {
                await Task.Delay(4000);
                LoadConfig();
                LoadPlayers();
                if (debug)
                {
                    this.AddEventHandlers(new PermissionsTester(this), Priority.High);
                }
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

        private void LoadPlayers()
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

        private void Save()
        {
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, HashSet<string>> playerRanks in playerRanks)
            {
                builder.Append(playerRanks.Key + ": [ \"" + string.Join("\", \"", playerRanks.Value) + "\" ]\n");
            }
            File.WriteAllText(FileManager.GetAppFolder(GetConfigBool("scpermissions_playerdata_global")) + "SCPermissions/" + GetConfigString("scpermissions_playerdata"), builder.ToString());
        }

        public void Reload()
        {

        }

        public bool GiveRank(string steamID, string rank)
        {
            if(playerRanks.ContainsKey(steamID))
            {
                return playerRanks[steamID].Add(rank);
            }
            return false;
        }

        public bool RemoveRank(string steamID, string rank)
        {
            if (playerRanks.ContainsKey(steamID))
            {
                return playerRanks[steamID].Remove(rank);
            }
            return false;
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
    }

    internal class PermissionsTester : IEventHandlerSpawn
    {
        private SCPermissions plugin;

        public PermissionsTester(SCPermissions plugin)
        {
            this.plugin = plugin;
        }

        public void OnSpawn(PlayerSpawnEvent ev)
        {
            if(ev.Player.HasPermission("scpermissions.test1"))
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
