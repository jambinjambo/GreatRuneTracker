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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using SoulMemory.EldenRing;
using SoulSplitter.UI.Generic;

namespace SoulSplitter.UI.EldenRing.GreatRuneTracking;

/// <summary>
/// Main ViewModel for the Great Rune Tracker feature.
/// Manages spoiler log parsing, rune tracking, and display settings.
/// </summary>
public class GreatRuneTrackerViewModel : ICustomNotifyPropertyChanged, IDisposable
{
    private readonly SpoilerLogWatcher _watcher;
    private bool _disposed;

    public GreatRuneTrackerViewModel()
    {
        _watcher = new SpoilerLogWatcher();
        _watcher.SpoilerLogDataChanged += OnSpoilerLogDataChanged;

        GreatRunes = new ObservableCollection<GreatRuneLocationViewModel>();

        // Initialize with default/empty rune entries
        foreach (GreatRune rune in Enum.GetValues(typeof(GreatRune)))
        {
            var location = new GreatRuneLocation
            {
                Rune = rune,
                RuneName = GetRuneDisplayName(rune)
            };
            GreatRunes.Add(new GreatRuneLocationViewModel(location));
        }
    }

    #region Properties

    /// <summary>
    /// Whether the Great Rune tracker is enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (this.SetField(ref _isEnabled, value))
            {
                if (value && !string.IsNullOrEmpty(SpoilerLogDirectory))
                {
                    _watcher.StartWatching(SpoilerLogDirectory);
                }
                else if (!value)
                {
                    _watcher.StopWatching();
                }
            }
        }
    }
    private bool _isEnabled;

    /// <summary>
    /// The directory containing spoiler log files.
    /// </summary>
    public string SpoilerLogDirectory
    {
        get => _spoilerLogDirectory;
        set
        {
            if (this.SetField(ref _spoilerLogDirectory, value))
            {
                if (IsEnabled && !string.IsNullOrEmpty(value))
                {
                    _watcher.StartWatching(value);
                }
            }
        }
    }
    private string _spoilerLogDirectory = string.Empty;

    /// <summary>
    /// Whether to automatically detect the most recent spoiler log.
    /// </summary>
    public bool AutoDetectSpoilerLog
    {
        get => _autoDetectSpoilerLog;
        set => this.SetField(ref _autoDetectSpoilerLog, value);
    }
    private bool _autoDetectSpoilerLog = true;

    /// <summary>
    /// The collection of Great Rune location ViewModels.
    /// </summary>
    public ObservableCollection<GreatRuneLocationViewModel> GreatRunes { get; }

    /// <summary>
    /// The currently loaded spoiler log data.
    /// </summary>
    public SpoilerLogData? CurrentSpoilerLog
    {
        get => _currentSpoilerLog;
        private set
        {
            if (this.SetField(ref _currentSpoilerLog, value))
            {
                InvokePropertyChanged(nameof(CurrentSeed));
                InvokePropertyChanged(nameof(StatusText));
                InvokePropertyChanged(nameof(ObtainedCount));
            }
        }
    }
    private SpoilerLogData? _currentSpoilerLog;

    /// <summary>
    /// The seed of the currently loaded spoiler log.
    /// </summary>
    public string CurrentSeed => CurrentSpoilerLog?.Seed ?? "No log loaded";

    /// <summary>
    /// The number of Great Runes obtained.
    /// </summary>
    public int ObtainedCount => GreatRunes.Count(r => r.IsObtained);

    /// <summary>
    /// Status text to display in the UI.
    /// </summary>
    public string StatusText
    {
        get
        {
            if (CurrentSpoilerLog == null)
            {
                return "No spoiler log loaded";
            }
            if (!CurrentSpoilerLog.IsValid)
            {
                return $"Error: {CurrentSpoilerLog.ParseError}";
            }
            return $"Seed: {CurrentSeed} | {ObtainedCount}/7 Great Runes";
        }
    }

    #region UI Settings

    /// <summary>
    /// Background color for the tracker window.
    /// </summary>
    public Color BackgroundColor
    {
        get => _backgroundColor;
        set => this.SetField(ref _backgroundColor, value);
    }
    private Color _backgroundColor = Color.FromArgb(200, 0, 0, 0);

    /// <summary>
    /// Text color for rune names and locations.
    /// </summary>
    public Color TextColor
    {
        get => _textColor;
        set => this.SetField(ref _textColor, value);
    }
    private Color _textColor = Colors.White;

    /// <summary>
    /// Color for obtained runes.
    /// </summary>
    public Color ObtainedColor
    {
        get => _obtainedColor;
        set => this.SetField(ref _obtainedColor, value);
    }
    private Color _obtainedColor = Colors.LimeGreen;

    /// <summary>
    /// Color for runes not yet obtained.
    /// </summary>
    public Color NotObtainedColor
    {
        get => _notObtainedColor;
        set => this.SetField(ref _notObtainedColor, value);
    }
    private Color _notObtainedColor = Colors.Gray;

    /// <summary>
    /// Font size for the tracker display.
    /// </summary>
    public double FontSize
    {
        get => _fontSize;
        set => this.SetField(ref _fontSize, value);
    }
    private double _fontSize = 14;

    /// <summary>
    /// Whether to show detailed location information.
    /// </summary>
    public bool ShowDetailedLocation
    {
        get => _showDetailedLocation;
        set => this.SetField(ref _showDetailedLocation, value);
    }
    private bool _showDetailedLocation = true;

    /// <summary>
    /// Whether to only show obtained runes.
    /// </summary>
    public bool ShowOnlyObtained
    {
        get => _showOnlyObtained;
        set => this.SetField(ref _showOnlyObtained, value);
    }
    private bool _showOnlyObtained = false;

    #endregion

    #endregion

    #region Methods

    /// <summary>
    /// Updates the obtained status of a specific Great Rune.
    /// </summary>
    /// <param name="rune">The rune to update.</param>
    /// <param name="obtained">Whether the rune has been obtained.</param>
    public void UpdateRuneStatus(GreatRune rune, bool obtained)
    {
        var runeVm = GreatRunes.FirstOrDefault(r => r.Rune == rune);
        if (runeVm != null)
        {
            runeVm.IsObtained = obtained;
            InvokePropertyChanged(nameof(ObtainedCount));
            InvokePropertyChanged(nameof(StatusText));
        }
    }

    /// <summary>
    /// Refreshes the spoiler log data.
    /// </summary>
    public void RefreshSpoilerLog()
    {
        _watcher.Refresh();
    }

    /// <summary>
    /// Loads a specific spoiler log file.
    /// </summary>
    public void LoadSpoilerLog(string filePath)
    {
        _watcher.LoadLogFile(filePath);
    }

    /// <summary>
    /// Resets all rune obtained statuses.
    /// </summary>
    public void Reset()
    {
        foreach (var rune in GreatRunes)
        {
            rune.IsObtained = false;
        }
        InvokePropertyChanged(nameof(ObtainedCount));
        InvokePropertyChanged(nameof(StatusText));
    }

    private void OnSpoilerLogDataChanged(object? sender, SpoilerLogData data)
    {
        CurrentSpoilerLog = data;

        if (data.IsValid)
        {
            foreach (var runeVm in GreatRunes)
            {
                var location = data.GetLocation(runeVm.Rune);
                if (location != null)
                {
                    runeVm.UpdateFromLocation(location);
                }
            }
        }
    }

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

    #endregion

    #region ICustomNotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    public void InvokePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _watcher.SpoilerLogDataChanged -= OnSpoilerLogDataChanged;
            _watcher.Dispose();
        }

        _disposed = true;
    }

    #endregion
}
