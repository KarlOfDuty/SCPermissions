using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using SCPermissions.Properties;
using Smod2;
using Smod2.API;
using Smod2.Attributes;
using Smod2.Commands;
using Smod2.Config;
using Smod2.Permissions;
using Smod2.Piping;
using YamlDotNet.Serialization;

namespace SCPermissions
{
    [PluginDetails(
        author = "Karl Essinger",
        name = "SCPermissions",
        description = "A permissions system. Secure, Contain, Permit.",
        id = "karlofduty.scpermissions",
        version = "0.4.3",
        SmodMajor = 3,
        SmodMinor = 4,
        SmodRevision = 1
    )]
    public class SCPermissions : Plugin, IPermissionsHandler
    {
        // Contains all registered players steamID and list of the ranks they have
        private Dictionary<string, HashSet<string>> playerRankDict = new Dictionary<string, HashSet<string>>();

		// Same as above but is not saved to file
		private Dictionary<string, HashSet<string>> tempPlayerRankDict = new Dictionary<string, HashSet<string>>();

		// A json object representing the permissions section in the config
		private JObject permissions = null;

        // Other config options
        public bool verbose = false;
        public bool debug = false;
        private string defaultRank = "";

		private HashSet<string> GetPlayerRanks(string steamID)
		{
			HashSet<string> ranks = new HashSet<string>();

			if (playerRankDict.ContainsKey(steamID))
			{
				ranks.UnionWith(playerRankDict[steamID]);
			}

			if (tempPlayerRankDict.ContainsKey(steamID))
			{
				ranks.UnionWith(tempPlayerRankDict[steamID]);
			}

			if (defaultRank != "")
			{
				ranks.Add(defaultRank);
			}
			return ranks;
		}

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
                this.Warn("Tried to check permission node '" + permissionName + "' but permissions had not been loaded yet.");
                return 0;
            }

			HashSet<string> playerRanks = GetPlayerRanks(steamID);

            // Check if player has any ranks
            if (playerRanks.Count > 0)
            {
                this.Debug("Ranks: " + string.Join(", ", playerRanks));
                // Check every rank from the rank system in the order they are registered in the config until an instance of the permission is found
                JProperty[] allRanks = permissions.Properties().ToArray();
                foreach (JProperty rank in allRanks)
                {
                    // Check if the player has the rank
                    if (playerRanks.Contains(rank.Name) || rank.Name == defaultRank)
                    {
                        try
                        {
                            JToken permissionNode = permissions.SelectToken(rank.Name + "['" + permissionName + "']");
                            if (permissionNode != null)
                            {
                                // Returns 1 if permission is allowed, returns -1 if permission is forbidden
                                if (permissionNode.Value<bool>())
                                {
                                    this.Debug("Returned 1 from " + rank.Name);
                                    return 1;
                                }
                                else
                                {
                                    this.Debug("Returned -1 from " + rank.Name);
                                    return -1;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            this.Verbose("Error attempting to parse permission node " + permissionName + " in rank " + rank.Name + ": " + e.Message);
                        }
                    }
                }
            }
            this.Debug("Returned 0");
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
            this.AddCommand("scperms_givetemprank", new GiveTempRankCommand(this));
            this.AddCommand("scperms_removetemprank", new RemoveTempRankCommand(this));
            this.AddCommand("scperms_listranks", new ListRanksCommand(this));
            this.AddCommand("scperms_verbose", new VerboseCommand(this));
            this.AddCommand("scperms_debug", new DebugCommand(this));

            this.AddCommand("scpermissions_reload", new ReloadCommand(this));
            this.AddCommand("scpermissions_giverank", new GiveRankCommand(this));
            this.AddCommand("scpermissions_removerank", new RemoveRankCommand(this));
            this.AddCommand("scpermissions_givetemprank", new GiveTempRankCommand(this));
            this.AddCommand("scpermissions_removetemprank", new RemoveTempRankCommand(this));
            this.AddCommand("scpermissions_listranks", new ListRanksCommand(this));
            this.AddCommand("scpermissions_verbose", new VerboseCommand(this));
            this.AddCommand("scpermissions_debug", new DebugCommand(this));

            LoadConfig();
            LoadPlayerData();
            this.AddEventHandlers(new PlayerJoinHandler(this), Priority.High);
			this.Info("Special Containment Permissions loaded.");
        }


        public override void Register()
        {
            RegisterPermissionsHandler(this);
            this.AddConfig(new ConfigSetting("scperms_config_global", true, true, "Whether or not the config should be placed in the global config directory, by default true."));
            this.AddConfig(new ConfigSetting("scperms_playerdata_global", true, true, "Whether or not the player data file should be placed in the global config directory, by default true."));
        }

		/// <summary>
		/// Loads the plugin config
		/// </summary>
        private void LoadConfig()
        {
            if (!Directory.Exists(FileManager.GetAppFolder(GetConfigBool("scperms_config_global")) + "SCPermissions"))
            {
                Directory.CreateDirectory(FileManager.GetAppFolder(GetConfigBool("scperms_config_global")) + "SCPermissions");
            }

            if (!File.Exists(FileManager.GetAppFolder(GetConfigBool("scperms_config_global")) + "SCPermissions/config.yml"))
            {
                File.WriteAllText(FileManager.GetAppFolder(GetConfigBool("scperms_config_global")) + "SCPermissions/config.yml", Encoding.UTF8.GetString(Resources.config));
            }

            // Reads config contents into FileStream
            FileStream stream = File.OpenRead(FileManager.GetAppFolder(GetConfigBool("scperms_config_global")) + "SCPermissions/config.yml");

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

            this.Info("Config \"" + FileManager.GetAppFolder(GetConfigBool("scperms_config_global")) + "SCPermissions/config.yml\" loaded.");
        }

		/// <summary>
		/// Loads player data
		/// </summary>
        private void LoadPlayerData()
        {
            if (!Directory.Exists(FileManager.GetAppFolder(GetConfigBool("scperms_playerdata_global")) + "SCPermissions"))
            {
                Directory.CreateDirectory(FileManager.GetAppFolder(GetConfigBool("scperms_playerdata_global")) + "SCPermissions");
            }
            if (!File.Exists(FileManager.GetAppFolder(GetConfigBool("scperms_playerdata_global")) + "SCPermissions/playerdata.yml"))
            {
                File.WriteAllText(FileManager.GetAppFolder(GetConfigBool("scperms_playerdata_global")) + "SCPermissions/playerdata.yml", Encoding.UTF8.GetString(Resources.players));
            }

            // Reads config contents into FileStream
            FileStream stream = File.OpenRead(FileManager.GetAppFolder(GetConfigBool("scperms_playerdata_global")) + "SCPermissions/playerdata.yml");

            // Converts the FileStream into a YAML Dictionary object
            IDeserializer deserializer = new DeserializerBuilder().Build();
            playerRankDict = deserializer.Deserialize<Dictionary<string, HashSet<string>>>(new StreamReader(stream)) ?? new Dictionary<string, HashSet<string>>();

            this.Info("Player data \"" + FileManager.GetAppFolder(GetConfigBool("scperms_playerdata_global")) + "SCPermissions/playerdata.yml\" loaded.");
        }

		/// <summary>
		/// Saves player data to file
		/// </summary>
        private void SavePlayerData()
        {
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, HashSet<string>> playerRanks in playerRankDict)
            {
                if(playerRanks.Value.Count > 0)
                {
                    builder.Append(playerRanks.Key + ": [ \"" + string.Join("\", \"", playerRanks.Value) + "\" ]\n");
                }
            }
            File.WriteAllText(FileManager.GetAppFolder(GetConfigBool("scperms_playerdata_global")) + "SCPermissions/playerdata.yml", builder.ToString());
        }

		/// <summary>
		/// Checks if the first player has a higher rank than the second.
		/// </summary>
		/// <param name="highRankSteamID">SteamID of the player assumed to have the higher rank.</param>
		/// <param name="lowRankSteamID">SteamID of the player assumed to have the lower rank.</param>
		/// <returns></returns>
        private bool RankIsHigherThan(string highRankSteamID, string lowRankSteamID)
        {
			HashSet<string> highPlayerRanks = GetPlayerRanks(highRankSteamID);
			HashSet<string> lowPlayerRanks = GetPlayerRanks(lowRankSteamID);

			if (highPlayerRanks.Count == 0)
            {
                return false;
            }

            if(lowPlayerRanks.Count == 0)
            {
                return true;
            }

            JProperty[] ranks = permissions.Properties().ToArray();
            foreach (JProperty rankProperty in ranks)
            {
                // If this rank is found first it is either higher in rank or equal to the other player, so returns false
                if (lowPlayerRanks.Contains(rankProperty.Name))
                {
                    return false;
                }
                else if (highPlayerRanks.Contains(rankProperty.Name))
                {
                    return true;
                }
            }
            return false;
        }

		/// <summary>
		/// Checks if a rank of this name is registered in the config.
		/// </summary>
		/// <param name="rank">The rank to check.</param>
		/// <returns></returns>
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

		/// <summary>
		/// Gives a rank to a player, refreshes their vanilla rank and saves all players ranks to file
		/// </summary>
		/// <param name="steamID">SteamID of the player.</param>
		/// <param name="rank">Rank to grant.</param>
		/// <returns></returns>
		[PipeMethod]
        public bool GiveRank(string steamID, string rank)
        {
            if(!RankExists(rank))
            {
                return false;
            }

            if(!playerRankDict.ContainsKey(steamID))
            {
                playerRankDict.Add(steamID, new HashSet<string>());
            }

			playerRankDict[steamID].Add(rank);
			SavePlayerData();
            RefreshVanillaRank(this.Server.GetPlayers(steamID).FirstOrDefault());
            return true;
        }

		/// <summary>
		/// Gives a temporary rank to a player which is not saved to file and refreshes the vanilla rank
		/// </summary>
		/// <param name="steamID">SteamID of the player.</param>
		/// <param name="rank">Rank to grant.</param>
		/// <returns></returns>
		[PipeMethod]
		public bool GiveTempRank(string steamID, string rank)
		{
			if (!RankExists(rank))
			{
				return false;
			}

			if (!tempPlayerRankDict.ContainsKey(steamID))
			{
				tempPlayerRankDict.Add(steamID, new HashSet<string>());
			}

			tempPlayerRankDict[steamID].Add(rank);
			RefreshVanillaRank(this.Server.GetPlayers(steamID).FirstOrDefault());
			return true;
		}

		/// <summary>
		/// Removes a rank from a player.
		/// </summary>
		/// <param name="steamID">SteamID of the player.</param>
		/// <param name="rank">Rank to remove.</param>
		/// <returns></returns>
		[PipeMethod]
		public bool RemoveRank(string steamID, string rank)
        {
            if (playerRankDict.ContainsKey(steamID))
            {
                if (playerRankDict[steamID].Remove(rank))
                {
                    SavePlayerData();
                    RefreshVanillaRank(this.Server.GetPlayers(steamID).FirstOrDefault());
                    RemoveTempRank(steamID, rank);
                    return true;
                }
            }
            return RemoveTempRank(steamID, rank);
		}

		/// <summary>
		/// Removes a temp rank from a player.
		/// </summary>
		/// <param name="steamID">SteamID of the player.</param>
		/// <param name="rank">Rank to remove.</param>
		/// <returns></returns>
		[PipeMethod]
		public bool RemoveTempRank(string steamID, string rank)
		{
			if (tempPlayerRankDict.ContainsKey(steamID))
			{
				if (tempPlayerRankDict[steamID].Remove(rank))
				{
					SavePlayerData();
					RefreshVanillaRank(this.Server.GetPlayers(steamID).FirstOrDefault());
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Checks if the player should be given any vanilla rank and sets it.
		/// </summary>
		/// <param name="player"></param>
		public void RefreshVanillaRank(Player player)
        {
            if(player == null)
            {
                return;
            }

			this.Debug("Refreshing vanilla ranks for: " + player.Name);

			HashSet<string> playerRanks = GetPlayerRanks(player.SteamId);
            if (playerRanks.Count > 0)
            {
                this.Debug("Ranks: " + string.Join(", ", playerRanks));

                // Check every rank from the rank system in the order they are registered in the config until a vanillarank entry is found
                JProperty[] ranks = permissions.Properties().ToArray();
                foreach (JProperty rankProperty in ranks)
                {
                    // Check if the player has the rank
                    if (playerRanks.Contains(rankProperty.Name) || rankProperty.Name == defaultRank)
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
        }

		/// <summary>
		/// Logs to console if debug mode is on.
		/// </summary>
		/// <param name="message">Message to send.</param>
        public new void Debug(string message)
        {
            if(debug)
            {
                this.Info(message);
            }
        }

		/// <summary>
		/// Logs to console if verbose is on.
		/// </summary>
		/// <param name="message">Message to send.</param>
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
                return "scperms_reload";
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
                return "scperms_giverank <rank> <steamid>";
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

        private class GiveTempRankCommand : ICommandHandler
        {
	        private SCPermissions plugin;

	        public GiveTempRankCommand(SCPermissions plugin)
	        {
		        this.plugin = plugin;
	        }

	        public string GetCommandDescription()
	        {
		        return "Gives a rank to a player for this session only, does not get saved on server restart.";
	        }

	        public string GetUsage()
	        {
		        return "scperms_givetemprank <rank> <steamid>";
	        }

	        public string[] OnCall(ICommandSender sender, string[] args)
	        {
		        try
		        {
			        if (args.Length > 1)
			        {
				        if (sender is Player player)
				        {
					        if (!player.HasPermission("scpermissions.givetemprank"))
					        {
						        return new string[] {"You don't have permission to use that command."};
					        }

					        if (!plugin.RankIsHigherThan(player.SteamId, args[1]))
					        {
						        return new string[]
							        {"You are not allowed to edit players with ranks equal or above your own."};
					        }
				        }

				        if (plugin.GiveTempRank(args[1], args[0]))
				        {
					        return new string[] {"Added the rank " + args[0] + " to " + args[1] + "."};
				        }
				        else
				        {
					        return new string[]
					        {
						        "Could not add that rank. Does the rank not exist or does the player already have it?"
					        };
				        }

			        }
			        else
			        {
				        return new string[] {"Not enough arguments."};
			        }
		        }
		        catch (Exception e)
		        {
			        this.plugin.Error("Error occured: " + e);
			        return new string[] { "Error occured." };
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
                return "scperms_removerank <rank> <steamid>";
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

		private class ListRanksCommand : ICommandHandler
		{
			private SCPermissions plugin;

			public ListRanksCommand(SCPermissions plugin)
			{
				this.plugin = plugin;
			}

			public string GetCommandDescription()
			{
				return "List all registered ranks.";
			}

			public string GetUsage()
			{
				return "scperms_listranks";
			}

			public string[] OnCall(ICommandSender sender, string[] args)
			{
				if (sender is Player player)
				{
					if (!player.HasPermission("scpermissions.listranks"))
					{
						return new string[] { "You don't have permission to use that command." };
					}
				}

				List<string> strings = new List<string>();
				JProperty[] allRanks = this.plugin.permissions.Properties().ToArray();
				foreach (JProperty rank in allRanks)
				{
					strings.Add(rank.Name);
				}
				return new string[] { "Registered ranks: " + string.Join(", ", strings) };
			}
		}

		private class RemoveTempRankCommand : ICommandHandler
		{
			private SCPermissions plugin;

			public RemoveTempRankCommand(SCPermissions plugin)
			{
				this.plugin = plugin;
			}

			public string GetCommandDescription()
			{
				return "Revokes a temporary rank from a player.";
			}

			public string GetUsage()
			{
				return "scperms_removetemprank <rank> <steamid>";
			}

			public string[] OnCall(ICommandSender sender, string[] args)
			{
				if (args.Length > 1)
				{
					if (sender is Player player)
					{
						if (!player.HasPermission("scpermissions.removetemprank"))
						{
							return new string[] { "You don't have permission to use that command." };
						}

						if (!plugin.RankIsHigherThan(player.SteamId, args[1]))
						{
							return new string[] { "You are not allowed to edit players with ranks equal or above your own." };
						}
					}

					if (plugin.RemoveTempRank(args[1], args[0]))
					{
						return new string[] { "Removed the temporary rank " + args[0] + " from " + args[1] + "." };
					}
					else
					{
						return new string[] { "Could not remove that temporary rank. Does the player not have it?" };
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
                return "scperms_verbose";
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
                return "scperms_debug";
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

	/// <summary>
	/// Sets vanilla ranks for joining players
	/// </summary>
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
