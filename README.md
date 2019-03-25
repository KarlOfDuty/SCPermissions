# SCPermissions
A permissions plugin for Smod/SCP:SL. Requires Smod version 3.4.0+.

# Config

## Vanilla server config

```yaml
# Whether or not the config should be placed in the global config directory
scpermissions_config_global: true
# Whether or not the player data file should be placed in the global config directory
scpermissions_playerdata_global: true
```

## Plugin config
```yaml
# Shows messages if errors occur, possibly other useful messages in the future
verbose: false

# Shows messages with debug information
debug: false

# The naem of the rank defined below which all players start with, set to "" to disable
defaultRank: default

# Permissions are generally 
permissions:

    # You can set permissions using single line statements
    admin:
        vanillarank: admin
        scpermissions.test1: true
        scpermissions.test2: true
        scpermissions.test3: true
        scpermissions.reload: true
        scpermissions.giverank: true
        scpermissions.removerank: true
        scpermissions.verbose: true
        scpermissions.debug: true
    # You can set permissions using object groups
    moderator:
        vanillarank: moderator
        scpermissions:
            test1: true
            test2: true
            test3: true

    # You can also mix both styles if you wish
    donator:
        vanillarank: donator
        scpermissions:
            test1: true
            test2: true
        scpermissions.test3: true
    # But you can NOT combine both styles for a single entry like this:
    #   scpermissions.subcategory:
    #       test: true
    # or like this:
    #   scpermissions:
    #       subcategory.test: true

    # You can set permissions negative to override lower roles
    restricted:
        vanillarank: restricted
        scpermissions:
            test1: false
            test2: false
            test3: false

    # You can set a default rank which all players have, it is recommended to keep this at the bottom as any lower ranks will just be overridden otherwise
    default:
        scpermissions:
            test1: true
            test2: false
            test3: false
```

# Commands

| Command | Permission | Description |
|---- |---- |---- |
| `scperms_reload` | `scpermissions.reload` | Reloads the plugin. |
| `scperms_giverank <rank> <steamid>` | `scpermissions.giverank` | Gives a player the specified rank. |
| `scperms_removerank <rank> <steamid>` | `scpermission.removerank` | Removes a rank from a player. |
| `scperms_verbose` | `scpermissions.verbose` | Toggles the verbose setting. |
| `scperms_debug` | `scpermissions.debug` | Toggles the debug setting. |
