# Great Rune Tracker - Technical Documentation

This document outlines the architecture, key modifications, and data flow of the Great Rune Tracker system for Elden Ring randomizer runs.

## System Overview

The tracker consists of two components that communicate via a JSON file:

```
┌─────────────────────┐         JSON File          ┌─────────────────────┐
│    SoulSplitter     │  ───────────────────────►  │  GreatRuneTracker   │
│  (LiveSplit Plugin) │   %LocalAppData%/          │   (Standalone App)  │
│                     │   SoulSplitter/            │                     │
│  - Reads game memory│   tracker_output.json      │  - Reads JSON       │
│  - Writes JSON      │                            │  - Parses spoiler   │
│                     │                            │  - Displays output  │
└─────────────────────┘                            └─────────────────────┘
```

This architecture avoids game crashes that occur when multiple applications hook into Elden Ring's memory simultaneously.

---

## Part 1: SoulSplitter Modifications

### File: `src/SoulSplitter/Splitters/EldenRingSplitter.cs`

### Key Constants and Variables

```csharp
// JSON output file path
private static readonly string TrackerOutputPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "SoulSplitter",
    "tracker_output.json"
);

// Boss event flags (30 major bosses)
private static readonly uint[] BossFlags = {
    // Limgrave & Weeping Peninsula
    10000800,   // Margit, the Fell Omen
    10000850,   // Godrick the Grafted
    1042380800, // Leonine Misbegotten
    1042330800, // Cemetery Shade (Tombsward Catacombs)

    // Liurnia
    14000800,   // Rennala (phase 1)
    14000850,   // Rennala (phase 2) - USE THIS ONE
    12010800,   // Royal Knight Loretta

    // Caelid
    1052520800, // Commander O'Neil
    1052380800, // Decaying Ekzykes

    // Altus Plateau
    1039540800, // Ancient Dragon Lansseax
    1037530800, // Sanguine Noble

    // Leyndell
    11000800,   // Morgott the Omen King
    11000850,   // Godfrey (Golden Shade)

    // Mt. Gelmir & Volcano Manor
    16000800,   // Rykard (phase 1)
    16000850,   // Rykard (phase 2) - USE THIS ONE

    // Mountaintops of the Giants
    1052570800, // Fire Giant (phase 1)
    1052570850, // Fire Giant (phase 2) - USE THIS ONE
    1050570800, // Commander Niall

    // Crumbling Farum Azula
    13000800,   // Maliketh (phase 1 - Clergyman)
    13000850,   // Maliketh (phase 2) - USE THIS ONE
    13000830,   // Dragonlord Placidusax

    // Leyndell, Ashen Capital
    11050800,   // Hoarah Loux (phase 1 - Godfrey)
    11050850,   // Hoarah Loux (phase 2) - USE THIS ONE

    // Elden Throne
    19000800,   // Radagon
    19000850,   // Elden Beast - USE THIS ONE

    // Underground
    12020800,   // Valiant Gargoyles
    12020850,   // Mimic Tear
    12010850,   // Loretta (Haligtree version)

    // Mohgwyn Palace
    12050800,   // Mohg, Lord of Blood

    // Haligtree
    15000800,   // Malenia (phase 1)
    15000850,   // Malenia (phase 2) - USE THIS ONE

    // Radahn
    1052380850, // Starscourge Radahn
};

// Ordinal flags: Set when player obtains ANY great rune (1st, 2nd, 3rd, etc.)
private static readonly uint[] OrdinalRuneFlags = { 181, 182, 183, 184, 185, 186, 187 };

// Specific rune flags (171-176) - identify WHICH rune was obtained
private static readonly GreatRune[] SpecificRunes = {
    GreatRune.Godrick,  // 171
    GreatRune.Radahn,   // 172
    GreatRune.Morgott,  // 173
    GreatRune.Rykard,   // 174
    GreatRune.Mohg,     // 175
    GreatRune.Malenia   // 176
};
```

### Great Rune Enum Values

```csharp
// From SoulMemory/EldenRing/GreatRune.cs
public enum GreatRune : uint
{
    Godrick = 171,   // Stormveil Castle
    Radahn = 172,    // Redmane Castle
    Morgott = 173,   // Leyndell
    Rykard = 174,    // Volcano Manor
    Mohg = 175,      // Mohgwyn Palace
    Malenia = 176,   // Miquella's Haligtree
    Unborn = 197     // Academy of Raya Lucaria (SPECIAL CASE)
}
```

### Critical Logic: Great Rune of the Unborn Detection

**Problem:** In the randomizer, when Great Rune of the Unborn is obtained from a non-Rennala location, flag 197 is NEVER set. This breaks direct flag detection.

**Solution:** Use ordinal flag deduction.

```csharp
bool GetUnbornState()
{
    // First check flag 197 (works in vanilla, not in randomizer)
    if (_eldenRing.ReadEventFlag((uint)GreatRune.Unborn))
        return true;

    // Deduction method: Count ordinal vs specific flags
    int ordinalCount = GetOrdinalCount();  // Count of flags 181-187 that are true
    int specificCount = 0;
    foreach (var rune in SpecificRunes)    // Count flags 171-176 that are true
    {
        if (_eldenRing.ReadEventFlag((uint)rune))
            specificCount++;
    }

    // If player has more runes (ordinal) than identified specific runes, Unborn was obtained
    // Example: ordinalCount=1, specificCount=0 → Unborn was the first rune obtained
    return ordinalCount > specificCount;
}
```

### JSON Output Format

The tracker writes to `%LocalAppData%\SoulSplitter\tracker_output.json`:

```json
{
  "igt": 123456789,
  "timestamp": "2024-01-13T12:34:56.789Z",
  "timerRunning": true,
  "greatRunes": {
    "Godrick": false,
    "Radahn": false,
    "Morgott": false,
    "Rykard": false,
    "Mohg": false,
    "Malenia": false,
    "Unborn": true
  },
  "bosses": {
    "10000800": false,
    "10000850": true,
    "1042380800": true,
    ...
  }
}
```

**Field Descriptions:**
| Field | Type | Description |
|-------|------|-------------|
| `igt` | int | In-game time in milliseconds |
| `timestamp` | string | UTC timestamp (ISO 8601) |
| `timerRunning` | bool | Whether LiveSplit timer is active |
| `greatRunes` | object | Map of rune name → obtained (bool) |
| `bosses` | object | Map of flag ID → defeated (bool) |

### Update Loop Integration

```csharp
// In EldenRingSplitter.Update() method:
mainViewModel.TryAndHandleError(() =>
{
    UpdateTrackerOutput();  // Called every tick when game is running
});
```

The `UpdateTrackerOutput()` method:
1. Checks all boss flags for state changes
2. Checks specific rune flags (171-176) for state changes
3. Checks Unborn state using deduction method
4. If any changes detected, writes updated JSON

---

## Part 2: GreatRuneTracker Application

### File: `src/GreatRuneTracker/Program.cs`

### Spoiler Log Parsing

The tracker parses the randomizer's spoiler log to determine where each item was placed.

**Spoiler Log Location:** `../randomizer/spoiler_logs/` (relative to tracker)

**Parsing Logic:**

```csharp
// Regex patterns for spoiler log parsing
private static readonly Regex HintPattern = new(
    @"^(.+?):\s*(.+)$",   // "Godrick's Great Rune: Tombsward Catacombs"
    RegexOptions.Compiled
);

// Great Rune name mappings
private static readonly Dictionary<string, GreatRune> GreatRuneNames = new()
{
    { "Godrick's Great Rune", GreatRune.Godrick },
    { "Radahn's Great Rune", GreatRune.Radahn },
    { "Morgott's Great Rune", GreatRune.Morgott },
    { "Rykard's Great Rune", GreatRune.Rykard },
    { "Mohg's Great Rune", GreatRune.Mohg },
    { "Malenia's Great Rune", GreatRune.Malenia },
    { "Great Rune of the Unborn", GreatRune.Unborn }
};
```

**Spoiler Log Format (hints section):**
```
-- Hints --
Godrick's Great Rune: Tombsward Catacombs
Radahn's Great Rune: Mohgwyn Palace
Great Rune of the Unborn: Castle Morne
...
```

### Boss Flag to Name Mapping

```csharp
private static readonly Dictionary<uint, string> BossFlagNames = new()
{
    // Key bosses with locations
    { 10000800, "Margit, the Fell Omen" },
    { 10000850, "Godrick the Grafted" },
    { 1042380800, "Leonine Misbegotten" },
    { 14000850, "Rennala, Queen of the Full Moon" },
    { 16000850, "Rykard, Lord of Blasphemy" },
    { 1052570850, "Fire Giant" },
    { 13000850, "Maliketh, the Black Blade" },
    { 11050850, "Hoarah Loux, Warrior" },
    { 19000850, "Elden Beast" },
    { 12020800, "Valiant Gargoyles" },
    { 12020850, "Mimic Tear" },
    { 12050800, "Mohg, Lord of Blood" },
    { 15000850, "Malenia, Blade of Miquella" },
    { 1052380850, "Starscourge Radahn" },
    // ... additional bosses
};
```

### Multi-Phase Boss Handling

Some bosses have two flags (phase 1 and phase 2). The tracker uses the FINAL phase flag:

| Boss | Phase 1 Flag | Phase 2 Flag (Use This) |
|------|--------------|-------------------------|
| Rennala | 14000800 | 14000850 |
| Rykard | 16000800 | 16000850 |
| Fire Giant | 1052570800 | 1052570850 |
| Maliketh | 13000800 | 13000850 |
| Hoarah Loux | 11050800 | 11050850 |
| Elden Beast | 19000800 | 19000850 |
| Malenia | 15000800 | 15000850 |

### JSON Reading Loop

```csharp
private static TrackerData? ReadTrackerJson(string path)
{
    try
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<TrackerData>(json);
    }
    catch
    {
        return null;
    }
}

// Main polling loop
while (true)
{
    var data = ReadTrackerJson(trackerJsonPath);
    if (data != null)
    {
        // Check for state changes
        foreach (var (rune, obtained) in data.GreatRunes)
        {
            if (obtained && !previousRuneStates[rune])
            {
                // Rune was just obtained!
                var location = spoilerData.GetRuneLocation(rune);
                Console.WriteLine($"OBTAINED: {rune} at {location}");
            }
        }
        // Similar logic for bosses...
    }
    Thread.Sleep(100); // Poll every 100ms
}
```

### Output Format

The tracker outputs to console and log file:

```
[00:12:34] DEFEATED: Leonine Misbegotten (replaced Cemetery Shade)
[00:12:35] OBTAINED: Great Rune of the Unborn (found at Castle Morne)
[00:15:22] DEFEATED: Godrick the Grafted (replaced Godrick the Grafted)
[00:15:23] OBTAINED: Radahn's Great Rune (found at Stormveil Castle)
```

---

## Part 3: Data Flow Summary

```
1. Player defeats boss / picks up Great Rune in game

2. Elden Ring sets event flag in memory
   - Boss: Sets flag like 10000850
   - Great Rune: Sets ordinal flag (181-187) AND specific flag (171-176)
   - EXCEPTION: Unborn from non-Rennala location only sets ordinal flag

3. SoulSplitter reads flags via memory hooks
   - _eldenRing.ReadEventFlag(flagId)
   - For Unborn: Uses ordinal vs specific count deduction

4. SoulSplitter writes tracker_output.json
   - Updated every tick when state changes

5. GreatRuneTracker reads JSON file
   - Polls every 100ms
   - Compares against previous state

6. GreatRuneTracker cross-references spoiler log
   - Determines original location of item
   - Outputs "OBTAINED: X (found at Y)"
```

---

## Part 4: Key Integration Points

### For Mod Integration

**To read tracker state from your mod:**

```csharp
// JSON file location
string jsonPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "SoulSplitter",
    "tracker_output.json"
);

// Deserialize
var json = File.ReadAllText(jsonPath);
var data = JsonSerializer.Deserialize<TrackerData>(json);

// Access data
bool hasUnborn = data.GreatRunes["Unborn"];
bool defeatedMargit = data.Bosses["10000800"];
int igtMilliseconds = data.Igt;
```

**To detect Great Rune of the Unborn directly (if you have memory access):**

```csharp
// DON'T just check flag 197 - it won't work in randomizer!
bool hasUnborn = CheckFlag(197);  // WRONG for randomizer

// DO use ordinal deduction:
int ordinalCount = CountTrueFlags(181, 182, 183, 184, 185, 186, 187);
int specificCount = CountTrueFlags(171, 172, 173, 174, 175, 176);
bool hasUnborn = CheckFlag(197) || (ordinalCount > specificCount);  // CORRECT
```

### Event Flag Quick Reference

| Flag | Description |
|------|-------------|
| 171-176 | Specific Great Rune flags (Godrick through Malenia) |
| 181-187 | Ordinal Great Rune flags (1st through 7th obtained) |
| 197 | Great Rune of the Unborn (NOT set in randomizer!) |
| 10000800 | Margit defeated |
| 10000850 | Godrick defeated |
| 19000850 | Elden Beast defeated (game complete) |

---

## Part 5: File Structure

```
BubSplitter/
├── src/
│   ├── SoulSplitter/
│   │   └── Splitters/
│   │       └── EldenRingSplitter.cs    # Main modifications
│   ├── SoulMemory/
│   │   └── EldenRing/
│   │       ├── GreatRune.cs            # Enum definitions
│   │       └── EldenRing.cs            # Memory reading
│   └── GreatRuneTracker/
│       ├── Program.cs                   # Tracker application
│       └── GreatRuneTracker.csproj     # Project file
├── Components/
│   ├── SoulSplitter.dll                # Compiled plugin
│   └── SoulMemory.dll                  # Compiled library
└── dist/
    ├── LiveSplit_Components/           # DLLs for distribution
    ├── GreatRuneTracker/               # Tracker app for distribution
    └── README.txt                      # Installation instructions
```

---

## Questions?

For questions about this implementation, refer to:
- SoulSplitter source: https://github.com/FrankvdStam/SoulSplitter
- Elden Ring event flags: Search for "Elden Ring event flag list"
- Randomizer: https://www.nexusmods.com/eldenring/mods/428
