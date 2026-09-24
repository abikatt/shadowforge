# ShadowForge

Modding tools for Blue Dragon (Xbox 360).

- `src/ShadowForge.Core` - format library (IPK, HDB, HMB, DDS, RPJ, EVT, XACT, maps)
- `src/ShadowForge.CLI` - `sforge` command-line tool
- `addon/io_shadowforge` - Blender extension for editing characters and maps via `sforge`

## Build

Requires the .NET 10 SDK.

```
dotnet build ShadowForge.slnx
dotnet test ShadowForge.slnx
```

Run `sforge --help` for the list of commands.

## License

BSD 3-Clause. See [LICENSE](LICENSE).
