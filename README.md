# Distillery Controller Application

This is a dotnet application which provides the single application which runs on the Controller
device attached to a Still. It is fully responsible for fulfilling the "dtmi:brewhub:controller:still;1"
DTMI.

This same application can connect to real sensors & valves, OR can be run on a regular PC to simulate
data.

For configuration, it will use the exact same TOML file which is used to configure Edge,
to make it easier.

```toml
[provisioning]
source = "dps"
global_endpoint = "https://global.azure-devices-provisioning.net"
id_scope = "$env:IDSCOPE"

[provisioning.attestation]
method = "symmetric_key"
registration_id = "$env:DEVICEID"
symmetric_key = { value = "$env:DEVICEKEY" }
```

To create one of these, I'm currently using the setup scripts in filtermodule.

```powershell
. .env.ps1
$env:DEVICEID = "...mac address on my development pc..."
cd setup
./MakeDeviceKey.ps1
./GenerateDeviceConfig.ps1
```