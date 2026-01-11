// This file is part of the SoulSplitter distribution (https://github.com/FrankvdStam/SoulSplitter).
// Copyright (c) 2024 Frank van der Stam.
// https://github.com/FrankvdStam/SoulSplitter/blob/main/LICENSE
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, version 3.
//
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SoulMemory.EldenRing;

namespace SoulSplitter.UI.EldenRing.GreatRuneTracking;

/// <summary>
/// Parses Elden Ring Randomizer spoiler log files to extract Great Rune location information.
/// </summary>
public static class SpoilerLogParser
{
    // Map of rune display names to enum values
    private static readonly Dictionary<string, GreatRune> RuneNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Godrick's Great Rune", GreatRune.Godrick },
        { "Radahn's Great Rune", GreatRune.Radahn },
        { "Morgott's Great Rune", GreatRune.Morgott },
        { "Rykard's Great Rune", GreatRune.Rykard },
        { "Mohg's Great Rune", GreatRune.Mohg },
        { "Malenia's Great Rune", GreatRune.Malenia },
        { "Great Rune of the Unborn", GreatRune.Unborn }
    };

    // Regex for hints section: "Godrick's Great Rune: In Haligtree"
    private static readonly Regex HintPattern = new(
        @"^(Godrick's Great Rune|Radahn's Great Rune|Morgott's Great Rune|Rykard's Great Rune|Mohg's Great Rune|Malenia's Great Rune|Great Rune of the Unborn):\s*In\s+(.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for detailed section: "Godrick's Great Rune in Haligtree: Dropped by Malenia. Replaces Remembrance of the Rot Goddess."
    private static readonly Regex DetailedPattern = new(
        @"^(Godrick's Great Rune|Radahn's Great Rune|Morgott's Great Rune|Rykard's Great Rune|Mohg's Great Rune|Malenia's Great Rune|Great Rune of the Unborn)\s+in\s+([^:]+):\s*(.+?)\.\s*Replaces\s+(.+)\.$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for filename: "2026-01-09_21.18.52_log_2005756270_31785.txt"
    private static readonly Regex FilenamePattern = new(
        @"^(\d{4}-\d{2}-\d{2})_(\d{2}\.\d{2}\.\d{2})_log_(\d+)_(\d+)\.txt$",
        RegexOptions.Compiled);

    /// <summary>
    /// Finds the most recent spoiler log file in the specified directory.
    /// </summary>
    /// <param name="directory">The directory containing spoiler log files.</param>
    /// <returns>The full path to the most recent log file, or null if none found.</returns>
    public static string? GetMostRecentSpoilerLog(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        try
        {
            var logFiles = Directory.GetFiles(directory, "*.txt")
                .Select(f => new { Path = f, Info = ParseFilename(Path.GetFileName(f)) })
                .Where(f => f.Info.HasValue)
                .OrderByDescending(f => f.Info!.Value.Timestamp)
                .FirstOrDefault();

            return logFiles?.Path;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a spoiler log filename to extract metadata.
    /// </summary>
    /// <param name="filename">The filename (not full path).</param>
    /// <returns>Parsed information or null if parsing failed.</returns>
    public static (DateTime Timestamp, string Seed, string Options)? ParseFilename(string filename)
    {
        var match = FilenamePattern.Match(filename);
        if (!match.Success)
        {
            return null;
        }

        var dateStr = match.Groups[1].Value;
        var timeStr = match.Groups[2].Value.Replace('.', ':');
        var seed = match.Groups[3].Value;
        var options = match.Groups[4].Value;

        if (DateTime.TryParseExact($"{dateStr} {timeStr}", "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
        {
            return (timestamp, seed, options);
        }

        return null;
    }

    /// <summary>
    /// Parses a spoiler log file and extracts Great Rune locations.
    /// </summary>
    /// <param name="filePath">The full path to the spoiler log file.</param>
    /// <returns>Parsed spoiler log data.</returns>
    public static SpoilerLogData Parse(string filePath)
    {
        var data = new SpoilerLogData
        {
            FilePath = filePath
        };

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            data.IsValid = false;
            data.ParseError = "File not found";
            return data;
        }

        try
        {
            // Parse filename for metadata
            var filename = Path.GetFileName(filePath);
            var filenameInfo = ParseFilename(filename);
            if (filenameInfo.HasValue)
            {
                data.FileTimestamp = filenameInfo.Value.Timestamp;
                data.Seed = filenameInfo.Value.Seed;
            }

            // Initialize all runes with empty locations
            foreach (GreatRune rune in Enum.GetValues(typeof(GreatRune)))
            {
                data.GreatRuneLocations[rune] = new GreatRuneLocation
                {
                    Rune = rune,
                    RuneName = GetRuneDisplayName(rune)
                };
            }

            // Read and parse the file
            var lines = File.ReadAllLines(filePath);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Try to match detailed pattern first (more specific info)
                var detailedMatch = DetailedPattern.Match(trimmedLine);
                if (detailedMatch.Success)
                {
                    var runeName = detailedMatch.Groups[1].Value;
                    var area = detailedMatch.Groups[2].Value.Trim();
                    var detail = detailedMatch.Groups[3].Value.Trim();
                    var replaces = detailedMatch.Groups[4].Value.Trim();

                    if (RuneNameMap.TryGetValue(runeName, out var rune))
                    {
                        var location = data.GreatRuneLocations[rune];
                        location.LocationArea = area;
                        location.DetailedLocation = detail;
                        location.ReplacesItem = replaces;
                    }
                    continue;
                }

                // Try hint pattern (simple area info)
                var hintMatch = HintPattern.Match(trimmedLine);
                if (hintMatch.Success)
                {
                    var runeName = hintMatch.Groups[1].Value;
                    var area = hintMatch.Groups[2].Value.Trim();

                    if (RuneNameMap.TryGetValue(runeName, out var rune))
                    {
                        var location = data.GreatRuneLocations[rune];
                        // Only set area if not already set by detailed pattern
                        if (string.IsNullOrEmpty(location.LocationArea))
                        {
                            location.LocationArea = area;
                        }
                    }
                }
            }

            data.IsValid = true;
        }
        catch (Exception ex)
        {
            data.IsValid = false;
            data.ParseError = ex.Message;
        }

        return data;
    }

    /// <summary>
    /// Gets the display name for a Great Rune.
    /// </summary>
    private static string GetRuneDisplayName(GreatRune rune)
    {
        return rune switch
        {
            GreatRune.Godrick => "Godrick's Great Rune",
            GreatRune.Radahn => "Radahn's Great Rune",
            GreatRune.Morgott => "Morgott's Great Rune",
            GreatRune.Rykard => "Rykard's Great Rune",
            GreatRune.Mohg => "Mohg's Great Rune",
            GreatRune.Malenia => "Malenia's Great Rune",
            GreatRune.Unborn => "Great Rune of the Unborn",
            _ => rune.ToString()
        };
    }
}
