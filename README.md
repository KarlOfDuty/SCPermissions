# SCPermissions [![Release](https://img.shields.io/github/release/KarlofDuty/SCPermissions.svg)](https://github.com/KarlOfDuty/SCPermissions/releases) [![Downloads](https://img.shields.io/github/downloads/KarlOfDuty/SCPermissions/total.svg)](https://github.com/KarlOfDuty/SCPermissions/releases) [![Discord Server](https://img.shields.io/discord/430468637183442945.svg?label=discord)](https://discord.gg/C5qMvkj)  [![Patreon](https://img.shields.io/badge/patreon-donate-orange.svg)](https://patreon.com/karlofduty)
A permissions plugin for Smod/SCP:SL. Requires Smod version 3.4.0+.

# Config

## Vanilla server config

```yaml
# Whether or not the config should be placed in the global config directory
scperms_config_global: true
# Whether or not the player data file should be placed in the global config directory
scperms_playerdata_global: true
```

## Plugin config
```yaml
# Shows messages if errors occur, possibly other useful messages in the future
verbose: true

# Shows messages with debug information
debug: false

# The naem of the rank defined below which all players start with, set to "" to disable
defaultRank: player

# Permissions are generally stated as "pluginname.permissionname", you can assign permission nodes to different ranks here.
# Use the scperms_giverank command to give a player a rank (or scperms_givetemprank for testing, the rank is then removed on server restart).
# If you have set conflicting permissions a higher rank will always override a lower one as players can have several ranks at the same time.
# The 'vanillarank' node gives players ranks designated in the vanilla config so you don't have to enter the players in both systems
permissions:
    admin:
        vanillarank: admin
        scpermissions.reload: true
        scpermissions.verbose: true
        scpermissions.debug: true
        scpermissions.giverank: true
        scpermissions.removerank: true
        scpermissions.giverank: true
        scpermissions.removerank: true

    moderator:
        vanillarank: moderator
        scpermissions.giverank: true
        scpermissions.removerank: true

    donator:
        vanillarank: donator
        customloadouts.donatorloadouts: true

    restricted:
        vanillarank: restricted
        customloadouts.playerloadouts: false

    player:
        customloadouts.playerloadouts: true
```

# Commands

| Command | Permission | Description |
|---- |---- |---- |
| `scperms_reload` | `scpermissions.reload` | Reloads the plugin. |
| `scperms_giverank <rank> <steamid>` | `scpermissions.giverank` | Gives a player the specified rank. |
| `scperms_givetemprank <rank> <steamid>` | `scpermissions.givetemprank` | Gives a player the specified rank which is then removed on server restart. This mostly exists for automation support for other plugins. |
| `scperms_removerank <rank> <steamid>` | `scpermission.removerank` | Removes a rank from a player, both saved and temp ranks. |
| `scperms_removetemprank <rank> <steamid>` | `scpermission.removetemprank` | Removes a temp rank from a player. This mostly exists for automation support for other plugins. |
| `scperms_listranks` | `scpermissions.listranks` | Shows all registered ranks. |
| `scperms_verbose` | `scpermissions.verbose` | Toggles the verbose setting. |
| `scperms_debug` | `scpermissions.debug` | Toggles the debug setting. |
