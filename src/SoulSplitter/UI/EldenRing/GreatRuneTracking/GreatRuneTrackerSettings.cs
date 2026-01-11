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
using System.Xml.Serialization;

namespace SoulSplitter.UI.EldenRing.GreatRuneTracking;

/// <summary>
/// Serializable settings for the Great Rune Tracker.
/// </summary>
[Serializable]
[XmlType(Namespace = "SoulSplitter")]
public class GreatRuneTrackerSettings
{
    /// <summary>
    /// Whether the tracker is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The directory containing spoiler log files.
    /// </summary>
    public string SpoilerLogDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Whether to automatically detect the most recent log.
    /// </summary>
    public bool AutoDetectLog { get; set; } = true;

    #region UI Settings

    /// <summary>
    /// Background color (ARGB hex string).
    /// </summary>
    public string BackgroundColor { get; set; } = "#C8000000"; // Semi-transparent black

    /// <summary>
    /// Text color (ARGB hex string).
    /// </summary>
    public string TextColor { get; set; } = "#FFFFFFFF"; // White

    /// <summary>
    /// Color for obtained runes (ARGB hex string).
    /// </summary>
    public string ObtainedColor { get; set; } = "#FF32CD32"; // LimeGreen

    /// <summary>
    /// Color for runes not yet obtained (ARGB hex string).
    /// </summary>
    public string NotObtainedColor { get; set; } = "#FF808080"; // Gray

    /// <summary>
    /// Font size for the tracker display.
    /// </summary>
    public double FontSize { get; set; } = 14;

    /// <summary>
    /// Whether to show detailed location information.
    /// </summary>
    public bool ShowDetailedLocation { get; set; } = true;

    /// <summary>
    /// Whether to only show obtained runes.
    /// </summary>
    public bool ShowOnlyObtained { get; set; }

    #endregion

    #region Window Position

    /// <summary>
    /// Left position of the tracker window.
    /// </summary>
    public double WindowLeft { get; set; } = 100;

    /// <summary>
    /// Top position of the tracker window.
    /// </summary>
    public double WindowTop { get; set; } = 100;

    /// <summary>
    /// Width of the tracker window.
    /// </summary>
    public double WindowWidth { get; set; } = 300;

    /// <summary>
    /// Height of the tracker window.
    /// </summary>
    public double WindowHeight { get; set; } = 400;

    #endregion
}
