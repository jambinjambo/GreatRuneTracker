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
using SoulMemory.EldenRing;

namespace SoulSplitter.UI.EldenRing.GreatRuneTracking;

/// <summary>
/// Contains parsed data from a randomizer spoiler log file.
/// </summary>
public class SpoilerLogData
{
    /// <summary>
    /// The full file path to the spoiler log.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// The timestamp when the log file was created (parsed from filename).
    /// </summary>
    public DateTime FileTimestamp { get; set; }

    /// <summary>
    /// The randomizer seed used for this run.
    /// </summary>
    public string Seed { get; set; } = string.Empty;

    /// <summary>
    /// Dictionary mapping each Great Rune to its location information.
    /// </summary>
    public Dictionary<GreatRune, GreatRuneLocation> GreatRuneLocations { get; set; } = new();

    /// <summary>
    /// Whether the spoiler log was parsed successfully.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Error message if parsing failed.
    /// </summary>
    public string? ParseError { get; set; }

    /// <summary>
    /// Gets the location for a specific Great Rune, or null if not found.
    /// </summary>
    public GreatRuneLocation? GetLocation(GreatRune rune)
    {
        return GreatRuneLocations.TryGetValue(rune, out var location) ? location : null;
    }
}
