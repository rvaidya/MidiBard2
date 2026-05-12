using System;
using System.Linq;

using MidiBard.Control.MidiControl.Editing;

namespace MidiBard;

public partial class MidiEditorWindow
{
    private void CaptureHistorySnapshot()
    {
        if (_file == null) return;
        _history.Capture(_file);
    }

    private bool ExecuteDirectEdit(Func<bool> edit, MidiEditorTransformResult? refresh = null)
    {
        if (_file == null)
            return false;

        var committed = MidiEditorDirectEditExecutor.Execute(_history, _file, edit);
        if (committed && refresh != null)
            ApplyTransformRefresh(refresh);

        return committed;
    }

    private MidiEditorTransformExecutionResult ExecuteEditorTransform<TOptions>(
        IMidiEditorTransform<TOptions> transform,
        TOptions options,
        int[]? selectedTrackIndices = null)
    {
        if (_file == null)
            return MidiEditorTransformExecutionResult.ValidationFailed("No MIDI file is loaded.");

        var context = new MidiEditorTransformContext(
            _file,
            selectedTrackIndices ?? GetSelectedPerformanceTrackIndices(),
            _selectedTrackIndex,
            _selectedEventIndices.ToArray());

        var execution = _transformExecutor.Execute(context, transform, options);
        if (execution.Changed)
            ApplyTransformRefresh(execution.Result);

        return execution;
    }

    private void ApplyTransformRefresh(MidiEditorTransformResult result)
    {
        if (result.ReloadSelectedTrack
            && _file != null
            && _selectedTrackIndex >= 0
            && _selectedTrackIndex < _file.Tracks.Count)
        {
            _file.Tracks[_selectedTrackIndex].LoadEvents(_file.TempoMap);
        }

        if (result.ClearEventSelection)
        {
            _selectedEventIndices.Clear();
            _globalEventsChecked = false;
        }

        if (result.ClearTrackSelection)
        {
            _selectedTrackIndices.Clear();
            _globalTracksChecked = false;
        }

        if (result.ClearSelectedTrack)
            SelectTrack(-1);
    }

    private void BeginGestureHistoryScope()
        => _gestureHistoryCaptured = false;

    private void CaptureHistorySnapshotForGesture()
    {
        if (_gestureHistoryCaptured) return;
        CaptureHistorySnapshot();
        _gestureHistoryCaptured = true;
    }

    private void EndGestureHistoryScope()
        => _gestureHistoryCaptured = false;

    private void UndoMidiEdit()
    {
        if (_file == null || !_history.Undo(_file)) return;
        ResetEditorAfterHistoryRestore();
    }

    private void RedoMidiEdit()
    {
        if (_file == null || !_history.Redo(_file)) return;
        ResetEditorAfterHistoryRestore();
    }

    private void ResetEditorAfterHistoryRestore()
    {
        SelectTrack(-1);
        _selectedTrackIndices.Clear();
        _selectedEventIndices.Clear();
        _globalTracksChecked = false;
        _globalEventsChecked = false;
        _editingEvent = null;
        _editingTrack = null;
        _editorDragMode = EditorDragMode.None;
        EndGestureHistoryScope();
        _preDragSnapshot.Clear();
        _noteHitList.Clear();
    }
}
