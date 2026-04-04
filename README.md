# AchievementManager

A modular achievement system for Unity.  
Tracks Trigger and Progress achievement types with PlayerPrefs persistence, JSON definitions for modding, and optional integration with SaveManager, GalleryManager, and EventManager.


## Features

- **Two achievement types** — `Trigger` (single-call unlock) and `Progress` (unlock when accumulated count reaches a target)
- **Definitions** — id, title, description, icon, points, type, progressTarget, hidden flag; define in Inspector
- **PlayerPrefs persistence** — unlock state and progress survive app restarts without a save file
- **JSON / Modding** — load and merge achievement definitions from `StreamingAssets/achievements.json`; mods can add new achievements or override existing ones
- **Progress bar** — `AddProgress(id, amount)` accumulates progress and auto-unlocks at `progressTarget`
- **Total points** — `TotalPoints` property sums all currently unlocked achievement points
- **SaveManager integration** — sets `ach_<id>` save flags on unlock for cross-system checks (activated via `ACHIEVEMENTMANAGER_SM`)
- **GalleryManager integration** — calls `GalleryManager.UnlockStatic(id)` on achievement unlock (activated via `ACHIEVEMENTMANAGER_GM`)
- **EventManager integration** — fires `AchievementUnlocked` as a named GameEvent (activated via `ACHIEVEMENTMANAGER_EM`)
- **Custom Inspector** — live unlock / progress display, per-achievement Unlock / Reset buttons, progress bars, Reset All
- **Odin Inspector integration** — `SerializedMonoBehaviour` base for full Inspector serialization of complex types; runtime-display fields marked `[ReadOnly]` in Play Mode (activated via `ODIN_INSPECTOR`)


## Installation

### Option A — Unity Package Manager (Git URL)

1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL…**
3. Enter:

   ```
   https://github.com/RolandKaechele/AchievementManager.git
   ```

### Option B — Clone into Assets

```bash
git clone https://github.com/RolandKaechele/AchievementManager.git Assets/AchievementManager
```

### Option C — npm / postinstall

```bash
cd Assets/AchievementManager
npm install
```

`postinstall.js` creates the required `StreamingAssets/` folder under `Assets/` and optionally copies example JSON files.


## Scene Setup

1. Attach `AchievementManager` to a persistent manager GameObject.
2. Define achievements in the Inspector.
3. Add `DontDestroyOnLoad(gameObject)` if tracking spans multiple scenes.


## Quick Start

### 1. Inspector fields

| Field | Default | Description |
| ----- | ------- | ----------- |
| `achievements` | *(empty)* | All achievement definitions |
| `loadFromJson` | `false` | Merge from JSON on Awake |
| `jsonPath` | `"achievements.json"` | Path relative to `StreamingAssets/` |

### 2. Trigger achievements

```csharp
var am = FindFirstObjectByType<AchievementManager.Runtime.AchievementManager>();

// Unlock a Trigger achievement
am.Unlock("first_chapter_complete");

// Check unlock
bool unlocked = am.IsUnlocked("first_chapter_complete");
```

### 3. Progress achievements

```csharp
// Add to a running counter (e.g. enemies defeated)
am.AddProgress("defeat_100_enemies");          // +1
am.AddProgress("defeat_100_enemies", 5);       // +5

// Set absolute value
am.SetProgress("defeat_100_enemies", 42);

// Read current progress
int current = am.GetProgress("defeat_100_enemies");  // → 42
```

The achievement automatically unlocks when `current >= progressTarget`.

### 4. React to events

```csharp
am.OnAchievementUnlocked += id                      => ShowNotification(id);
am.OnProgressUpdated     += (id, current, target)   => UpdateProgressBar(id, current, target);
```

### 5. Unlock from anywhere

```csharp
// Static — no scene reference needed, no events fired
AchievementManager.Runtime.AchievementManager.UnlockStatic("first_chapter_complete");
```

### 6. Summary

```csharp
Debug.Log($"Unlocked {am.GetUnlocked().Count} / {am.Achievements.Count}  ({am.TotalPoints} pts)");
```


## JSON / Modding

Enable `loadFromJson` and place `achievements.json` in `StreamingAssets/`.

```json
{
  "achievements": [
    {
      "id": "first_chapter_complete",
      "title": "Der erste Schritt",
      "description": "Schließe das erste Kapitel ab.",
      "points": 10,
      "type": 0,
      "progressTarget": 1,
      "hidden": false
    },
    {
      "id": "defeat_100_enemies",
      "title": "Unaufhaltsam",
      "description": "Besiege 100 Feinde.",
      "points": 25,
      "type": 1,
      "progressTarget": 100,
      "hidden": false
    }
  ]
}
```

**`type` values:** `0` = Trigger, `1` = Progress

JSON and Inspector entries are merged by `id`. JSON entries with a matching id override Inspector data; new ids are appended.


## PlayerPrefs Keys

| Key | Value | Description |
| --- | ----- | ----------- |
| `ach_unlock_<id>` | `0` / `1` | Unlock state for each achievement |
| `ach_progress_<id>` | `int` | Accumulated progress count (Progress type only) |


## Runtime API

| Member | Description |
| ------ | ----------- |
| `Unlock(id)` | Unlock a Trigger or Progress achievement; persists to PlayerPrefs. Idempotent |
| `IsUnlocked(id)` | True if unlocked (PlayerPrefs → SaveManager if `ACHIEVEMENTMANAGER_SM`) |
| `AddProgress(id, amount)` | Increment progress; auto-unlocks when target is reached |
| `SetProgress(id, value)` | Set progress to absolute value; auto-unlocks when target is reached |
| `GetProgress(id)` | Current accumulated progress value |
| `ResetProgress(id)` | Clear unlock + progress for a single achievement |
| `ResetAll()` | Clear all achievements and progress |
| `GetDefinition(id)` | Returns `AchievementDefinition` or null |
| `GetUnlocked()` | List of all currently unlocked achievements |
| `UnlockStatic(id)` | *(static)* Persist unlock via PlayerPrefs without events |
| `Achievements` | `IReadOnlyList<AchievementDefinition>` (merged) |
| `TotalPoints` | Sum of points from all unlocked achievements |
| `OnAchievementUnlocked` | `event Action<string>` — achievement id |
| `OnProgressUpdated` | `event Action<string, int, int>` — (id, current, target) |


## Optional Integrations

### SaveManager (`ACHIEVEMENTMANAGER_SM`)

Requires `ACHIEVEMENTMANAGER_SM` define and [SaveManager](https://github.com/RolandKaechele/SaveManager).

`Unlock(id)` additionally calls `SaveManager.SetFlag("ach_<id>")`.  
`IsUnlocked(id)` additionally checks `SaveManager.IsSet("ach_<id>")`.

### GalleryManager (`ACHIEVEMENTMANAGER_GM`)

Requires `ACHIEVEMENTMANAGER_GM` define and [GalleryManager](https://github.com/RolandKaechele/GalleryManager).  
`Unlock(id)` calls `GalleryManager.UnlockStatic(id)` — useful when achievements and gallery entries share ids.

### EventManager (`ACHIEVEMENTMANAGER_EM`)

Requires `ACHIEVEMENTMANAGER_EM` define. The following named GameEvent is fired:

| Event name | When |
| ---------- | ---- |
| `AchievementUnlocked` | `Unlock(id)`; value = achievement id |


### Odin Inspector (`ODIN_INSPECTOR`)

Requires `ODIN_INSPECTOR` define (standard Odin Inspector scripting define). Inherits from `SerializedMonoBehaviour` for full Inspector serialization; runtime-display fields are marked `[ReadOnly]`.


## Dependencies

| Dependency | Required | Notes |
| ---------- | -------- | ----- |
| Unity 2022.3+ | ✓ | |
| SaveManager | optional | Required when `ACHIEVEMENTMANAGER_SM` is defined |
| GalleryManager | optional | Required when `ACHIEVEMENTMANAGER_GM` is defined |
| EventManager | optional | Required when `ACHIEVEMENTMANAGER_EM` is defined |
| Odin Inspector | optional | Required when `ODIN_INSPECTOR` is defined |


## Repository

[https://github.com/RolandKaechele/AchievementManager](https://github.com/RolandKaechele/AchievementManager)


## License

MIT — see [LICENSE](LICENSE).
