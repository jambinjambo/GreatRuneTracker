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
using System.IO;

namespace SoulSplitter.UI.EldenRing.GreatRuneTracking;

/// <summary>
/// Watches the spoiler log directory for new log files and notifies when they are created.
/// </summary>
public class SpoilerLogWatcher : IDisposable
{
    private FileSystemWatcher? _watcher;
    private string _currentDirectory = string.Empty;
    private string? _currentLogPath;
    private SpoilerLogData? _currentData;
    private bool _disposed;

    /// <summary>
    /// Event raised when a new spoiler log file is detected.
    /// </summary>
    public event EventHandler<SpoilerLogData>? NewSpoilerLogDetected;

    /// <summary>
    /// Event raised when the spoiler log data has changed.
    /// </summary>
    public event EventHandler<SpoilerLogData>? SpoilerLogDataChanged;

    /// <summary>
    /// Gets the currently loaded spoiler log data.
    /// </summary>
    public SpoilerLogData? CurrentData => _currentData;

    /// <summary>
    /// Gets the currently monitored directory.
    /// </summary>
    public string CurrentDirectory => _currentDirectory;

    /// <summary>
    /// Gets the path to the currently loaded log file.
    /// </summary>
    public string? CurrentLogPath => _currentLogPath;

    /// <summary>
    /// Starts watching the specified directory for new spoiler log files.
    /// Also loads the most recent log file if one exists.
    /// </summary>
    /// <param name="directory">The directory to watch.</param>
    public void StartWatching(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return;
        }

        StopWatching();

        _currentDirectory = directory;

        // Load the most recent log file
        var mostRecent = SpoilerLogParser.GetMostRecentSpoilerLog(directory);
        if (mostRecent != null)
        {
            LoadLogFile(mostRecent);
        }

        // Set up file system watcher
        _watcher = new FileSystemWatcher(directory, "*.txt")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnFileCreated;
    }

    /// <summary>
    /// Stops watching for new spoiler log files.
    /// </summary>
    public void StopWatching()
    {
        if (_watcher != null)
        {
            _watcher.Created -= OnFileCreated;
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    /// <summary>
    /// Manually loads a specific log file.
    /// </summary>
    /// <param name="filePath">The path to the log file.</param>
    public void LoadLogFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return;
        }

        _currentLogPath = filePath;
        _currentData = SpoilerLogParser.Parse(filePath);
        SpoilerLogDataChanged?.Invoke(this, _currentData);
    }

    /// <summary>
    /// Refreshes the current log data by re-parsing the file.
    /// </summary>
    public void Refresh()
    {
        if (!string.IsNullOrEmpty(_currentLogPath) && File.Exists(_currentLogPath))
        {
            LoadLogFile(_currentLogPath!);
        }
        else if (!string.IsNullOrEmpty(_currentDirectory))
        {
            var mostRecent = SpoilerLogParser.GetMostRecentSpoilerLog(_currentDirectory);
            if (mostRecent != null)
            {
                LoadLogFile(mostRecent);
            }
        }
    }

    /// <summary>
    /// Resets the watcher, clearing all loaded data.
    /// </summary>
    public void Reset()
    {
        _currentLogPath = null;
        _currentData = null;
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        // Verify it's a valid spoiler log file
        var filenameInfo = SpoilerLogParser.ParseFilename(e.Name ?? string.Empty);
        if (!filenameInfo.HasValue)
        {
            return;
        }

        // Small delay to ensure file is fully written
        System.Threading.Thread.Sleep(100);

        // Parse the new log file
        var newData = SpoilerLogParser.Parse(e.FullPath);
        if (newData.IsValid)
        {
            _currentLogPath = e.FullPath;
            _currentData = newData;
            NewSpoilerLogDetected?.Invoke(this, newData);
            SpoilerLogDataChanged?.Invoke(this, newData);
        }
    }

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
            StopWatching();
        }

        _disposed = true;
    }
}
