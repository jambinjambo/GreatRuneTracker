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

namespace SoulMemory.EldenRing;

/// <summary>
/// Event flags for specific Great Runes (not ordinal flags like 181-187).
/// These flags indicate which specific Great Rune the player has obtained.
/// </summary>
public enum GreatRune : uint
{
    [Annotation(Name = "Godrick's Great Rune", Description = "Stormveil Castle")]
    Godrick = 171,

    [Annotation(Name = "Radahn's Great Rune", Description = "Redmane Castle")]
    Radahn = 172,

    [Annotation(Name = "Morgott's Great Rune", Description = "Leyndell")]
    Morgott = 173,

    [Annotation(Name = "Rykard's Great Rune", Description = "Volcano Manor")]
    Rykard = 174,

    [Annotation(Name = "Mohg's Great Rune", Description = "Mohgwyn Palace")]
    Mohg = 175,

    [Annotation(Name = "Malenia's Great Rune", Description = "Miquella's Haligtree")]
    Malenia = 176,

    [Annotation(Name = "Great Rune of the Unborn", Description = "Academy of Raya Lucaria")]
    Unborn = 197
}
