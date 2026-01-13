// This file is part of the SoulSplitter distribution (https://github.com/FrankvdStam/SoulSplitter).
// Copyright (c) 2022 Frank van der Stam.
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
using System.IO;
using System.Linq;
using LiveSplit.Model;
using SoulMemory;
using SoulMemory.EldenRing;
using SoulSplitter.Splits.EldenRing;
using SoulSplitter.UI;
using SoulSplitter.UI.EldenRing;
using SoulSplitter.UI.EldenRing.GreatRuneTracking;
using SoulSplitter.UI.Generic;

namespace SoulSplitter.Splitters;

internal class EldenRingSplitter : ISplitter
{
    private readonly EldenRing _eldenRing;
    private EldenRingViewModel _eldenRingViewModel = null!;
    private readonly LiveSplitState _liveSplitState;
    private MainViewModel _mainViewModel= null!;

    // Great Rune tracking
    private Dictionary<GreatRune, bool> _previousGreatRuneStates = new();
    private bool _greatRuneTrackingInitialized;

    // Boss tracking for JSON output
    private Dictionary<uint, bool> _previousBossStates = new();

    // Boss flag IDs (includes multi-phase bosses)
    private static readonly uint[] BossFlags = {
        // Limgrave & Weeping Peninsula
        10000850, // Margit
        10000800, // Godrick
        1043300800, // Leonine Misbegotten
        1035500800, // Tree Sentinel
        // Liurnia
        14000850, // Rennala Phase 1
        14000801, // Rennala Phase 2
        14000800, // Rennala (main)
        12080800, // Royal Knight Loretta
        // Caelid
        12010800, // Starscourge Radahn
        39200800, // Decaying Ekzykes
        1039540800, // Commander O'Neil
        // Altus Plateau & Mt Gelmir
        11000850, // Godskin Apostle (Windmill)
        11000800, // Godskin Apostle (Dominula)
        35000800, // Tibia Mariner (Wyndham)
        1052380800, // Elemer of the Briar
        // Volcano Manor
        12020800, // Abductor Virgins
        12090800, // God-Devouring Serpent
        12020850, // Rykard Phase 1
        16000850, // Rykard Phase 2
        16000801, // Rykard Phase 3
        16000800, // Rykard (main)
        // Leyndell
        12040800, // Godfrey (Golden Shade)
        1051570800, // Draconic Tree Sentinel
        // Capital Outskirts
        1052520801, // Fell Twins
        1052520800, // Fell Twins (main)
        // Mountaintops of the Giants
        13000850, // Fire Giant Phase 1
        13000830, // Fire Giant Phase 2
        13000801, // Fire Giant Phase 3
        13000800, // Fire Giant (main)
        // Forbidden Lands & Consecrated Snowfield
        15000850, // Loretta, Knight of the Haligtree
        15000800, // Loretta (main)
        // Crumbling Farum Azula
        12050800, // Maliketh Phase 1
        // Haligtree
        11050801, // Malenia Phase 1
        11050800, // Malenia (main)
        // Elden Throne
        19000810, // Elden Beast Phase 1
        19000800, // Elden Beast (main)
    };

    // JSON output path
    private static readonly string TrackerOutputPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SoulSplitter", "tracker_output.json");

    public EldenRingSplitter(LiveSplitState state, EldenRing eldenRing)
    {
        _liveSplitState = state;
        _eldenRing = eldenRing;

        _liveSplitState.OnStart += OnStart;
        _liveSplitState.OnReset += OnReset;
        _liveSplitState.IsGameTimePaused = true;

        _timerModel = new TimerModel();
        _timerModel.CurrentState = state;
    }

    public void SetViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _liveSplitState.OnStart -= OnStart;
            _liveSplitState.OnReset -= OnReset;
        }
    }

    public ResultErr<RefreshError> Update(MainViewModel mainViewModel)
    {
        //Settings from the UI
        mainViewModel.TryAndHandleError(() =>
        {
            _eldenRingViewModel = mainViewModel.EldenRingViewModel;
        });


        ResultErr<RefreshError>? result = null;

        //Refresh attachment to ER process
        mainViewModel.TryAndHandleError(() =>
        {
            result = _eldenRing.TryRefresh();
            if (result.IsErr)
            {
                mainViewModel.AddRefreshError(result.GetErr());
            }
        });

        //Lock IGT to 0 if requested
        if (result!.IsOk && _eldenRingViewModel.LockIgtToZero)
        {
            mainViewModel.TryAndHandleError(() => _eldenRing.WriteInGameTimeMilliseconds(0));
            return Result.Ok();//Don't allow other features to be used while locking the timer
        }

        mainViewModel.TryAndHandleError(() =>
        {
            UpdatePosition();
        });

        mainViewModel.TryAndHandleError(() =>
        {
            UpdateTimer(_eldenRingViewModel.StartAutomatically);
        });

        mainViewModel.TryAndHandleError(() =>
        {
            UpdateAutoSplitter();
        });

        mainViewModel.TryAndHandleError(() =>
        {
            mainViewModel.FlagTrackerViewModel.Update(_eldenRing);
        });

        mainViewModel.TryAndHandleError(() =>
        {
            UpdateGreatRuneTracking();
        });

        mainViewModel.TryAndHandleError(() =>
        {
            UpdateTrackerOutput();
        });

        return result!;
    }


    private void UpdatePosition()
    {
        var position = _eldenRing.GetPosition();
        _eldenRingViewModel.CurrentPosition.Area   = position.Area  ;
        _eldenRingViewModel.CurrentPosition.Block  = position.Block ;
        _eldenRingViewModel.CurrentPosition.Region = position.Region;
        _eldenRingViewModel.CurrentPosition.Size   = position.Size  ;
        _eldenRingViewModel.CurrentPosition.X      = position.X     ;
        _eldenRingViewModel.CurrentPosition.Y      = position.Y     ;
        _eldenRingViewModel.CurrentPosition.Z      = position.Z     ;
    }

    private void UpdateGreatRuneTracking()
    {
        var tracker = _eldenRingViewModel.GreatRuneTracker;
        if (!tracker.IsEnabled)
            return;

        if (!_greatRuneTrackingInitialized)
        {
            foreach (GreatRune rune in Enum.GetValues(typeof(GreatRune)))
            {
                _previousGreatRuneStates[rune] = _eldenRing.ReadEventFlag((uint)rune);
            }
            _greatRuneTrackingInitialized = true;
            return;
        }

        foreach (GreatRune rune in Enum.GetValues(typeof(GreatRune)))
        {
            bool currentState = _eldenRing.ReadEventFlag((uint)rune);

            if (currentState && !_previousGreatRuneStates[rune])
            {
                // Rune was just obtained
                tracker.UpdateRuneStatus(rune, true);
            }

            _previousGreatRuneStates[rune] = currentState;
        }
    }

    private void ResetGreatRuneTracking()
    {
        _previousGreatRuneStates.Clear();
        _greatRuneTrackingInitialized = false;
        _eldenRingViewModel.GreatRuneTracker.Reset();
    }

    #region Tracker Output

    // Separate tracking for JSON output (independent of UI tracker)
    private Dictionary<GreatRune, bool> _trackerRuneStates = new();
    private bool _trackerInitialized;
    private int _previousOrdinalCount = 0;

    // Ordinal flags: 181 = "Gets 1st Great Rune", 182 = "Gets 2nd Great Rune", etc.
    // These flags are set when ANY great rune is obtained, regardless of which one
    private static readonly uint[] OrdinalRuneFlags = { 181, 182, 183, 184, 185, 186, 187 };

    // Specific rune flags (171-176) - these identify WHICH rune was obtained
    // Note: Flag 197 (Unborn) may NOT be set in randomizer when obtained from non-Rennala location
    private static readonly GreatRune[] SpecificRunes = { GreatRune.Godrick, GreatRune.Radahn, GreatRune.Morgott, GreatRune.Rykard, GreatRune.Mohg, GreatRune.Malenia };

    private void UpdateTrackerOutput()
    {
        // Count how many ordinal flags are set (how many total runes the player has)
        int GetOrdinalCount()
        {
            int count = 0;
            foreach (var flag in OrdinalRuneFlags)
            {
                if (_eldenRing.ReadEventFlag(flag))
                    count++;
            }
            return count;
        }

        // Check specific rune flags (171-176) - these work reliably
        bool GetSpecificRuneState(GreatRune rune)
        {
            return _eldenRing.ReadEventFlag((uint)rune);
        }

        // For Unborn: Use deduction - if ordinal count > specific flags count, Unborn was obtained
        // Note: Flag 197 is NOT set in randomizer when Unborn is obtained from non-Rennala location
        bool GetUnbornState()
        {
            // First check flag 197 (works in vanilla, not in randomizer)
            if (_eldenRing.ReadEventFlag((uint)GreatRune.Unborn))
                return true;

            // Deduction method: Count ordinal vs specific flags
            int ordinalCount = GetOrdinalCount();
            int specificCount = 0;
            foreach (var rune in SpecificRunes)
            {
                if (_eldenRing.ReadEventFlag((uint)rune))
                    specificCount++;
            }

            // If player has more runes (ordinal) than identified specific runes, Unborn was obtained
            return ordinalCount > specificCount;
        }

        // Initialize all states on first run
        if (!_trackerInitialized)
        {
            foreach (var flagId in BossFlags)
            {
                _previousBossStates[flagId] = _eldenRing.ReadEventFlag(flagId);
            }
            // Initialize specific rune states
            foreach (var rune in SpecificRunes)
            {
                _trackerRuneStates[rune] = GetSpecificRuneState(rune);
            }
            // Initialize Unborn state using deduction
            _trackerRuneStates[GreatRune.Unborn] = GetUnbornState();
            _previousOrdinalCount = GetOrdinalCount();
            _trackerInitialized = true;
            WriteTrackerOutput();
            return;
        }

        // Check for any changes
        bool hasChanges = false;

        // Check boss changes
        foreach (var flagId in BossFlags)
        {
            bool currentState = _eldenRing.ReadEventFlag(flagId);
            if (currentState != _previousBossStates[flagId])
            {
                _previousBossStates[flagId] = currentState;
                hasChanges = true;
            }
        }

        // Check Great Rune changes
        // For specific runes (Godrick, Radahn, etc.) - use their specific flags (171-176)
        // For Unborn - use deduction method since flag 197 may not be set in randomizer
        foreach (var rune in SpecificRunes)
        {
            bool currentState = GetSpecificRuneState(rune);
            bool prevState = _trackerRuneStates.TryGetValue(rune, out var prev) && prev;
            if (currentState != prevState)
            {
                _trackerRuneStates[rune] = currentState;
                hasChanges = true;
            }
        }
        // Check Unborn separately using deduction
        {
            bool currentState = GetUnbornState();
            bool prevState = _trackerRuneStates.TryGetValue(GreatRune.Unborn, out var prev) && prev;
            if (currentState != prevState)
            {
                _trackerRuneStates[GreatRune.Unborn] = currentState;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            WriteTrackerOutput();
        }
    }

    private void WriteTrackerOutput()
    {
        try
        {
            // Ensure directory exists
            var dir = Path.GetDirectoryName(TrackerOutputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Build JSON manually to avoid dependencies
            var lines = new List<string>();
            lines.Add("{");
            lines.Add($"  \"igt\": {_inGameTime},");
            lines.Add($"  \"timestamp\": \"{DateTime.UtcNow:o}\",");
            lines.Add($"  \"timerRunning\": {(_timerState == TimerState.Running).ToString().ToLower()},");

            // Great Runes - use tracked states (which use deduction for Unborn)
            lines.Add("  \"greatRunes\": {");
            var runeEntries = new List<string>();
            foreach (GreatRune rune in Enum.GetValues(typeof(GreatRune)))
            {
                // Use the tracked state which already handles Unborn via deduction
                bool state = _trackerRuneStates.TryGetValue(rune, out var s) && s;
                runeEntries.Add($"    \"{rune}\": {state.ToString().ToLower()}");
            }
            lines.Add(string.Join(",\n", runeEntries));
            lines.Add("  },");

            // Bosses - read directly from game memory
            lines.Add("  \"bosses\": {");
            var bossEntries = new List<string>();
            foreach (var flagId in BossFlags)
            {
                bool state = _eldenRing.ReadEventFlag(flagId);
                bossEntries.Add($"    \"{flagId}\": {state.ToString().ToLower()}");
            }
            lines.Add(string.Join(",\n", bossEntries));
            lines.Add("  }");

            lines.Add("}");

            File.WriteAllText(TrackerOutputPath, string.Join("\n", lines));
        }
        catch
        {
            // Silently fail - don't crash SoulSplitter for tracker output
        }
    }

    private void ResetTrackerOutput()
    {
        _previousBossStates.Clear();
        _trackerRuneStates.Clear();
        _previousOrdinalCount = 0;
        _trackerInitialized = false;

        // Write initial state
        WriteTrackerOutput();
    }

    #endregion

    //Starting the timer by calling Start(); on a TimerModel object will trigger more than just SoulSplitter's start event.
    //It occurred at least twice that another plugin would throw exceptions during the start event, causing SoulSplitter's start event to never be called at all.
    //That in turn never changed the timer state to running. We can not rely on this event.
    //Thats why autostarting will take care of this and doesn't need the event.
    //However, we still need this event when players start the timer manually.
    private void OnStart(object sender, EventArgs e)
    {
        StartTimer();
        StartAutoSplitting(_eldenRingViewModel);
        _mainViewModel.FlagTrackerViewModel.Start();
    }

    private void OnReset(object sender, TimerPhase timerPhase)
    {
        ResetTimer();
        ResetAutoSplitting();
        _mainViewModel.FlagTrackerViewModel.Reset();
        ResetGreatRuneTracking();
        ResetTrackerOutput();
    }

    #region Timer
    private readonly TimerModel _timerModel;
    private int _inGameTime;
    private TimerState _timerState = TimerState.WaitForStart;
    private bool _startAutomatically;

    private void StartTimer()
    {
        _liveSplitState.IsGameTimePaused = true;
        _timerState = TimerState.Running;
        _eldenRing.EnableHud();
    }

    private void ResetTimer()
    {
        _timerState = TimerState.WaitForStart;
        _inGameTime = 0;
    }


    public void UpdateTimer(bool startAutomatically)
    {
        //Allow updates from the UI only when a run isn't in progress
        if (_timerState == TimerState.WaitForStart)
        {
            _startAutomatically = startAutomatically;
        }

        switch (_timerState)
        {
            case TimerState.WaitForStart:
                if (_startAutomatically)
                {
                    var igt = _eldenRing.GetInGameTimeMilliseconds();
                    if (igt is > 0 and < 150)
                    {
                        _eldenRing.WriteInGameTimeMilliseconds(0);
                        StartTimer();
                        _timerModel.Start();
                        StartAutoSplitting(_eldenRingViewModel);
                    }
                }
                break;

            case TimerState.Running:

                var currentIgt = _eldenRing.GetInGameTimeMilliseconds();
                var blackscreenActive = _eldenRing.IsBlackscreenActive();


                //Blackscreens/meme loading screens - timer is running, but game is actually loading
                if (currentIgt != 0 && currentIgt > _inGameTime && currentIgt < _inGameTime + 1000 && blackscreenActive)
                {
                    _eldenRing.WriteInGameTimeMilliseconds(_inGameTime);
                }
                else
                {
                    if (currentIgt != 0)
                    {
                        _inGameTime = currentIgt;
                    }
                }
                _timerModel.CurrentState.SetGameTime(TimeSpan.FromMilliseconds(_inGameTime));
                break;
        }
    }

    #endregion

    #region Autosplitting

    private List<Split> _splits = [];

    public void ResetAutoSplitting()
    {
        _splits.Clear();
    }

    public void StartAutoSplitting(EldenRingViewModel eldenRingViewModel)
    {
        _splits = (
            from timingType in eldenRingViewModel.Splits
            from splitType in timingType.Children
            from split in splitType.Children
            select new Split(timingType.TimingType, splitType.EldenRingSplitType, split.Split)
            ).ToList();
    }

    public void UpdateAutoSplitter()
    {
        if (_timerState != TimerState.Running)
        {
            return;
        }

        List<Item>? inventoryItems = null;

        foreach (var s in _splits)
        {
            if (!s.SplitTriggered)
            {
                if (!s.SplitConditionMet)
                {
                    switch (s.EldenRingSplitType)
                    {
                        default:
                            throw new ArgumentException($"Unsupported split type {s.EldenRingSplitType}");

                        case EldenRingSplitType.Boss:
                        case EldenRingSplitType.Grace:
                        case EldenRingSplitType.ItemPickup:
                        case EldenRingSplitType.KnownFlag:
                        case EldenRingSplitType.Flag:
                            s.SplitConditionMet = _eldenRing.ReadEventFlag(s.Flag);
                            break;

                        case EldenRingSplitType.Item:
                            //Only get the inventory items once per livesplit tick
                            inventoryItems ??= _eldenRing.ReadInventory();
                            s.SplitConditionMet = inventoryItems.Any(i => i.Category == s.Item.Category && i.Id == s.Item.Id);
                            break;

                        case EldenRingSplitType.Position:
                            if (
                                _eldenRingViewModel.CurrentPosition.Area    == s.Position.Area &&
                                _eldenRingViewModel.CurrentPosition.Block   == s.Position.Block &&
                                _eldenRingViewModel.CurrentPosition.Region  == s.Position.Region &&
                                _eldenRingViewModel.CurrentPosition.Size    == s.Position.Size &&

                                s.Position.X + 5.0f > _eldenRingViewModel.CurrentPosition.X &&
                                s.Position.X - 5.0f < _eldenRingViewModel.CurrentPosition.X &&

                                s.Position.Y + 5.0f > _eldenRingViewModel.CurrentPosition.Y &&
                                s.Position.Y - 5.0f < _eldenRingViewModel.CurrentPosition.Y &&

                                s.Position.Z + 5.0f > _eldenRingViewModel.CurrentPosition.Z &&
                                s.Position.Z - 5.0f < _eldenRingViewModel.CurrentPosition.Z)
                            {
                                s.SplitConditionMet = true;
                            }
                            break;
                    }
                }

                if (s.SplitConditionMet)
                {
                    ResolveSplitTiming(s);
                }
            }
        }
    }

    private void ResolveSplitTiming(Split s)
    {
        switch (s.TimingType)
        {
            default:
                throw new ArgumentException($"Unsupported timing type {s.TimingType}");

            case TimingType.Immediate:
                _timerModel.Split();
                s.SplitTriggered = true;
                break;

            case TimingType.OnLoading:
                if (_eldenRing.GetScreenState() == ScreenState.Loading)
                {
                    _timerModel.Split();
                    s.SplitTriggered = true;
                }
                break;

            case TimingType.OnBlackscreen:
                if (_eldenRing.IsBlackscreenActive())
                {
                    _timerModel.Split();
                    s.SplitTriggered = true;
                }
                break;
        }
    }


    #endregion
}
