using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Smod2;
using Smod2.API;
using Smod2.Attributes;
using Smod2.Permissions;

namespace SCPermissions
{
    [PluginDetails(
        author = "Karl Essinger",
        name = "SCPermissions",
        description = "A permissions system. Secure, Contain, Permit.",
        id = "karlofduty.scpermissions",
        version = "0.0.1",
        SmodMajor = 3,
        SmodMinor = 2,
        SmodRevision = 3
    )]
    public class SCPermissions : Plugin, IPermissionsHandler
    {
        public short CheckPermission(Player player, string permissionName)
        {
            return -1;
        }

        public override void OnDisable()
        {
            // Useless function
        }

        public override void OnEnable()
        {
            this.Info("Special Containment Permissions loaded.");
        }

        public override void Register()
        {
            AddPermissionsHandler(this);
        }
    }
}
