# BugCatcher

An in-game debug overlay for Unity: performance HUD, log console, audio spectrum and system info in one tool. Built on Graphy and In-game Debug Console.

## Features

- **HUD**: FPS, RAM, Audio, Logs and System modules, each with its own display mode and screen corner. Touches pass through to the game.
- **Console**: logs with search, filters, collapse and stack traces; run In-game Debug Console commands with suggestions.
- **Audio**: live spectrum and levels from Unity Audio or FMOD.
- **System**: device, OS, CPU, GPU, RAM and screen info, one tap to copy.
- **Bubble**: floating button showing FPS and the error count.
- Graphite and Paper themes, adjustable HUD scale and opacity, layout presets.
- Works on desktop, phone portrait and phone landscape.

## Installation

Requires Unity 2022.3 or newer. Depends on Graphy (`com.tayx.graphy` 4.0.0) and In-game Debug Console (`com.yasirkula.ingamedebugconsole` 1.9.0).

### Unity Package Manager
```
https://github.com/ThanhDV143/BugCatcher.git?path=/Packages/thanhdv.bugcatcher
```

1. In Unity, open **Window** → **Package Manager**.
2. Press the **+** button, choose "**Add package from git URL...**"
3. Enter url above and press **Add**.

Unity does not fetch dependencies from git: add the scoped registry below first (scopes `com.tayx` and `com.yasirkula` are enough), or install Graphy and In-game Debug Console yourself.

### Scoped Registry

1. In Unity, open **Project Settings** → **Package Manager** → **Add New Scoped Registry**
- ``Name`` ThanhDVs
- ``URL`` https://upm.thanhdv.com
- ``Scope(s)`` thanhdv, com.tayx, com.yasirkula

2. In Unity, open **Window** → **Package Manager**.
- Press the **+** button, choose "**Add package by name...**" → ``thanhdv.bugcatcher``
- or
- Press the **Packages** button, choose "**My Registries**"

## Enabling and disabling

> **BugCatcher is enabled right after import (it adds the `BUGCATCHER_ENABLED` symbol for you). No prefab or scene setup is needed.**

Use *Tools → ThanhDV → Bug Catcher → Enable Bug Catcher*.

BugCatcher is only included while the `BUGCATCHER_ENABLED` scripting symbol is set, in the Editor and in every build.

> **Untick *Enable Bug Catcher* before making a release build**, or it will ship with your game.

## Usage

Open the panel:

| Action | Platform |
|---|---|
| Tap the bubble | All |
| Hold the bubble for the quick menu | All |
| Hold both top corners of the screen for 1 second | Mobile |
| Press `` ` `` three times quickly | Desktop / Editor |

From code: `BugCatcher.Open()`, `BugCatcher.Close()`, `BugCatcher.Toggle()`.

Configure the HUD (modules, corners, scale, theme) in the panel's **HUD** tab.

## FMOD

Use *Tools → ThanhDV → Bug Catcher → Use FMOD* to show FMOD audio in the Audio tab and HUD. Requires FMOD for Unity (2.02 API).

## Custom commands

Add commands the In-game Debug Console way; they appear in the console with suggestions:

```csharp
[ConsoleMethod("player.heal", "Restore full health")]
public static void Heal() { ... }
```

Commands without parameters also show up as quick buttons.

## Input System

If your project uses **only** the new Input System, add an `EventSystem` with `InputSystemUIInputModule` to the scene so the panel receives input.

## License

Fonts: IBM Plex Sans / Mono, SIL Open Font License 1.1.
