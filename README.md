# BugCatcher

An in-game debug overlay for Unity: performance HUD, log console with commands, audio spectrum and system info.

- **HUD**: FPS, RAM, Audio, Logs and System modules; touches pass through to the game.
- **Console**: search, filters, collapse, stack traces and your own commands.
- **Audio**: spectrum and levels from Unity Audio or FMOD.
- **Auto screenshot**: saves the last seconds of gameplay when the game hitches or throws an error.
- Themes, layout presets; desktop, phone portrait and landscape.

## Installation

Requires Unity 2022.3 or newer.

- **Git URL** (*Package Manager → + → Add package from git URL*):
  `https://github.com/ThanhDV143/BugCatcher.git?path=/Packages/thanhdv.bugcatcher`
- **Scoped registry** (*Project Settings → Package Manager*): name `ThanhDVs`, URL `https://upm.thanhdv.com`, scope `thanhdv`; then add `thanhdv.bugcatcher` by name.

## Enabling and disabling

BugCatcher is enabled right after import: it adds the `BUGCATCHER_ENABLED` symbol, and needs no scene setup. It is only included while that symbol is set. Toggle it with *Tools → ThanhDV → Bug Catcher → Enable Bug Catcher*. Build scripts can also pass the symbol in `BuildPlayerOptions.extraScriptingDefines`.

> **Untick *Enable Bug Catcher* before making a release build**, or it will ship with your game.

## Usage

Open the panel by tapping the bubble (hold it for the quick menu), holding both top corners of the screen for 1 s on mobile, or pressing `` ` `` three times on desktop. From code: `BugCatcher.Open()`, `Close()`, `Toggle()`.

**Auto screenshot** (Settings tab, off by default) keeps the last 2 s of low-resolution frames in memory and saves them, with an `info.txt`, to `Application.persistentDataPath/BugCatcher/Recordings` when a frame takes 3× the 5 s average (and at least 50 ms) or an error appears for the first time. Record from code:

```csharp
BugCatcher.Recorder.Start("Boss fight", CaptureMode.P240_16);
string folder = BugCatcher.Recorder.Stop();
BugCatcher.Recorder.SaveRecent("Quest bug");   // save the last 2 s
BugCatcher.Recorder.Suppress("Loading");       // ignore hitches until Resume()
```

**FMOD**: *Tools → ThanhDV → Bug Catcher → Use FMOD* (requires FMOD for Unity 2.02).

**Input System only**: add an `EventSystem` with `InputSystemUIInputModule` to the scene.

## Commands

Mark a static method with `[ConsoleMethod]`, or add a method or lambda from code:

```csharp
#if BUGCATCHER_ENABLED
using ThanhDV.BugCatcher;

public static class PlayerCommands
{
    [ConsoleMethod("player.gold", "Add gold", "amount")]
    private static void AddGold(int amount) { ... }
}
#endif
```

```csharp
BugCatcher.Commands.Add("player.heal", "Restore full health", Heal);
BugCatcher.Commands.Add<int>("player.hp", "Set health", value => hp = value, "value");
BugCatcher.Commands.Remove("player.hp");
```

- Parameters: `string`, `bool`, `char`, numbers, enums, `Vector2/3/4`, `Vector2Int/3Int`, `Quaternion`, `Color`. Group an argument with spaces in quotes or brackets: `player.teleport (1 2 3)`.
- Commands without parameters show up as quick buttons; `help` lists every command.
- A command added on an object is removed when the object is destroyed.
- Commands survive managed code stripping with no `[Preserve]` or `link.xml`, as long as the Editor has compiled with `BUGCATCHER_ENABLED`. If the symbol is passed only to the build, set *Managed Stripping Level* to *Minimal*.

### Keep debug code out of release builds

Without `BUGCATCHER_ENABLED`, BugCatcher's types do not exist, so any code that uses them outside `#if BUGCATCHER_ENABLED` fails the release build instead of shipping by accident.

> **Write debug tools in a file wrapped in `#if BUGCATCHER_ENABLED`, or in an assembly definition that has `BUGCATCHER_ENABLED` in its *Define Constraints*.**

To register commands from a gameplay class without `#if` in it, use a partial method; without the symbol the call is removed:

```csharp
// Player.cs
public partial class Player : MonoBehaviour
{
    private void OnEnable() => AddDebugCommands();
    partial void AddDebugCommands();
}

// Player.Debug.cs
#if BUGCATCHER_ENABLED
using ThanhDV.BugCatcher;

public partial class Player
{
    partial void AddDebugCommands() => BugCatcher.Commands.Add("player.heal", "Restore full health", Heal);
    private void Heal() { ... }
}
#endif
```

Do not put debug components on prefabs or point Inspector events at debug methods: those are not compiled code, so they break at run time instead of at build time.

## License

Fonts: IBM Plex Sans / Mono, SIL Open Font License 1.1.

---

## Upgrading from 1.x.x to 2.x.x

2.x no longer depends on In-game Debug Console or Graphy.

- Replace `using IngameDebugConsole;` with `using ThanhDV.BugCatcher;`; `[ConsoleMethod]` keeps its arguments.
- Wrap command files as described in *Keep debug code out of release builds*, or the release build fails.
- `DebugLogConsole.AddCommand` / `RemoveCommand` → `BugCatcher.Commands.Add` / `Remove`, same arguments. `AddCommandInstance` and `AddCommandStatic` are gone: pass the method to `Add`.
- No longer supported: `GameObject`, `Component`, array, list, `Rect`, `RectInt`, `Bounds`, `BoundsInt`, `RectOffset` and `Color32` parameters, custom parameter types, `OnCommandExecuted` and `help <name>`.
- Remove `com.yasirkula.ingamedebugconsole` and `com.tayx.graphy` (and their registry scopes) if nothing else uses them.
