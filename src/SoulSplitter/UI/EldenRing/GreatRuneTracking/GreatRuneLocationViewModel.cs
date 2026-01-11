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
using System.ComponentModel;
using System.Windows.Media;
using SoulMemory.EldenRing;
using SoulSplitter.UI.Generic;

namespace SoulSplitter.UI.EldenRing.GreatRuneTracking;

/// <summary>
/// ViewModel for displaying a single Great Rune's location and obtained status.
/// </summary>
public class GreatRuneLocationViewModel : ICustomNotifyPropertyChanged
{
    public GreatRuneLocationViewModel(GreatRuneLocation location)
    {
        _location = location;
        Rune = location.Rune;
        UpdateFromLocation();
    }

    private GreatRuneLocation _location;

    /// <summary>
    /// The Great Rune type.
    /// </summary>
    public GreatRune Rune { get; }

    /// <summary>
    /// Short display name (e.g., "Godrick").
    /// </summary>
    public string ShortName
    {
        get => _shortName;
        private set => this.SetField(ref _shortName, value);
    }
    private string _shortName = string.Empty;

    /// <summary>
    /// Full name of the rune (e.g., "Godrick's Great Rune").
    /// </summary>
    public string FullName
    {
        get => _fullName;
        private set => this.SetField(ref _fullName, value);
    }
    private string _fullName = string.Empty;

    /// <summary>
    /// The area where the rune is located.
    /// </summary>
    public string LocationArea
    {
        get => _locationArea;
        private set => this.SetField(ref _locationArea, value);
    }
    private string _locationArea = string.Empty;

    /// <summary>
    /// Detailed location information.
    /// </summary>
    public string DetailedLocation
    {
        get => _detailedLocation;
        private set => this.SetField(ref _detailedLocation, value);
    }
    private string _detailedLocation = string.Empty;

    /// <summary>
    /// Whether this rune has been obtained.
    /// </summary>
    public bool IsObtained
    {
        get => _isObtained;
        set
        {
            if (this.SetField(ref _isObtained, value))
            {
                InvokePropertyChanged(nameof(StatusColor));
                InvokePropertyChanged(nameof(StatusBrush));
                if (value && !_location.ObtainedTime.HasValue)
                {
                    _location.ObtainedTime = DateTime.Now;
                    _location.Obtained = true;
                }
            }
        }
    }
    private bool _isObtained;

    /// <summary>
    /// Color indicating obtained status.
    /// </summary>
    public Color StatusColor => IsObtained ? Colors.LimeGreen : Colors.Gray;

    /// <summary>
    /// Brush for binding to UI elements.
    /// </summary>
    public SolidColorBrush StatusBrush => new(StatusColor);

    /// <summary>
    /// Combined display text showing area and detail.
    /// </summary>
    public string DisplayLocation
    {
        get
        {
            if (!string.IsNullOrEmpty(DetailedLocation))
            {
                return $"{LocationArea}: {DetailedLocation}";
            }
            return LocationArea;
        }
    }

    /// <summary>
    /// Updates this ViewModel from a new GreatRuneLocation.
    /// </summary>
    public void UpdateFromLocation(GreatRuneLocation? location = null)
    {
        if (location != null)
        {
            _location = location;
        }

        FullName = _location.RuneName;
        ShortName = GetShortName(Rune);
        LocationArea = _location.LocationArea;
        DetailedLocation = _location.DetailedLocation;
        IsObtained = _location.Obtained;

        InvokePropertyChanged(nameof(DisplayLocation));
    }

    private static string GetShortName(GreatRune rune)
    {
        return rune switch
        {
            GreatRune.Godrick => "Godrick",
            GreatRune.Radahn => "Radahn",
            GreatRune.Morgott => "Morgott",
            GreatRune.Rykard => "Rykard",
            GreatRune.Mohg => "Mohg",
            GreatRune.Malenia => "Malenia",
            GreatRune.Unborn => "Unborn",
            _ => rune.ToString()
        };
    }

    #region ICustomNotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    public void InvokePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
