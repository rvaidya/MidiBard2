using System;

namespace MidiBard.Control.MidiControl.Preview;

internal sealed unsafe partial class MidiEditorPlaybackPreview
{
    private void RefreshTrackPlayback(int trackIndex, uint fadeOutDuration)
    {
        if ((uint)trackIndex >= (uint)trackPlaybackStates.Length)
            return;

        var playbackState = trackPlaybackStates[trackIndex];
        if (!IsTrackVisible(trackIndex))
        {
            // Visibility is a live mute, not a MIDI NoteOff: keep HeldNotes intact.
            CancelSameOnsetRoll(playbackState, pruneLowerNotes: true);
            StopAllTrackSounds(playbackState, fadeOutDuration);
            return;
        }

        if (playbackState.SameOnsetRollTick.HasValue &&
            playbackState.CurrentNote?.OnsetTick == playbackState.SameOnsetRollTick.Value &&
            playbackState.SameOnsetRollQueue.Count > 0)
        {
            return;
        }

        var winningNote = GetWinningNote(playbackState);

        if (playbackState.CurrentNote.HasValue && winningNote.HasValue &&
            IsSameSoundingNote(playbackState.CurrentNote.Value, winningNote.Value))
        {
            playbackState.CurrentNote = winningNote;
            return;
        }

        StopCurrentTrackSound(playbackState, fadeOutDuration);

        if (!winningNote.HasValue)
            return;

        StartTrackSound(trackIndex, playbackState, winningNote.Value);
    }

    private void RefreshTrackPlaybackForMusicalChange(int trackIndex, double releaseSeconds)
    {
        if ((uint)trackIndex >= (uint)trackPlaybackStates.Length)
            return;

        var playbackState = trackPlaybackStates[trackIndex];
        if (!IsTrackVisible(trackIndex))
        {
            CancelSameOnsetRoll(playbackState, pruneLowerNotes: true);
            StopAllTrackSounds(playbackState, MidiEditorPreviewReleasePolicy.MaximumDynamicReleaseFadeMs);
            return;
        }

        if (playbackState.SameOnsetRollTick.HasValue &&
            playbackState.CurrentNote is { } currentRollNote &&
            currentRollNote.OnsetTick == playbackState.SameOnsetRollTick.Value &&
            playbackState.SameOnsetRollQueue.Count > 0)
        {
            if (playbackState.HeldNotes.Exists(note => note.Sequence == currentRollNote.Sequence))
                return;

            CancelSameOnsetRoll(playbackState, pruneLowerNotes: true);
        }

        var winningNote = GetWinningNote(playbackState);

        if (playbackState.CurrentNote.HasValue && winningNote.HasValue &&
            IsSameSoundingNote(playbackState.CurrentNote.Value, winningNote.Value))
        {
            playbackState.CurrentNote = winningNote;
            return;
        }

        ReleaseCurrentTrackSound(playbackState, releaseSeconds);

        if (!winningNote.HasValue)
            return;

        StartTrackSound(trackIndex, playbackState, winningNote.Value);
    }

    private void StartTrackSound(int trackIndex, TrackPlaybackState playbackState, HeldNote note)
    {
        LogDuplicateInstrumentPreviewIfNeeded(trackIndex, note);
        var sound = StartSound(trackIndex, note);
        if (sound == 0)
            return;

        playbackState.CurrentNote = note;
        playbackState.CurrentSound = sound;
    }

    private static HeldNote? GetWinningNote(TrackPlaybackState playbackState)
    {
        if (playbackState.HeldNotes.Count == 0)
            return null;

        var winningNote = playbackState.HeldNotes[0];
        for (var i = 1; i < playbackState.HeldNotes.Count; i++)
        {
            var note = playbackState.HeldNotes[i];
            if (note.OnsetTick > winningNote.OnsetTick ||
                (note.OnsetTick == winningNote.OnsetTick && note.MidiNote > winningNote.MidiNote) ||
                (note.OnsetTick == winningNote.OnsetTick && note.MidiNote == winningNote.MidiNote && note.Sequence < winningNote.Sequence))
            {
                winningNote = note;
            }
        }

        return winningNote;
    }

    private void RefreshAllTrackPlayback(uint fadeOutDuration)
    {
        for (var trackIndex = 0; trackIndex < trackPlaybackStates.Length; trackIndex++)
            RefreshTrackPlayback(trackIndex, fadeOutDuration);
    }

    private bool IsTrackVisible(int trackIndex)
    {
        try
        {
            return trackVisibilityProvider(trackIndex);
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Verbose(e, "[MidiEditorPreview] Failed to read preview track visibility.");
            return false;
        }
    }

    private static bool IsSameSoundingNote(HeldNote a, HeldNote b)
        => a.GameNote == b.GameNote && a.InstrumentId == b.InstrumentId && a.OnsetTick == b.OnsetTick;
}
