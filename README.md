# Mod List

A BepInEx 5 plugin for Slots & Daggers that adds a Mods button and in-game config editor.

## Features

- A Mod List screen showing every installed BepInEx mod with its name, version, and GUID
- A Config button for mods that have settings, so you can change them without leaving the game
- A Reset button on every setting
- Settings marked `ReadOnly` or `Browsable = false` by their mod are shown read-only or hidden, and `Order` is respected
- Changes are saved to the mod's `.cfg` file right away

## Install

1. Install BepInEx 5 into the game folder.
2. Copy `ModList.dll` into `BepInEx/plugins/ModList/`.
3. Launch the game.

## Build

1. Create a `libs/` folder next to the `.csproj` and copy in the Unity, `UnityEngine.UI`, `Unity.TextMeshPro`, and `Assembly-CSharp` DLLs from your game install. BepInEx and Harmony come from the `BepInEx.Core` NuGet package.
2. Run `dotnet build -c Release`.
