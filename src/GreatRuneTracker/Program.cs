// Great Rune Tracker - Standalone console app for Elden Ring randomizer runs
// Monitors Great Rune event flags and logs them with locations from spoiler logs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using SoulMemory.EldenRing;

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

    // Track which runes have been obtained
    private static readonly Dictionary<uint, bool> ObtainedRunes = new();

    // Spoiler log data: rune name -> (area, detailed location)
    private static readonly Dictionary<string, (string Area, string Detail)> RuneLocations = new();

    // Output file path
    private static string _outputFile = "GreatRuneLog.txt";

    // Seed from spoiler log
    private static string? _seed;

    // List of obtained runes for output
    private static readonly List<(string Name, string Location, string Time)> ObtainedList = new();

    static void Main(string[] args)
    {
        Console.Title = "Great Rune Tracker";
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

        // Try to load spoiler log
        LoadSpoilerLog(spoilerPath);

        // Connect to game and start tracking
        var eldenRing = new EldenRing();

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[STATUS] Waiting for Elden Ring...");
        Console.WriteLine("         (Make sure EAC is disabled)");
        Console.ResetColor();

        bool wasConnected = false;
        bool initialStateRead = false;

        while (true)
        {
            try
            {
                var result = eldenRing.TryRefresh();

                if (result.IsOk)
                {
                    if (!wasConnected)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\n[STATUS] Connected to Elden Ring!");
                        Console.ResetColor();
                        wasConnected = true;
                        initialStateRead = false;

                        // Write initial output file
                        WriteOutputFile();
                    }

                    // Read initial state of all flags (to avoid false positives on connect)
                    if (!initialStateRead)
                    {
                        foreach (var flag in GreatRuneFlags.Keys)
                        {
                            ObtainedRunes[flag] = eldenRing.ReadEventFlag(flag);
                        }
                        initialStateRead = true;

                        // Check if any runes were already obtained
                        int alreadyObtained = ObtainedRunes.Count(r => r.Value);
                        if (alreadyObtained > 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"[INFO] {alreadyObtained} Great Rune(s) already obtained in this save");
                            Console.ResetColor();
                        }

                        Console.WriteLine("\n[TRACKING] Monitoring for Great Runes...\n");
                    }

                    // Check for newly obtained runes
                    foreach (var kvp in GreatRuneFlags)
                    {
                        uint flagId = kvp.Key;
                        string runeName = kvp.Value;

                        bool currentState = eldenRing.ReadEventFlag(flagId);

                        if (currentState && !ObtainedRunes[flagId])
                        {
                            // Rune was just obtained!
                            ObtainedRunes[flagId] = true;

                            // Get IGT
                            int igtMs = eldenRing.GetInGameTimeMilliseconds();
                            string igtFormatted = FormatTime(igtMs);

                            // Get location from spoiler log
                            string location = GetDisplayLocation(runeName);

                            // Add to list
                            ObtainedList.Add((runeName, location, igtFormatted));

                            // Print to console
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"[{igtFormatted}] OBTAINED: {runeName}");
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.WriteLine($"           Location: {location}");
                            Console.ResetColor();
                            Console.WriteLine();

                            // Update output file
                            WriteOutputFile();

                            // Show progress
                            int obtained = ObtainedRunes.Count(r => r.Value);
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"           Progress: {obtained}/7 Great Runes\n");
                            Console.ResetColor();
                        }
                    }
                }
                else
                {
                    if (wasConnected)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("\n[STATUS] Disconnected from Elden Ring");
                        Console.WriteLine("[STATUS] Waiting for reconnection...");
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
  ║       GREAT RUNE TRACKER v1.0         ║
  ║   For Elden Ring Randomizer Runs      ║
  ╚═══════════════════════════════════════╝
");
        Console.ResetColor();
    }

    static void PrintHelp()
    {
        Console.WriteLine(@"
Usage: GreatRuneTracker.exe [options]

Options:
  -s, --spoiler <path>   Path to spoiler log file or directory
  -o, --output <path>    Output file path (default: GreatRuneLog.txt)
  -h, --help             Show this help message

Examples:
  GreatRuneTracker.exe
  GreatRuneTracker.exe -s ""C:\path\to\spoiler_logs""
  GreatRuneTracker.exe -s ""C:\path\to\spoiler.txt"" -o ""MyRun.txt""

The tracker will automatically search for spoiler_logs in:
  - Current directory
  - ../spoiler_logs (parent folder)
  - ../../spoiler_logs (grandparent folder)
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

            foreach (var line in lines)
            {
                // Check for seed in content
                var seedContentMatch = seedPattern.Match(line);
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
            Console.ResetColor();

            // Generate output filename based on date and seed
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string seedStr = _seed ?? "unknown";
            _outputFile = $"{dateStr}_{seedStr}_GreatRuneLog.txt";
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
            using var writer = new StreamWriter(_outputFile, false);

            writer.WriteLine("=== GREAT RUNE TRACKER ===");
            writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            if (_seed != null)
            {
                writer.WriteLine($"Seed: {_seed}");
            }
            writer.WriteLine();

            if (ObtainedList.Count == 0)
            {
                writer.WriteLine("No Great Runes obtained yet.");
                writer.WriteLine();
                writer.WriteLine("Progress: 0/7 Great Runes");
            }
            else
            {
                writer.WriteLine("--- Great Runes Obtained ---");
                writer.WriteLine();

                for (int i = 0; i < ObtainedList.Count; i++)
                {
                    var (name, location, time) = ObtainedList[i];
                    writer.WriteLine($"{i + 1}. {name}");
                    writer.WriteLine($"   Location: {location}");
                    writer.WriteLine($"   IGT: {time}");
                    writer.WriteLine();
                }

                writer.WriteLine($"--- Progress: {ObtainedList.Count}/7 Great Runes ---");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Failed to write output file: {ex.Message}");
            Console.ResetColor();
        }
    }
}
