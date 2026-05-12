using System;
using System.Linq;

namespace MidiBard.Control.MidiControl.Preview;

internal sealed unsafe partial class MidiEditorPlaybackPreview
{
    private nint StartSound(int trackIndex, HeldNote note)
    {
        var request = new PreviewSoundRequest(trackIndex, note.Channel, note.MidiNote, note.GameNote, note.InstrumentId);
        var sound = soundPlayer.Play(request, out var statusMessage);
        if (!string.IsNullOrWhiteSpace(statusMessage))
            StatusMessage = statusMessage;
        return sound;
    }

    private void StopNote(int trackIndex, int channel, int midiNote, double releaseSeconds)
    {
        if ((uint)trackIndex >= (uint)trackPlaybackStates.Length)
            return;

        var playbackState = trackPlaybackStates[trackIndex];
        playbackState.SameOnsetRollQueue.RemoveAll(note => note.Channel == channel && note.MidiNote == midiNote);
        var heldNoteIndex = playbackState.HeldNotes.FindIndex(note => note.Channel == channel && note.MidiNote == midiNote);
        if (heldNoteIndex < 0)
            return;

        playbackState.HeldNotes.RemoveAt(heldNoteIndex);
        RefreshTrackPlaybackForMusicalChange(trackIndex, releaseSeconds);
    }

    private void ReleaseCurrentTrackSound(TrackPlaybackState playbackState, double releaseSeconds)
    {
        var currentNote = playbackState.CurrentNote;
        var currentSound = playbackState.CurrentSound;
        playbackState.CurrentSound = 0;
        playbackState.CurrentNote = null;

        if (currentSound == 0 || !currentNote.HasValue)
            return;

        if (!releasePolicy.ShouldStopOnMusicalRelease(currentNote.Value.InstrumentId))
        {
            RetainNaturalOneShotSound(playbackState, currentSound, currentNote.Value.InstrumentId);
            return;
        }

        var heldSeconds = releaseSeconds - currentNote.Value.OnsetSeconds;
        var fadeOutDuration = releasePolicy.GetMusicalReleaseFadeMs(currentNote.Value.InstrumentId, heldSeconds);
        soundPlayer.Stop(currentSound, fadeOutDuration);
    }

    private void RetainNaturalOneShotSound(TrackPlaybackState playbackState, nint sound, uint instrumentId)
    {
        var retainedSound = new RetainedPreviewSound(sound);
        playbackState.SoundsForCleanup.Add(retainedSound);
        retainedSound.CleanupSchedule = scheduler.Schedule(
            TimeSpan.FromMilliseconds(releasePolicy.GetNaturalOneShotCleanupDelayMs(instrumentId)),
            () => CleanupRetainedSound(playbackState, retainedSound));
    }

    private void CleanupRetainedSound(TrackPlaybackState playbackState, RetainedPreviewSound retainedSound)
    {
        lock (playbackLock)
        {
            if (!playbackState.SoundsForCleanup.Remove(retainedSound))
                return;

            StopRetainedSound(retainedSound, MidiEditorPreviewReleasePolicy.CleanupFadeMs);
        }
    }

    private void StopCurrentTrackSound(TrackPlaybackState playbackState, uint fadeOutDuration)
    {
        if (playbackState.CurrentSound != 0)
            soundPlayer.Stop(playbackState.CurrentSound, fadeOutDuration);

        playbackState.CurrentSound = 0;
        playbackState.CurrentNote = null;
    }

    private void StopAllTrackSounds(TrackPlaybackState playbackState, uint fadeOutDuration)
    {
        StopCurrentTrackSound(playbackState, fadeOutDuration);
        foreach (var sound in playbackState.SoundsForCleanup.ToArray())
            StopRetainedSound(sound, fadeOutDuration);

        playbackState.SoundsForCleanup.Clear();
    }

    private void StopRetainedSound(RetainedPreviewSound retainedSound, uint fadeOutDuration)
    {
        retainedSound.CleanupSchedule?.Dispose();
        retainedSound.CleanupSchedule = null;
        if (retainedSound.Stopped || retainedSound.Sound == 0)
            return;

        soundPlayer.Stop(retainedSound.Sound, fadeOutDuration);
        retainedSound.Stopped = true;
    }

    private void StopAllSounds()
    {
        lock (playbackLock)
            StopAllSoundsLocked(MidiEditorPreviewReleasePolicy.CleanupFadeMs);
    }

    private void StopAllSoundsLocked(uint fadeOutDuration)
    {
        CancelPendingCompensatedEventsLocked();

        foreach (var playbackState in trackPlaybackStates)
        {
            CancelSameOnsetRoll(playbackState, pruneLowerNotes: false);
            StopAllTrackSounds(playbackState, fadeOutDuration);
            playbackState.HeldNotes.Clear();
        }
    }

    private void CancelPendingCompensatedEventsLocked()
    {
        compensatedEventScheduleVersion++;
        compensationPolicy.Reset();
        foreach (var pending in pendingCompensatedEventSchedules)
            pending.Dispose();

        pendingCompensatedEventSchedules.Clear();
    }

    private void PlaybackFinished(object? sender, EventArgs e)
    {
        lock (playbackLock)
        {
            StopAllSoundsLocked(MidiEditorPreviewReleasePolicy.CleanupFadeMs);
            ResetProgramStatesLocked();
        }
    }
}
