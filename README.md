# Tome

Tome is a bare-bones mod manager for Deadlock, initially inspired by Kerbal Space Program's [CKAN](https://github.com/KSP-CKAN/CKAN).

Contributions are welcome! (although, heavily vibe-coded content is discouraged)

## Install/Run Tome

- This project is still *really* early in development.
- There is no skin conflict resolution.
- There will be breaking changes in updates.

Tome is not at a stage where I'd distribute installers and exes, so you will have to have the .NET 10 SDK installed.
My development environment is Visual Studio with the .NET desktop development module (for WPF).

You can build Tome with:
```cmd
dotnet build -c Release
```

The executable will be built at `Tome\bin\Release\net10.0-windows\Tome.exe`.

Tome will be Windows exclusive, as it uses WPF.

## Credits

- <https://www.postman.com/s0nought/gb-api-v11/overview> for the Gamebanana API reference.
- [ValveKeyValue](https://github.com/ValveResourceFormat/ValveKeyValue) for parsing gameinfo.gi
- [steamlocate-rs](https://github.com/WilliamVenner/steamlocate-rs/) for providing a reference for locating the user's Steam installation
- [Deadlock Mod Manager](https://github.com/deadlock-mod-manager/deadlock-mod-manager) and [Grimoire Mod Manager](https://github.com/Slush97/grimoire) for providing a reference on how Deadlock patching works