using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SCPermissions.Properties;
using Smod2;
using Smod2.API;
using Smod2.Attributes;
using Smod2.Commands;
using Smod2.Config;
using Smod2.EventHandlers;
using Smod2.Events;
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
		version = "1.0.4",
		SmodMajor = 3,
		SmodMinor = 10,
		SmodRevision = 0
	)]
	public class SCPermissions : Plugin, IPermissionsHandler
	{
		// Contains all registered players userIDs and list of the ranks they have
		private Dictionary<string, HashSet<string>> playerRankDict = new Dictionary<string, HashSet<string>>();

		// Same as above but is not saved to file
		private Dictionary<string, HashSet<string>> tempPlayerRankDict = new Dictionary<string, HashSet<string>>();

		// A json object representing the permissions section in the config
		private JObject permissions = null;

		// Other config options
		private bool verbose = false;
		private bool debug = false;
		private string defaultRank = "";

		private HashSet<string> GetPlayerRanks(string userID)
		{
			HashSet<string> ranks = new HashSet<string>();

			if (playerRankDict.ContainsKey(userID))
			{
				this.Debug("Permanent ranks: " + string.Join(", ", playerRankDict[userID]));
				ranks.UnionWith(playerRankDict[userID]);
			}
			else
			{
				this.Debug("Player has no permanent ranks.");
			}

			if (tempPlayerRankDict.ContainsKey(userID))
			{
				this.Debug("Temporary ranks: " + string.Join(", ", tempPlayerRankDict[userID]));
				ranks.UnionWith(tempPlayerRankDict[userID]);
			}
			else
			{
				this.Debug("Player has no temporary ranks.");
			}

			if (defaultRank != "")
			{
				ranks.Add(defaultRank);
				this.Debug("Default rank: " + defaultRank);
			}
			else
			{
				this.Debug("No default rank exists.");
			}
			return ranks;
		}

		// Called by the permissions manager when any plugin checks the permissions of a player
		public short CheckPermission(Player player, string permissionName)
		{
			return CheckPermission(player.UserID, permissionName);
		}

		// I've split this up so I can easily provide a userID without joining when debugging
		public short CheckPermission(string userID, string permissionName)
		{
			this.Debug("Checking permission '" + permissionName + "' on " + userID + ".");

			if (permissions == null)
			{
				this.Warn("Tried to check permission node '" + permissionName + "' but permissions had not been loaded yet.");
				return 0;
			}

			HashSet<string> playerRanks = GetPlayerRanks(userID);

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
			this.AddEventHandlers(new PlayerJoinHandler(this), Priority.LATE);
			this.Info("Special Containment Permissions loaded.");
		}

		public override void OnDisable() {}

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
			if (!Directory.Exists(FileManager.GetAppFolder(true, !GetConfigBool("scperms_config_global")) + "SCPermissions"))
			{
				Directory.CreateDirectory(FileManager.GetAppFolder(true, !GetConfigBool("scperms_config_global")) + "SCPermissions");
			}

			if (!File.Exists(FileManager.GetAppFolder(true, !GetConfigBool("scperms_config_global")) + "SCPermissions/config.yml"))
			{
				File.WriteAllText(FileManager.GetAppFolder(true, !GetConfigBool("scperms_config_global")) + "SCPermissions/config.yml", Encoding.UTF8.GetString(Resources.config));
			}

			// Reads config contents into FileStream
			using (FileStream stream = File.OpenRead(FileManager.GetAppFolder(true, !GetConfigBool("scperms_config_global")) + "SCPermissions/config.yml"))
			{
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
    
                this.Info("Config \"" + FileManager.GetAppFolder(true, !GetConfigBool("scperms_config_global")) + "SCPermissions/config.yml\" loaded.");
			}
		}

		/// <summary>
		/// Loads player data
		/// </summary>
		private void LoadPlayerData()
		{
			if (!Directory.Exists(FileManager.GetAppFolder(true, !GetConfigBool("scperms_playerdata_global")) + "SCPermissions"))
			{
				Directory.CreateDirectory(FileManager.GetAppFolder(true, !GetConfigBool("scperms_playerdata_global")) + "SCPermissions");
			}
			if (!File.Exists(FileManager.GetAppFolder(true, !GetConfigBool("scperms_playerdata_global")) + "SCPermissions/playerdata.yml"))
			{
				File.WriteAllText(FileManager.GetAppFolder(true, !GetConfigBool("scperms_playerdata_global")) + "SCPermissions/playerdata.yml", Encoding.UTF8.GetString(Resources.players));
			}

			// Reads config contents into FileStream
			using (FileStream stream = File.OpenRead(FileManager.GetAppFolder(true, !GetConfigBool("scperms_playerdata_global")) + "SCPermissions/playerdata.yml"))
			{
				// Converts the FileStream into a YAML Dictionary object
                IDeserializer deserializer = new DeserializerBuilder().Build();
                playerRankDict = deserializer.Deserialize<Dictionary<string, HashSet<string>>>(new StreamReader(stream)) ?? new Dictionary<string, HashSet<string>>();
    
                this.Info("Player data \"" + FileManager.GetAppFolder(true, !GetConfigBool("scperms_playerdata_global")) + "SCPermissions/playerdata.yml\" loaded.");
			}
		}

		/// <summary>
		/// Saves player data to file
		/// </summary>
		private void SavePlayerData()
		{
			try
			{
				StringBuilder builder = new StringBuilder();
				foreach (KeyValuePair<string, HashSet<string>> playerRanks in playerRankDict)
				{
					if (playerRanks.Value.Count > 0)
					{
						builder.Append(playerRanks.Key + ": [ \"" + string.Join("\", \"", playerRanks.Value) + "\" ]\n");
					}
				}
				File.WriteAllText(FileManager.GetAppFolder(true, !GetConfigBool("scperms_playerdata_global")) + "SCPermissions/playerdata.yml", builder.ToString());
			}
			catch (Exception e)
			{
				this.Error("Exception occured when trying to save player data: " + e);
			}

		}

		/// <summary>
		/// Checks if the first player has a higher rank than the second.
		/// </summary>
		/// <param name="highRankUserID">userID of the player assumed to have the higher rank.</param>
		/// <param name="lowRankUserID">userID of the player assumed to have the lower rank.</param>
		/// <returns></returns>
		private bool RankIsHigherThan(string highRankUserID, string lowRankUserID)
		{
			HashSet<string> highPlayerRanks = GetPlayerRanks(highRankUserID);
			HashSet<string> lowPlayerRanks = GetPlayerRanks(lowRankUserID);

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
		/// <param name="userID">userID of the player.</param>
		/// <param name="rank">Rank to grant.</param>
		/// <returns></returns>
		[PipeMethod]
		public bool GiveRank(string userID, string rank)
		{
			if(!RankExists(rank))
			{
				return false;
			}

			if(!playerRankDict.ContainsKey(userID))
			{
				playerRankDict.Add(userID, new HashSet<string>());
			}

			playerRankDict[userID].Add(rank);
			SavePlayerData();
			RefreshVanillaRank(this.Server.GetPlayers(userID).FirstOrDefault());
			return true;
		}

		/// <summary>
		/// Gives a temporary rank to a player which is not saved to file and refreshes the vanilla rank
		/// </summary>
		/// <param name="userID">userID of the player.</param>
		/// <param name="rank">Rank to grant.</param>
		/// <returns></returns>
		[PipeMethod]
		public bool GiveTempRank(string userID, string rank)
		{
			if (!RankExists(rank))
			{
				return false;
			}

			if (!tempPlayerRankDict.ContainsKey(userID))
			{
				tempPlayerRankDict.Add(userID, new HashSet<string>());
			}

			tempPlayerRankDict[userID].Add(rank);
			RefreshVanillaRank(this.Server.GetPlayers(userID).FirstOrDefault());
			return true;
		}

		/// <summary>
		/// Removes a rank from a player.
		/// </summary>
		/// <param name="userID">userID of the player.</param>
		/// <param name="rank">Rank to remove.</param>
		/// <returns></returns>
		[PipeMethod]
		public bool RemoveRank(string userID, string rank)
		{
			if (playerRankDict.ContainsKey(userID))
			{
				if (playerRankDict[userID].Remove(rank))
				{
					SavePlayerData();
					RefreshVanillaRank(this.Server.GetPlayers(userID).FirstOrDefault());
					RemoveTempRank(userID, rank);
					return true;
				}
			}
			return RemoveTempRank(userID, rank);
		}

		/// <summary>
		/// Removes a temp rank from a player.
		/// </summary>
		/// <param name="userID">userID of the player.</param>
		/// <param name="rank">Rank to remove.</param>
		/// <returns></returns>
		[PipeMethod]
		public bool RemoveTempRank(string userID, string rank)
		{
			if (tempPlayerRankDict.ContainsKey(userID))
			{
				if (tempPlayerRankDict[userID].Remove(rank))
				{
					SavePlayerData();
					RefreshVanillaRank(this.Server.GetPlayers(userID).FirstOrDefault());
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
				this.Debug("Not refreshing vanilla rank, player not online.");
				return;
			}

			this.Debug("Refreshing vanilla ranks for: " + player.Name);

			HashSet<string> playerRanks = GetPlayerRanks(player.UserID);
			if (playerRanks.Count > 0)
			{
				this.Debug("Found ranks: " + string.Join(", ", playerRanks));

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

		public bool ValidateUserID(string userID)
		{
			if (!userID.EndsWith("@steam") && !userID.EndsWith("@discord"))
			{
				this.Debug("UserID '" + userID + "' doesn't end with either @steam or @discord, making it invalid.");
				return false;
			}

			string rawID = userID.Replace("@steam", "").Replace("@discord", "");
			if(!ulong.TryParse(rawID, out ulong _))
			{
				this.Debug("Partial UserID '" + rawID + "' is not convertable to a 64bit unsigned integer, making it invalid.");
				return false;
			}

			if (rawID.Length < 17)
			{
				this.Debug("Partial UserID '" + rawID + "' is not long enough to be a valid id.");
				return false;
			}

			this.Debug("UserID '" + userID + "' is valid.");
			return true;
		}

		/////////////////////////////////
		// Commands
		////////////////////////////////
		private class ReloadCommand : ICommandHandler
		{
			private readonly SCPermissions plugin;

			public ReloadCommand(SCPermissions plugin)
			{
				this.plugin = plugin;
			}

			public string GetCommandDescription()
			{
				return "Reloads the config and player data.";
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
						return new[] { "You don't have permission to use that command." };
					}
				}

				plugin.Info("Reloading plugin...");
				plugin.LoadConfig();
				plugin.LoadPlayerData();
				return new[] { "Reload complete." };
			}
		}

		private class GiveRankCommand : ICommandHandler
		{
			private readonly SCPermissions plugin;

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
				return "scperms_giverank <rank> <userID>";
			}

			public string[] OnCall(ICommandSender sender, string[] args)
			{
				if (args.Length > 1)
				{
					if (sender is Player player)
					{
						if (!player.HasPermission("scpermissions.giverank"))
						{
							return new[] { "You don't have permission to use that command." };
						}

						if(!plugin.RankIsHigherThan(player.UserID, args[1]))
						{
							return new[] { "You are not allowed to edit players with ranks equal or above your own." };
						}
					}

					if (!plugin.ValidateUserID(args[1]))
					{
						return new[] { "That doesn't look like a valid user ID, it has to be a number at least 17 characters long and ending with either @steam or @discord" };
					}

					if (plugin.GiveRank(args[1], args[0]))
					{
						return new[] { "Added the rank " + args[0] + " to " + args[1] + "." };
					}
					else
					{
						return new[] { "Could not add that rank. Does the rank not exist or does the player already have it?" };
					}
				}
				else
				{
					return new[] { "Not enough arguments." };
				}
			}
		}

		private class GiveTempRankCommand : ICommandHandler
		{
			private readonly SCPermissions plugin;

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
				return "scperms_givetemprank <rank> <userID>";
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
								return new[] {"You don't have permission to use that command."};
							}

							if (!plugin.RankIsHigherThan(player.UserID, args[1]))
							{
								return new[] {"You are not allowed to edit players with ranks equal or above your own."};
							}
						}

						if (!plugin.ValidateUserID(args[1]))
						{
							return new[] { "That doesn't look like a valid user ID, it has to be a number at least 17 characters long and ending with either @steam or @discord" };
						}

						if (plugin.GiveTempRank(args[1], args[0]))
						{
							return new[] {"Added the rank " + args[0] + " to " + args[1] + "."};
						}
						else
						{
							return new[] { "Could not add that rank. Does the rank not exist or does the player already have it?" };
						}

					}
					else
					{
						return new[] {"Not enough arguments."};
					}
				}
				catch (Exception e)
				{
					this.plugin.Error("Error occured: " + e);
					return new[] { "Error occured." };
				}
			}
		}

		private class RemoveRankCommand : ICommandHandler
		{
			private readonly SCPermissions plugin;

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
				return "scperms_removerank <rank> <userID>";
			}

			public string[] OnCall(ICommandSender sender, string[] args)
			{
				if (args.Length > 1)
				{
					if (sender is Player player)
					{
						if (!player.HasPermission("scpermissions.removerank"))
						{
							return new[] { "You don't have permission to use that command." };
						}

						if (!plugin.RankIsHigherThan(player.UserID, args[1]))
						{
							return new[] { "You are not allowed to edit players with ranks equal or above your own." };
						}
					}

					if (plugin.RemoveRank(args[1], args[0]))
					{
						return new[] { "Removed the rank " + args[0] + " from " + args[1] + "." };
					}
					else
					{
						return new[] { "Could not remove that rank. Does the player not have it?" };
					}

				}
				else
				{
					return new[] { "Not enough arguments." };
				}
			}
		}

		private class ListRanksCommand : ICommandHandler
		{
			private readonly SCPermissions plugin;

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
						return new[] { "You don't have permission to use that command." };
					}
				}

				List<string> strings = new List<string>();
				JProperty[] allRanks = this.plugin.permissions.Properties().ToArray();
				foreach (JProperty rank in allRanks)
				{
					strings.Add(rank.Name);
				}
				return new[] { "Registered ranks: " + string.Join(", ", strings) };
			}
		}

		private class RemoveTempRankCommand : ICommandHandler
		{
			private readonly SCPermissions plugin;

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
				return "scperms_removetemprank <rank> <userID>";
			}

			public string[] OnCall(ICommandSender sender, string[] args)
			{
				if (args.Length > 1)
				{
					if (sender is Player player)
					{
						if (!player.HasPermission("scpermissions.removetemprank"))
						{
							return new[] { "You don't have permission to use that command." };
						}

						if (!plugin.RankIsHigherThan(player.UserID, args[1]))
						{
							return new[] { "You are not allowed to edit players with ranks equal or above your own." };
						}
					}

					if (plugin.RemoveTempRank(args[1], args[0]))
					{
						return new[] { "Removed the temporary rank " + args[0] + " from " + args[1] + "." };
					}
					else
					{
						return new[] { "Could not remove that temporary rank. Does the player not have it?" };
					}

				}
				else
				{
					return new[] { "Not enough arguments." };
				}
			}
		}

		private class VerboseCommand : ICommandHandler
		{
			private readonly SCPermissions plugin;

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
						return new[] { "You don't have permission to use that command." };
					}
				}

				plugin.verbose = !plugin.verbose;
				return new[] { "Verbose messages: " + plugin.verbose };
			}
		}

		private class DebugCommand : ICommandHandler
		{
			private readonly SCPermissions plugin;

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
						return new[] { "You don't have permission to use that command." };
					}
				}

				plugin.debug = !plugin.debug;
				return new[] { "Debug messages: " + plugin.debug };
			}
		}
	}

	/// <summary>
	/// Sets vanilla ranks for joining players
	/// </summary>
	internal class PlayerJoinHandler : IEventHandlerPlayerJoin
	{
		private readonly SCPermissions plugin;

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
					plugin.RefreshVanillaRank(ev.Player);

				}).Start();
			}
			catch (Exception e)
			{
				plugin.Verbose(e.ToString());
			}
		}
	}
}
