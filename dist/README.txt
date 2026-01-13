===============================================
ELDEN RING RANDOMIZER TRACKER v2.1
===============================================

This tracker monitors Great Rune acquisitions and boss defeats during
Elden Ring randomizer runs. It works alongside LiveSplit/SoulSplitter
and displays where each Great Rune and boss was found.

REQUIREMENTS
------------
- Elden Ring (with EAC disabled for randomizer)
- LiveSplit with SoulSplitter component
- Elden Ring Item and Enemy Randomizer
- .NET Framework 4.8.1 (usually pre-installed on Windows 10/11)

WHAT'S INCLUDED
---------------
This package contains two folders:

1. LiveSplit_Components/
   - SoulSplitter.dll (modified to output tracking data)
   - SoulMemory.dll (required dependency)

2. GreatRuneTracker/
   - GreatRuneTracker.exe (the tracker application)
   - Supporting DLL files

INSTALLATION
------------

STEP 1: Install Modified SoulSplitter

   Copy BOTH files from the LiveSplit_Components folder to your
   LiveSplit Components folder:

   From: LiveSplit_Components/
   To:   [Your LiveSplit folder]/Components/

   Example: C:/LiveSplit/Components/

   This replaces the standard SoulSplitter with a version that
   outputs tracking data for our tracker to read.

   NOTE: Back up your existing SoulSplitter.dll and SoulMemory.dll
   first if you want to restore them later!

STEP 2: Place the Tracker

   Copy the entire GreatRuneTracker folder to your Elden Ring
   mods folder (next to the randomizer folder):

   Example: .../ELDEN RING/Game/mods/GreatRuneTracker/

   The tracker will auto-detect spoiler logs from:
   ../randomizer/spoiler_logs/

STEP 3: Configure LiveSplit (if not already done)

   1. Open LiveSplit
   2. Right-click > Edit Layout
   3. Click + (Add) > Control > SoulSplitter
   4. Configure SoulSplitter for Elden Ring
   5. Set up your splits as desired

RUNNING A RACE
--------------

1. Start LiveSplit (with SoulSplitter enabled)
2. Launch Elden Ring (with EAC disabled)
3. Run GreatRuneTracker.exe from the GreatRuneTracker folder
4. The tracker will show "Connected to SoulSplitter!" when ready
5. Start your randomizer run!

The tracker will display:
- DEFEATED: [Boss Name] and what boss it replaced
- OBTAINED: [Great Rune] and the location it was found

Output logs are saved to: GreatRuneTracker/logs/

WHAT'S TRACKED
--------------

Great Runes (7 total):
  - Godrick's Great Rune
  - Radahn's Great Rune
  - Morgott's Great Rune
  - Rykard's Great Rune
  - Mohg's Great Rune
  - Malenia's Great Rune
  - Great Rune of the Unborn

Major Bosses (30 total):
  - All main story bosses
  - Multi-phase boss support (Rennala, Fire Giant, Rykard, etc.)

TROUBLESHOOTING
---------------

"Waiting for SoulSplitter data..."
   - Make sure LiveSplit is running with SoulSplitter enabled
   - Make sure you copied BOTH DLL files to LiveSplit/Components/
   - Try restarting LiveSplit

"No spoiler log found"
   - Place the GreatRuneTracker folder in mods/ next to randomizer/
   - Or use: GreatRuneTracker.exe --spoiler "path/to/spoiler_logs"

Tracker not detecting Great Rune of the Unborn?
   - This version uses ordinal flag deduction which should work
   - Make sure you're using the modified SoulSplitter.dll included here

COMMAND LINE OPTIONS
--------------------

  -s, --spoiler <path>   Path to spoiler log file or directory
  -o, --output <path>    Output file path (default: logs/TrackerLog.txt)
  -h, --help             Show help message

===============================================
Created for Elden Ring Randomizer Racing
Version 2.1 - Great Rune of the Unborn fix
===============================================
