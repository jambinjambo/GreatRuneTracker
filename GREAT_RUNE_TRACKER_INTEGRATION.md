# Great Rune Tracker Integration Guide

This guide explains how to integrate the Great Rune Tracker into the existing SoulSplitter codebase.

## Files Created

All new files are in place:
- `src/SoulMemory/EldenRing/GreatRune.cs` - Great Rune enum with flag IDs
- `src/SoulSplitter/UI/EldenRing/GreatRuneTracking/` - All tracker components

## Required Modifications to Existing Files

### 1. EldenRingViewModel.cs

Add the using statement:
```csharp
using SoulSplitter.UI.EldenRing.GreatRuneTracking;
```

Add the GreatRuneTracker property after the existing properties:
```csharp
[XmlIgnore]
public GreatRuneTrackerViewModel GreatRuneTracker
{
    get => _greatRuneTracker;
    set => this.SetField(ref _greatRuneTracker, value);
}
private GreatRuneTrackerViewModel _greatRuneTracker = new();
```

### 2. EldenRingSplitter.cs

Add using statement:
```csharp
using SoulMemory.EldenRing;
using SoulSplitter.UI.EldenRing.GreatRuneTracking;
```

Add tracking fields:
```csharp
private Dictionary<GreatRune, bool> _previousGreatRuneStates = new();
private bool _greatRuneTrackingInitialized;
```

In the Update() method, add Great Rune flag monitoring:
```csharp
private void UpdateGreatRuneTracking(IEldenRing eldenRing, GreatRuneTrackerViewModel tracker)
{
    if (!tracker.IsEnabled)
        return;

    if (!_greatRuneTrackingInitialized)
    {
        foreach (GreatRune rune in Enum.GetValues(typeof(GreatRune)))
        {
            _previousGreatRuneStates[rune] = eldenRing.ReadEventFlag((uint)rune);
        }
        _greatRuneTrackingInitialized = true;
        return;
    }

    foreach (GreatRune rune in Enum.GetValues(typeof(GreatRune)))
    {
        bool currentState = eldenRing.ReadEventFlag((uint)rune);

        if (currentState && !_previousGreatRuneStates[rune])
        {
            // Rune was just obtained
            tracker.UpdateRuneStatus(rune, true);
        }

        _previousGreatRuneStates[rune] = currentState;
    }
}
```

### 3. EldenRingControl.xaml (Optional UI Integration)

Add a button to open the tracker window. In the XAML, add a new row and an expander:

```xml
<!-- Great Rune Tracker -->
<Expander Grid.Row="7" Header="Great Rune Tracker (Randomizer)" Margin="5">
    <StackPanel>
        <CheckBox Content="Enable Great Rune Tracking"
                  IsChecked="{Binding GreatRuneTracker.IsEnabled}"/>

        <StackPanel Orientation="Horizontal" Margin="0,10,0,0">
            <TextBlock Text="Spoiler Log Directory:" VerticalAlignment="Center"/>
            <TextBox Text="{Binding GreatRuneTracker.SpoilerLogDirectory}"
                     Width="250" Margin="10,0"/>
        </StackPanel>

        <Button Content="Open Tracker Window"
                Margin="0,10,0,0"
                HorizontalAlignment="Left"
                Click="OpenGreatRuneTracker_Click"/>
    </StackPanel>
</Expander>
```

Add the click handler in EldenRingControl.xaml.cs:
```csharp
private GreatRuneTrackerWindow? _trackerWindow;

private void OpenGreatRuneTracker_Click(object sender, RoutedEventArgs e)
{
    if (DataContext is EldenRingViewModel vm)
    {
        if (_trackerWindow == null || !_trackerWindow.IsVisible)
        {
            _trackerWindow = new GreatRuneTrackerWindow
            {
                DataContext = vm.GreatRuneTracker
            };
        }
        _trackerWindow.Show();
        _trackerWindow.Activate();
    }
}
```

## Default Spoiler Log Directory

Set the default spoiler log directory when the tracker is initialized:
```csharp
SpoilerLogDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\mods\randomizer\spoiler_logs"
```

## Testing

1. Build the solution in Visual Studio
2. Copy the built DLLs to your LiveSplit/Components folder
3. Start Elden Ring with a randomizer run
4. Open the Great Rune Tracker window
5. The tracker should auto-detect your spoiler log
6. When you obtain a Great Rune, it should update the display

## Great Rune Event Flags

| Rune | Flag ID |
|------|---------|
| Godrick's Great Rune | 171 |
| Radahn's Great Rune | 172 |
| Morgott's Great Rune | 173 |
| Rykard's Great Rune | 174 |
| Mohg's Great Rune | 175 |
| Malenia's Great Rune | 176 |
| Great Rune of the Unborn | 197 |
