# osu! plugins

[!WARNING]
DON'T USE THIS PROJECT WITH ONLINE BUILDS, SEE [#93](https://github.com/Cai1Hsu/osu-plugins/issues/93#issue-4502056290).

This repository contains a custom plugin system for [osu!(lazer)](https://github.com/ppy/osu) and some example plugins. It's a rewrite version of my modifications to the osu! client. As more and more modifications are made, I find it hard to maintain them all in a single project and it's also hard to debug plugins when they are tightly coupled.

_This repository doesn't provide any pre-built binaries currently. You will need to build the projects yourself to get the plugin loader and example plugins._

This repo now contains the following plugins:

- [osu.Plugin.MainMenuPlayer](./osu.Plugin.MainMenuPlayer): A plugin that make your main menu a music player by hiding UI and show some track info. Similar to [MATRIX-feather/LLin(Hikariii)](https://github.com/MATRIX-feather/LLin).
- [Template project](./osu.Plugin.Template): A template project that you can use to create your own plugins.

I'm migrating more of my modifications to this plugin system and maybe new plugins. But there's no guarantees, since these are only for my **personal use**.

## Install a plugin

To get your plugin loaded by osu!, you need to install the loader and place your plugin DLL in correct folder.

The plugin loader is a custom ruleset that directly loaded by the game client. You should install it in your osu! installation directory or ruleset folder(see [User storage](https://github.com/ppy/osu/wiki/User-file-storage)).

After the loader is installed, create a folder named `plugins` in the user storage directory, and place your plugin DLL there. The loader will automatically load all plugins in that folder when the game starts.

## Create your own plugin

To create your own plugin, simply create a new class library project that references the `osu.Game.Plugins` project in this repo and `ppy.osu.Game` packet from NuGet. There's a template project in this repo that you can use as a starting point: [osu.Plugin.Template](./osu.Plugin.Template).

## Test with local osu

When developing plugins, you may want to test with a local osu! checkout. To do this, check out the osu! at the same level as this repo, then execute the `UseLocalOsu.*` scripts to switch project references to local osu! projects. See documentation in the scripts for details.

## Publish for release

When using local osu project references (via `UseLocalOsu.*`), game assets from the local checkout may be copied into publish output. To avoid this, use the release pack scripts:

- `./PackForRelease.ps1` (PowerShell)
- `./PackForRelease.sh` (bash)

These scripts first pack local osu projects from `../osu` into a local NuGet source, then temporarily switch plugin references to that package version, run `dotnet publish`, and finally restore local references.

Examples:

- `./PackForRelease.ps1 2025.1209.0`
- `./PackForRelease.ps1 2025.1209.0 -Target osu.Plugin.LegacyExperience/osu.Plugin.LegacyExperience.csproj`
- `./PackForRelease.sh 2025.1209.0`

By default, both scripts publish the whole solution (`osu-plugins.slnx`).

## Credit

- [MATRIX-feather/LLin](https://github.com/MATRIX-feather/LLin): Some ideas are inspired by this project.
