# Mod List

Adds a Mods button and in-game config editor to Slots & Daggers.

## Features

- A Mod List screen showing every installed BepInEx mod with its name, version, and package name
- A Config button for mods that have settings, so you can change them without leaving the game
- A Reset button on every setting
- Changes are saved to the mod's `.cfg` file right away

## Installation

Install with a mod manager such as r2modman or the Thunderstore Mod Manager, then launch the game.

Manual install: install BepInEx 5 into the game folder, then copy `ModList.dll` into `BepInEx/plugins/ModList/`.

## Configuration

Launch the game once to generate the config file at `BepInEx/config/com.king_tajin.modlist.cfg`.

| Setting | Default | Description |
| --- | --- | --- |
| DumpCreditsHierarchy | false | For development purposes only. Logs the credits screen hierarchy the first time the mod list opens |

## Source

https://github.com/King-Tajin/Slots-And-Daggers-Mod-List

Licensed under the MIT License.
