// Great Rune Tracker - Standalone console app for Elden Ring randomizer runs
// Reads flag states from SoulSplitter's JSON output to avoid memory conflicts
// Monitors Great Rune and Boss event flags and logs them with locations from spoiler logs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace GreatRuneTracker;

class Program
{
    // Great Rune event flag IDs (excludes Miquella's - not used in races)
    private static readonly Dictionary<uint, string> GreatRuneFlags = new()
    {
        { 171, "Godrick's Great Rune" },
        { 172, "Radahn's Great Rune" },
        { 173, "Morgott's Great Rune" },
        { 174, "Rykard's Great Rune" },
        { 175, "Mohg's Great Rune" },
        { 176, "Malenia's Great Rune" },
        { 197, "Great Rune of the Unborn" }
    };

    // Map GreatRune enum names to flag IDs (for JSON parsing)
    private static readonly Dictionary<string, uint> GreatRuneNameToFlag = new()
    {
        { "Godrick", 171 },
        { "Radahn", 172 },
        { "Morgott", 173 },
        { "Rykard", 174 },
        { "Mohg", 175 },
        { "Malenia", 176 },
        { "Unborn", 197 }
    };

    // Boss event flag IDs - the 30 major bosses tracked for races
    // For multi-phase fights, we track the Phase 2 flag as the completion trigger
    private static readonly Dictionary<uint, string> BossFlags = new()
    {
        { 10000850, "Margit, the Fell Omen" },
        { 10000800, "Godrick the Grafted" },
        { 1043300800, "Leonine Misbegotten" },
        { 1035500800, "Royal Knight Loretta" },
        { 14000850, "Red Wolf of Radagon" },
        { 14000800, "Rennala" },                 // Multi-phase
        { 12080800, "Ancestor Spirit" },
        { 12010800, "Dragonkin Soldier of Nokstella" },
        { 39200800, "Magma Wyrm Makar" },
        { 1039540800, "Elemer of the Briar" },
        { 11000850, "Godfrey, First Elden Lord (Golden Shade)" },
        { 11000800, "Morgott, the Omen King" },
        { 35000800, "Mohg, the Omen" },
        { 1052380800, "Starscourge Radahn" },
        { 12020850, "Mimic Tear" },
        { 12090800, "Regal Ancestor Spirit" },
        { 12020800, "Valiant Gargoyles" },
        { 16000850, "Godskin Noble" },
        { 16000800, "Rykard" },                  // Multi-phase
        { 12040800, "Astel, Naturalborn of the Void" },
        { 1051570800, "Commander Niall" },
        { 1052520800, "Fire Giant" },            // Multi-phase
        { 13000850, "Godskin Duo" },
        { 13000830, "Dragonlord Placidusax" },
        { 13000800, "Maliketh" },               // Multi-phase
        { 15000850, "Loretta, Knight of the Haligtree" },
        { 15000800, "Malenia, Blade of Miquella" },
        { 12050800, "Mohg, Lord of Blood" },
        { 11050800, "Hoarah Loux" },             // Multi-phase
        { 19000800, "Elden Beast" }              // Multi-phase
    };

    // Multi-phase boss mappings: Phase 2 flag -> Phase 1 flag
    private static readonly Dictionary<uint, uint> MultiPhaseBosses = new()
    {
        { 14000800, 14000801 },    // Rennala: Phase 2 -> Phase 1
        { 16000800, 16000801 },    // Rykard: Phase 2 -> Phase 1
        { 1052520800, 1052520801 },// Fire Giant: Phase 2 -> Phase 1
        { 13000800, 13000801 },    // Maliketh: Phase 2 -> Phase 1
        { 11050800, 11050801 },    // Hoarah Loux: Phase 2 -> Phase 1
        { 19000800, 19000810 }     // Elden Beast: Phase 2 -> Phase 1
    };

    // Track which runes have been obtained
    private static readonly Dictionary<uint, bool> ObtainedRunes = new();

    // Track which bosses have been defeated
    private static readonly Dictionary<uint, bool> DefeatedBosses = new();

    // Spoiler log data: rune name -> (area, detailed location)
    private static readonly Dictionary<string, (string Area, string Detail)> RuneLocations = new();

    // Spoiler log data: boss flag ID -> replacement boss name
    private static readonly Dictionary<uint, string> BossReplacements = new();

    // Output file path
    private static string _outputFile = "logs/TrackerLog.txt";

    // Seed from spoiler log
    private static string? _seed;

    // SoulSplitter JSON input path
    private static readonly string SoulSplitterJsonPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SoulSplitter", "tracker_output.json");

    // Unified event list - stores all events in chronological order
    // EventType: "rune" or "boss"
    // For runes: Primary = rune name, Secondary = location
    // For bosses: Primary = original boss, Secondary = replacement boss
    private static readonly List<(string EventType, string Primary, string Secondary, string Time, int IgtMs)> EventList = new();

    static void Main(string[] args)
    {
        Console.Title = "Elden Ring Randomizer Tracker";
        PrintHeader();

        // Parse command line arguments
        string? spoilerPath = null;
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--spoiler" || args[i] == "-s") && i + 1 < args.Length)
            {
                spoilerPath = args[i + 1];
                i++;
            }
            else if ((args[i] == "--output" || args[i] == "-o") && i + 1 < args.Length)
            {
                _outputFile = args[i + 1];
                i++;
            }
            else if (args[i] == "--help" || args[i] == "-h")
            {
                PrintHelp();
                return;
            }
        }

        // Initialize rune tracking state
        foreach (var flag in GreatRuneFlags.Keys)
        {
            ObtainedRunes[flag] = false;
        }

        // Initialize boss tracking state
        foreach (var flag in BossFlags.Keys)
        {
            DefeatedBosses[flag] = false;
        }

        // Try to load spoiler log
        LoadSpoilerLog(spoilerPath);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[STATUS] Waiting for SoulSplitter data...");
        Console.WriteLine($"         Reading from: {SoulSplitterJsonPath}");
        Console.WriteLine("         (Make sure LiveSplit with SoulSplitter is running)");
        Console.ResetColor();

        bool wasConnected = false;
        bool initialStateRead = false;
        DateTime lastModified = DateTime.MinValue;

        while (true)
        {
            try
            {
                if (File.Exists(SoulSplitterJsonPath))
                {
                    var fileInfo = new FileInfo(SoulSplitterJsonPath);

                    // Only process if file was modified
                    if (fileInfo.LastWriteTime > lastModified)
                    {
                        lastModified = fileInfo.LastWriteTime;

                        // Read and parse JSON
                        string json = File.ReadAllText(SoulSplitterJsonPath);
                        var data = ParseTrackerJson(json);

                        if (data != null)
                        {
                            if (!wasConnected)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("\n[STATUS] Connected to SoulSplitter!");
                                Console.ResetColor();
                                wasConnected = true;
                                initialStateRead = false;

                                // Write initial output file
                                WriteOutputFile();
                            }

                            // Read initial state of all flags (to avoid false positives on connect)
                            if (!initialStateRead)
                            {
                                foreach (var rune in data.GreatRunes)
                                {
                                    if (GreatRuneNameToFlag.TryGetValue(rune.Key, out uint flagId))
                                    {
                                        ObtainedRunes[flagId] = rune.Value;
                                    }
                                }
                                foreach (var boss in data.Bosses)
                                {
                                    if (uint.TryParse(boss.Key, out uint flagId))
                                    {
                                        DefeatedBosses[flagId] = boss.Value;
                                    }
                                }
                                initialStateRead = true;

                                // Check if any runes were already obtained
                                int alreadyObtainedRunes = ObtainedRunes.Count(r => r.Value);
                                int alreadyDefeatedBosses = DefeatedBosses.Count(b => b.Value);
                                if (alreadyObtainedRunes > 0 || alreadyDefeatedBosses > 0)
                                {
                                    Console.ForegroundColor = ConsoleColor.Cyan;
                                    if (alreadyObtainedRunes > 0)
                                        Console.WriteLine($"[INFO] {alreadyObtainedRunes} Great Rune(s) already obtained in this save");
                                    if (alreadyDefeatedBosses > 0)
                                        Console.WriteLine($"[INFO] {alreadyDefeatedBosses} Boss(es) already defeated in this save");
                                    Console.ResetColor();
                                }

                                Console.WriteLine("\n[TRACKING] Monitoring for Great Runes and Bosses...\n");
                            }
                            else
                            {
                                // Check for newly defeated bosses FIRST (before runes)
                                foreach (var boss in data.Bosses)
                                {
                                    if (!uint.TryParse(boss.Key, out uint flagId))
                                        continue;

                                    if (!BossFlags.TryGetValue(flagId, out string? bossName))
                                        continue;

                                    bool currentState = boss.Value;

                                    bool previousState = DefeatedBosses.TryGetValue(flagId, out var prev) && prev;
                                    if (currentState && !previousState)
                                    {
                                        // Boss was just defeated!
                                        DefeatedBosses[flagId] = true;

                                        // Get IGT from JSON
                                        int igtMs = data.Igt;
                                        string igtFormatted = FormatTime(igtMs);

                                        // Get replacement boss(es) from spoiler log
                                        string replacementDisplay;
                                        if (MultiPhaseBosses.TryGetValue(flagId, out uint phase1Flag))
                                        {
                                            // Multi-phase boss: get both phase replacements
                                            string phase1Boss = BossReplacements.TryGetValue(phase1Flag, out var p1)
                                                ? p1 : "Unknown";
                                            string phase2Boss = BossReplacements.TryGetValue(flagId, out var p2)
                                                ? p2 : "Unknown";
                                            replacementDisplay = $"{phase1Boss} (Phase 1) {phase2Boss} (Phase 2)";
                                        }
                                        else
                                        {
                                            // Single-phase boss
                                            replacementDisplay = BossReplacements.TryGetValue(flagId, out var replacement)
                                                ? replacement : "Unknown";
                                        }

                                        // Add to unified event list
                                        EventList.Add(("boss", bossName, replacementDisplay, igtFormatted, igtMs));

                                        // Print to console
                                        Console.ForegroundColor = ConsoleColor.Yellow;
                                        Console.WriteLine($"[{igtFormatted}] DEFEATED: {bossName}");
                                        Console.ForegroundColor = ConsoleColor.White;
                                        Console.WriteLine($"           Was: {replacementDisplay}");
                                        Console.ResetColor();

                                        // Update output file
                                        WriteOutputFile();

                                        // Show progress
                                        int obtainedRunes = ObtainedRunes.Count(r => r.Value);
                                        int defeatedBosses = DefeatedBosses.Count(b => b.Value);
                                        Console.ForegroundColor = ConsoleColor.Cyan;
                                        Console.WriteLine($"           Progress: {obtainedRunes}/7 Runes | {defeatedBosses}/30 Bosses\n");
                                        Console.ResetColor();
                                    }
                                }

                                // Check for newly obtained runes AFTER bosses
                                foreach (var rune in data.GreatRunes)
                                {
                                    if (!GreatRuneNameToFlag.TryGetValue(rune.Key, out uint flagId))
                                        continue;

                                    if (!GreatRuneFlags.TryGetValue(flagId, out string? runeName))
                                        continue;

                                    bool currentState = rune.Value;

                                    if (currentState && !ObtainedRunes[flagId])
                                    {
                                        // Rune was just obtained!
                                        ObtainedRunes[flagId] = true;

                                        // Get IGT from JSON
                                        int igtMs = data.Igt;
                                        string igtFormatted = FormatTime(igtMs);

                                        // Get location from spoiler log
                                        string location = GetDisplayLocation(runeName);

                                        // Add to unified event list
                                        EventList.Add(("rune", runeName, location, igtFormatted, igtMs));

                                        // Print to console
                                        Console.ForegroundColor = ConsoleColor.Green;
                                        Console.WriteLine($"[{igtFormatted}] OBTAINED: {runeName}");
                                        Console.ForegroundColor = ConsoleColor.White;
                                        Console.WriteLine($"           Location: {location}");
                                        Console.ResetColor();

                                        // Update output file
                                        WriteOutputFile();

                                        // Show progress
                                        int obtainedRunes = ObtainedRunes.Count(r => r.Value);
                                        int defeatedBosses = DefeatedBosses.Count(b => b.Value);
                                        Console.ForegroundColor = ConsoleColor.Cyan;
                                        Console.WriteLine($"           Progress: {obtainedRunes}/7 Runes | {defeatedBosses}/30 Bosses\n");
                                        Console.ResetColor();
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (wasConnected)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("\n[STATUS] SoulSplitter data file not found");
                        Console.WriteLine("[STATUS] Waiting for SoulSplitter...");
                        Console.ResetColor();
                        wasConnected = false;
                        initialStateRead = false;
                    }
                }
            }
            catch (Exception ex)
            {
                if (wasConnected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[ERROR] {ex.Message}");
                    Console.ResetColor();
                    wasConnected = false;
                    initialStateRead = false;
                }
            }

            Thread.Sleep(100); // Poll every 100ms
        }
    }

    class TrackerData
    {
        public int Igt { get; set; }
        public string? Timestamp { get; set; }
        public bool TimerRunning { get; set; }
        public Dictionary<string, bool> GreatRunes { get; set; } = new();
        public Dictionary<string, bool> Bosses { get; set; } = new();
    }

    static TrackerData? ParseTrackerJson(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var data = new TrackerData
            {
                Igt = root.TryGetProperty("igt", out var igt) ? igt.GetInt32() : 0,
                Timestamp = root.TryGetProperty("timestamp", out var ts) ? ts.GetString() : null,
                TimerRunning = root.TryGetProperty("timerRunning", out var tr) && tr.GetBoolean()
            };

            if (root.TryGetProperty("greatRunes", out var runes))
            {
                foreach (var prop in runes.EnumerateObject())
                {
                    data.GreatRunes[prop.Name] = prop.Value.GetBoolean();
                }
            }

            if (root.TryGetProperty("bosses", out var bosses))
            {
                foreach (var prop in bosses.EnumerateObject())
                {
                    data.Bosses[prop.Name] = prop.Value.GetBoolean();
                }
            }

            return data;
        }
        catch
        {
            return null;
        }
    }

    static string GetDisplayLocation(string runeName)
    {
        if (RuneLocations.TryGetValue(runeName, out var loc))
        {
            // Prefer detailed location, fall back to area
            return !string.IsNullOrEmpty(loc.Detail) ? loc.Detail : loc.Area;
        }
        return "Unknown location";
    }

    static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine(@"
  ╔═══════════════════════════════════════╗
  ║   ELDEN RING RANDOMIZER TRACKER v2.0  ║
  ║     Great Runes + Boss Tracking       ║
  ║        (SoulSplitter Mode)            ║
  ╚═══════════════════════════════════════╝
");
        Console.ResetColor();
    }

    static void PrintHelp()
    {
        Console.WriteLine(@"
Usage: GreatRuneTracker.exe [options]

Tracks Great Rune acquisitions and boss kills in Elden Ring randomizer runs.
Reads flag data from SoulSplitter's JSON output (no direct game memory access).
Events are logged chronologically with IGT timestamps.

IMPORTANT: This tracker requires LiveSplit with SoulSplitter to be running.
           SoulSplitter writes flag states to a JSON file that this tracker reads.

Options:
  -s, --spoiler <path>   Path to spoiler log file or directory
  -o, --output <path>    Output file path (default: logs/TrackerLog.txt)
  -h, --help             Show this help message

Examples:
  GreatRuneTracker.exe
  GreatRuneTracker.exe -s ""C:\path\to\spoiler_logs""
  GreatRuneTracker.exe -s ""C:\path\to\spoiler.txt"" -o ""logs\MyRun.txt""

The tracker will automatically search for spoiler_logs in:
  - ./randomizer/spoiler_logs
  - ./spoiler_logs
  - ../spoiler_logs
  - ../../spoiler_logs
");
    }

    static void LoadSpoilerLog(string? path)
    {
        string? logFile = null;

        if (!string.IsNullOrEmpty(path))
        {
            if (File.Exists(path))
            {
                logFile = path;
            }
            else if (Directory.Exists(path))
            {
                logFile = FindMostRecentSpoilerLog(path);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Spoiler path not found: {path}");
                Console.ResetColor();
            }
        }
        else
        {
            // Try to find spoiler logs in common relative locations
            // Supports: exe next to randomizer folder, or inside randomizer folder
            var searchPaths = new[]
            {
                "randomizer/spoiler_logs",      // exe is next to randomizer folder
                "../randomizer/spoiler_logs",   // exe is one level down from randomizer
                "spoiler_logs",                 // exe is inside randomizer folder
                "../spoiler_logs",
                "../../spoiler_logs",
                "../../../spoiler_logs",
                ".",
                ".."
            };

            foreach (var searchPath in searchPaths)
            {
                try
                {
                    if (Directory.Exists(searchPath))
                    {
                        var found = FindMostRecentSpoilerLog(searchPath);
                        if (found != null)
                        {
                            logFile = found;
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"[INFO] Found spoiler logs in: {Path.GetFullPath(searchPath)}");
                            Console.ResetColor();
                            break;
                        }
                    }
                }
                catch
                {
                    // Ignore access errors
                }
            }
        }

        if (logFile != null)
        {
            ParseSpoilerLog(logFile);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[WARNING] No spoiler log found. Locations will show as 'Unknown'");
            Console.WriteLine("          Use --spoiler <path> to specify spoiler log location");
            Console.WriteLine();
            Console.WriteLine("          Or place this exe so that spoiler_logs is in:");
            Console.WriteLine("          - ./spoiler_logs");
            Console.WriteLine("          - ../spoiler_logs");
            Console.WriteLine("          - ../../spoiler_logs");
            Console.ResetColor();
        }
    }

    static string? FindMostRecentSpoilerLog(string directory)
    {
        try
        {
            // Look for files matching spoiler log naming pattern
            // Pattern: date_time_log_seed_number.txt
            var files = Directory.GetFiles(directory, "*.txt")
                .Select(f => new FileInfo(f))
                .Where(f => f.Name.IndexOf("log", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           Regex.IsMatch(f.Name, @"\d{4}-\d{2}-\d{2}"))
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();

            return files.FirstOrDefault()?.FullName;
        }
        catch
        {
            return null;
        }
    }

    static void ParseSpoilerLog(string filePath)
    {
        try
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[INFO] Loading spoiler log: {Path.GetFileName(filePath)}");
            Console.ResetColor();

            var lines = File.ReadAllLines(filePath);

            // Extract seed from filename (pattern: ..._log_SEED_...)
            var seedMatch = Regex.Match(Path.GetFileName(filePath), @"log_(\d+)");
            if (seedMatch.Success)
            {
                _seed = seedMatch.Groups[1].Value;
            }

            // Also look for seed in file content
            var seedPattern = new Regex(@"Seed:\s*(\d+)", RegexOptions.IgnoreCase);

            // Regex for hints section: "Godrick's Great Rune: In Leyndell"
            var hintsPattern = new Regex(@"^(Godrick's Great Rune|Radahn's Great Rune|Morgott's Great Rune|Rykard's Great Rune|Mohg's Great Rune|Malenia's Great Rune|Great Rune of the Unborn):\s*(?:In\s+)?(.+)$",
                RegexOptions.IgnoreCase);

            // Regex for detailed section: "Godrick's Great Rune in Leyndell: Given by Melina..."
            var detailedPattern = new Regex(@"^(Godrick's Great Rune|Radahn's Great Rune|Morgott's Great Rune|Rykard's Great Rune|Mohg's Great Rune|Malenia's Great Rune|Great Rune of the Unborn)\s+in\s+([^:]+):\s*(.+)$",
                RegexOptions.IgnoreCase);

            // Regex for boss replacements: "Replacing Margit, the Fell Omen (#10000850) in Stormveil Castle: Erdtree Burial Watchdog and Imps (#30010800) from Impaler's Catacombs"
            var bossReplacementPattern = new Regex(@"Replacing\s+(.+?)\s+\(#(\d+)\)\s+in\s+[^:]+:\s+(.+?)\s+\(#\d+\)\s+from",
                RegexOptions.IgnoreCase);

            int bossReplacementsFound = 0;

            foreach (var line in lines)
            {
                // Check for seed in content
                var seedContentMatch = seedPattern.Match(line);

                // Check for boss replacements
                var bossMatch = bossReplacementPattern.Match(line);
                if (bossMatch.Success)
                {
                    string originalBossName = bossMatch.Groups[1].Value.Trim();
                    if (uint.TryParse(bossMatch.Groups[2].Value, out uint flagId))
                    {
                        string replacementBoss = bossMatch.Groups[3].Value.Trim();
                        // Strip " Boss" suffix from replacement names
                        if (replacementBoss.EndsWith(" Boss"))
                        {
                            replacementBoss = replacementBoss.Substring(0, replacementBoss.Length - 5);
                        }
                        BossReplacements[flagId] = replacementBoss;
                        bossReplacementsFound++;
                    }
                    continue;
                }
                if (seedContentMatch.Success && _seed == null)
                {
                    _seed = seedContentMatch.Groups[1].Value;
                }

                // Try detailed format first (more specific)
                var detailedMatch = detailedPattern.Match(line);
                if (detailedMatch.Success)
                {
                    string runeName = NormalizeRuneName(detailedMatch.Groups[1].Value);
                    string area = detailedMatch.Groups[2].Value.Trim();
                    string details = detailedMatch.Groups[3].Value.Trim();

                    // Parse the detailed description to get a concise location
                    string parsedDetail = ParseDetailedLocation(details);

                    // Only update if we don't have detailed info yet, or if this is better
                    if (!RuneLocations.ContainsKey(runeName) || string.IsNullOrEmpty(RuneLocations[runeName].Detail))
                    {
                        RuneLocations[runeName] = (area, parsedDetail);
                    }
                    continue;
                }

                // Try hints format
                var hintsMatch = hintsPattern.Match(line);
                if (hintsMatch.Success)
                {
                    string runeName = NormalizeRuneName(hintsMatch.Groups[1].Value);
                    string area = hintsMatch.Groups[2].Value.Trim();

                    // Only set area if we don't have this rune yet
                    if (!RuneLocations.ContainsKey(runeName))
                    {
                        RuneLocations[runeName] = (area, "");
                    }
                }
            }

            if (_seed != null)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[INFO] Seed: {_seed}");
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[INFO] Spoiler log loaded (locations hidden for racing!)");
            if (bossReplacementsFound > 0)
            {
                Console.WriteLine($"[INFO] Found {bossReplacementsFound} boss replacements");
            }
            Console.ResetColor();

            // Generate output filename based on date and seed, in logs subdirectory
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string seedStr = _seed ?? "unknown";
            string logsDir = "logs";

            // Create logs directory if it doesn't exist
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }

            _outputFile = Path.Combine(logsDir, $"{dateStr}_{seedStr}_TrackerLog.txt");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[INFO] Output file: {_outputFile}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Failed to parse spoiler log: {ex.Message}");
            Console.ResetColor();
        }
    }

    static string ParseDetailedLocation(string details)
    {
        // ============================================
        // SPECIFIC NAMED CHECKS (highest priority)
        // These are well-known checks in the racing community
        // Loaded from CheckPatterns.tsv
        // ============================================

        // Margit, the Fell Omen (not combined with Morgott)
        if (details.IndexOf("Margit", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Margit, the Fell Omen";
        }

        // Castle Sol - Commander Niall drops Haligtree Medallion piece
        if (details.IndexOf("Commander Niall", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Castle Sol Haligtree Medallion Check";
        }

        // Discarded Palace Key chest in Raya Lucaria Grand Library
        if (details.IndexOf("Discarded Palace Key", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Discarded Palace Key Chest Check";
        }

        // Fort Haight tower chest
        if (details.IndexOf("Fort Haight", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Fort Haight Check";
        }

        // Fort Faroth ladder chest
        if (details.IndexOf("Fort Faroth", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Fort Faroth Check";
        }

        // Rusty Key door room in Stormveil
        if (details.IndexOf("Rusty Key door", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Rusty Key Check";
        }

        // Albus the Albinauric (pot disguise in Village of the Albinaurics)
        if (details.IndexOf("Albus", StringComparison.OrdinalIgnoreCase) >= 0 ||
            details.IndexOf("disguised as a pot", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Albus";
        }

        // Academy Glintstone Key guarded by Glintstone Dragon Smarag
        if (details.IndexOf("Glintstone Dragon Smarag", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Academy Glintstone Key Check";
        }

        // Four Belfries chest
        if (details.IndexOf("topmost of the Belfries", StringComparison.OrdinalIgnoreCase) >= 0 ||
            details.IndexOf("Four Belfries", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Belfries Check";
        }

        // Imbued Sword Key on Raya Lucaria rooftop
        if (details.IndexOf("Raya Lucaria rooftop", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Imbued Sword Key Check";
        }

        // Sellia magical barrier chest
        if (details.IndexOf("magical barrier in Sellia", StringComparison.OrdinalIgnoreCase) >= 0 ||
            details.IndexOf("lighting flames around town", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Sellia Check";
        }

        // Divine Tower of Liurnia / Carian Study Hall (Inverted Statue)
        if (details.IndexOf("Divine Tower of Liurnia", StringComparison.OrdinalIgnoreCase) >= 0 ||
            details.IndexOf("Carian Study Hall", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Inverted Statue Check";
        }

        // Tanith in Volcano Manor
        if (details.IndexOf("Given by Tanith", StringComparison.OrdinalIgnoreCase) >= 0 ||
            details.IndexOf("Tanith upon joining", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Tanith";
        }

        // ============================================
        // GENERAL PATTERNS
        // ============================================

        // Pattern: "Dropped by [BOSS]" -> "[BOSS]"
        var droppedMatch = Regex.Match(details, @"Dropped by ([^.]+)", RegexOptions.IgnoreCase);
        if (droppedMatch.Success)
        {
            return droppedMatch.Groups[1].Value.Trim().TrimEnd('.');
        }

        // Pattern: "In a chest unlocked by [KEY]" -> "[KEY] Chest Check"
        var keyMatch = Regex.Match(details, @"In a chest unlocked by ([^.]+?)(?:\s+in\s+|\.|$)", RegexOptions.IgnoreCase);
        if (keyMatch.Success)
        {
            return keyMatch.Groups[1].Value.Trim() + " Chest Check";
        }

        // Pattern: "Given by [NPC]" -> "[NPC]"
        var givenMatch = Regex.Match(details, @"Given by ([^.]+)", RegexOptions.IgnoreCase);
        if (givenMatch.Success)
        {
            return givenMatch.Groups[1].Value.Trim().TrimEnd('.');
        }

        // Pattern: "Sold by [MERCHANT]" -> "[MERCHANT]"
        var soldMatch = Regex.Match(details, @"Sold by ([^.]+)", RegexOptions.IgnoreCase);
        if (soldMatch.Success)
        {
            return soldMatch.Groups[1].Value.Trim().TrimEnd('.');
        }

        // Pattern: "Atop/At/In [LOCATION]" for chest/pickup descriptions
        var locationMatch = Regex.Match(details, @"^(?:Atop|At|In)\s+(.+?)(?:\.|Replaces|$)", RegexOptions.IgnoreCase);
        if (locationMatch.Success)
        {
            return locationMatch.Groups[1].Value.Trim();
        }

        // Fall back to first sentence
        var firstSentence = details.Split('.')[0].Trim();
        return firstSentence.Length > 50 ? firstSentence.Substring(0, 47) + "..." : firstSentence;
    }

    static string NormalizeRuneName(string name)
    {
        // Normalize variations in rune names
        name = name.Trim();

        if (name.IndexOf("Godrick", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Godrick's Great Rune";
        if (name.IndexOf("Radahn", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Radahn's Great Rune";
        if (name.IndexOf("Morgott", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Morgott's Great Rune";
        if (name.IndexOf("Rykard", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Rykard's Great Rune";
        if (name.IndexOf("Mohg", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Mohg's Great Rune";
        if (name.IndexOf("Malenia", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Malenia's Great Rune";
        if (name.IndexOf("Unborn", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Great Rune of the Unborn";

        return name;
    }

    static string FormatTime(int milliseconds)
    {
        var ts = TimeSpan.FromMilliseconds(milliseconds);
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    static void WriteOutputFile()
    {
        try
        {
            // Ensure logs directory exists
            string? dir = Path.GetDirectoryName(_outputFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using var writer = new StreamWriter(_outputFile, false);

            writer.WriteLine("=== ELDEN RING RANDOMIZER TRACKER ===");
            writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            if (_seed != null)
            {
                writer.WriteLine($"Seed: {_seed}");
            }
            writer.WriteLine();

            if (EventList.Count == 0)
            {
                writer.WriteLine("No events recorded yet.");
            }
            else
            {
                writer.WriteLine("--- Event Log ---");
                writer.WriteLine();

                foreach (var evt in EventList)
                {
                    if (evt.EventType == "rune")
                    {
                        writer.WriteLine($"[{evt.Time}] OBTAINED: {evt.Primary}");
                        writer.WriteLine($"           Location: {evt.Secondary}");
                    }
                    else if (evt.EventType == "boss")
                    {
                        writer.WriteLine($"[{evt.Time}] DEFEATED: {evt.Primary}");
                        writer.WriteLine($"           Was: {evt.Secondary}");
                    }
                    writer.WriteLine();
                }
            }

            // Progress summary
            int obtainedRunes = ObtainedRunes.Count(r => r.Value);
            int defeatedBosses = DefeatedBosses.Count(b => b.Value);
            writer.WriteLine($"--- Progress: {obtainedRunes}/7 Runes | {defeatedBosses}/30 Bosses ---");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Failed to write output file: {ex.Message}");
            Console.ResetColor();
        }
    }
}
