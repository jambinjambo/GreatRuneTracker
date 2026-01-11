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
using SoulMemory.EldenRing;

namespace SoulSplitter.UI.EldenRing.GreatRuneTracking;

/// <summary>
/// Represents the location information for a Great Rune parsed from a randomizer spoiler log.
/// </summary>
public class GreatRuneLocation
{
    /// <summary>
    /// The specific Great Rune type.
    /// </summary>
    public GreatRune Rune { get; set; }

    /// <summary>
    /// The full display name of the rune (e.g., "Godrick's Great Rune").
    /// </summary>
    public string RuneName { get; set; } = string.Empty;

    /// <summary>
    /// The general area/region where the rune is located (e.g., "Haligtree", "Liurnia").
    /// Parsed from the hints section of the spoiler log.
    /// </summary>
    public string LocationArea { get; set; } = string.Empty;

    /// <summary>
    /// The detailed location description (e.g., "Dropped by Royal Knight Loretta").
    /// Parsed from the spoilers section of the log.
    /// </summary>
    public string DetailedLocation { get; set; } = string.Empty;

    /// <summary>
    /// The original item that this Great Rune replaces at this location.
    /// </summary>
    public string ReplacesItem { get; set; } = string.Empty;

    /// <summary>
    /// Whether the player has obtained this Great Rune.
    /// </summary>
    public bool Obtained { get; set; }

    /// <summary>
    /// The time when the rune was obtained (null if not yet obtained).
    /// </summary>
    public DateTime? ObtainedTime { get; set; }

    /// <summary>
    /// Returns a display-friendly string for the location.
    /// Uses detailed location if available, otherwise falls back to area.
    /// </summary>
    public string GetDisplayLocation()
    {
        if (!string.IsNullOrEmpty(DetailedLocation))
        {
            return $"{LocationArea}: {DetailedLocation}";
        }
        return LocationArea;
    }
}
