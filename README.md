# Tome

Tome is a bare-bones mod manager for Deadlock, initially inspired by Kerbal Space Program's [CKAN](https://github.com/KSP-CKAN/CKAN).

Contributions are welcome! (although, heavily vibe-coded content is discouraged)

## Install Tome

I consider Tome mostly feature complete for distribution, and am working on getting installers made.
Meanwhile, you can build it yourself!

## Build Tome

My development environment is Visual Studio with the .NET (10) desktop development module (for WPF).

You can build Tome with:
```cmd
dotnet build -c Release
```

The executable will be built at `Tome\bin\Release\net10.0-windows\Tome.exe`.

## Platform Support

Tome will be Windows exclusive, as it uses WPF.

## TODO

- Support mods that serve multiple archives which can both be installed (ref. [rabbit graves](https://gamebanana.com/mods/734072))
- Maybe support performance config mods which override gameinfo.gi

## Credits

- <https://www.postman.com/s0nought/gb-api-v11/overview>: Gamebanana API reference.
- [ValveKeyValue](https://github.com/ValveResourceFormat/ValveKeyValue): for parsing gameinfo.gi
- [steamlocate-rs](https://github.com/WilliamVenner/steamlocate-rs/): reference for locating the user's Steam installation
- [InnoDependencyInstaller](https://github.com/DomGries/InnoDependencyInstaller): .NET & WebView installation for installers
- [Deadlock Mod Manager](https://github.com/deadlock-mod-manager/deadlock-mod-manager) and [Grimoire Mod Manager](https://github.com/Slush97/grimoire): reference on how Deadlock patching works