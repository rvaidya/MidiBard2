using System;
using System.Linq;

namespace MidiBard.Control.MidiControl.Preview;

internal sealed unsafe partial class MidiEditorPlaybackPreview
{
    private void PlayNote(
        int trackIndex,
        int channel,
        int midiNote,
        long onsetTick,
        double onsetSeconds,
        int? resolvedGameNote,
        uint? resolvedInstrumentId)
    {
        var trackIsVisible = IsTrackVisible(trackIndex);
        if (!TryCreateHeldNote(
                trackIndex,
                channel,
                midiNote,
                onsetTick,
                onsetSeconds,
                trackIsVisible,
                resolvedGameNote,
                resolvedInstrumentId,
                out var heldNote))
            return;

        var playbackState = trackPlaybackStates[trackIndex];
        if (playbackState.SameOnsetRollTick is { } rollTick && rollTick != heldNote.OnsetTick)
            CancelSameOnsetRoll(playbackState, pruneLowerNotes: true);

        playbackState.HeldNotes.Add(heldNote);

        if (!trackIsVisible)
        {
            CancelSameOnsetRoll(playbackState, pruneLowerNotes: true);
            RemoveSameOnsetLowerNotes(playbackState, heldNote.OnsetTick);
            StopAllTrackSounds(playbackState, MidiEditorPreviewReleasePolicy.MaximumDynamicReleaseFadeMs);
            return;
        }

        if (TryQueueSameOnsetRoll(trackIndex, playbackState, heldNote))
            return;

        RefreshTrackPlaybackForMusicalChange(trackIndex, onsetSeconds);
    }

    private bool TryCreateHeldNote(
        int trackIndex,
        int channel,
        int midiNote,
        long onsetTick,
        double onsetSeconds,
        bool trackIsVisible,
        int? resolvedGameNote,
        uint? resolvedInstrumentId,
        out HeldNote heldNote)
    {
        heldNote = default;

        if ((uint)trackIndex >= (uint)trackStates.Length || (uint)trackIndex >= (uint)trackPlaybackStates.Length)
            return false;

        var trackState = trackStates[trackIndex];
        var translated = resolvedGameNote ?? TrackInfo.TranslateNoteNumber(
            midiNote + trackState.Transpose,
            settings.TransposeGlobal,
            settings.AdaptNotesOOR);

        if (translated is < 0 or > 36)
            return false;

        var instrumentId = resolvedInstrumentId ?? ResolveInstrumentForEvent(trackIndex, trackState, channel);
        if (instrumentId == null || instrumentId == 0)
        {
            if (trackIsVisible)
                StatusMessage = "Preview skipped a note because no instrument could be resolved.";
            return false;
        }

        heldNote = new HeldNote(channel, midiNote, translated, instrumentId.Value, onsetTick, onsetSeconds, nextNoteSequence++);
        return true;
    }

    private static void RemoveSameOnsetLowerNotes(TrackPlaybackState playbackState, long onsetTick)
    {
        var highestMidiNote = int.MinValue;
        var sameOnsetCount = 0;
        foreach (var note in playbackState.HeldNotes)
        {
            if (note.OnsetTick != onsetTick)
                continue;

            sameOnsetCount++;
            if (note.MidiNote > highestMidiNote)
                highestMidiNote = note.MidiNote;
        }

        if (sameOnsetCount < 2)
            return;

        playbackState.HeldNotes.RemoveAll(note =>
            note.OnsetTick == onsetTick &&
            note.MidiNote < highestMidiNote);
    }

    private bool TryQueueSameOnsetRoll(int trackIndex, TrackPlaybackState playbackState, HeldNote heldNote)
    {
        var currentNote = playbackState.CurrentNote;
        if (!currentNote.HasValue || currentNote.Value.OnsetTick != heldNote.OnsetTick)
            return false;

        if (heldNote.MidiNote <= currentNote.Value.MidiNote)
        {
            playbackState.HeldNotes.RemoveAll(note => note.Sequence == heldNote.Sequence);
            return true;
        }

        if (playbackState.SameOnsetRollTick != heldNote.OnsetTick)
        {
            CancelSameOnsetRoll(playbackState, pruneLowerNotes: false);
            playbackState.SameOnsetRollTick = heldNote.OnsetTick;
            playbackState.SameOnsetRollElapsedSeconds = 0.0;
            playbackState.SameOnsetRollVersion++;
        }

        AddSameOnsetRollNote(playbackState, heldNote);
        ScheduleSameOnsetRoll(trackIndex, playbackState);
        return true;
    }

    private static void AddSameOnsetRollNote(TrackPlaybackState playbackState, HeldNote heldNote)
    {
        if (playbackState.SameOnsetRollQueue.Any(note => note.Sequence == heldNote.Sequence))
            return;

        playbackState.SameOnsetRollQueue.Add(heldNote);
        playbackState.SameOnsetRollQueue.Sort((a, b) =>
        {
            var noteCompare = a.MidiNote.CompareTo(b.MidiNote);
            return noteCompare != 0 ? noteCompare : a.Sequence.CompareTo(b.Sequence);
        });
    }

    private void ScheduleSameOnsetRoll(int trackIndex, TrackPlaybackState playbackState)
    {
        if (playbackState.SameOnsetRollSchedule != null)
            return;

        var version = playbackState.SameOnsetRollVersion;
        playbackState.SameOnsetRollSchedule = scheduler.Schedule(
            SameOnsetRollStep,
            () => AdvanceSameOnsetRoll(trackIndex, version));
    }

    private void AdvanceSameOnsetRoll(int trackIndex, long version)
    {
        lock (playbackLock)
        {
            if ((uint)trackIndex >= (uint)trackPlaybackStates.Length)
                return;

            var playbackState = trackPlaybackStates[trackIndex];
            if (playbackState.SameOnsetRollVersion != version)
                return;

            playbackState.SameOnsetRollSchedule = null;

            if (playbackState.SameOnsetRollTick is not { } rollTick)
                return;

            if (!IsTrackVisible(trackIndex))
            {
                CancelSameOnsetRoll(playbackState, pruneLowerNotes: true);
                StopAllTrackSounds(playbackState, MidiEditorPreviewReleasePolicy.MaximumDynamicReleaseFadeMs);
                return;
            }

            var currentNote = playbackState.CurrentNote;
            if (!currentNote.HasValue || currentNote.Value.OnsetTick != rollTick)
            {
                CancelSameOnsetRoll(playbackState, pruneLowerNotes: true);
                return;
            }

            PruneSameOnsetRollQueue(playbackState, currentNote.Value);
            if (playbackState.SameOnsetRollQueue.Count == 0)
            {
                CompleteSameOnsetRoll(playbackState);
                return;
            }

            var nextNote = playbackState.SameOnsetRollQueue[0];
            playbackState.SameOnsetRollQueue.RemoveAt(0);
            playbackState.SameOnsetRollElapsedSeconds += SameOnsetRollStep.TotalSeconds;
            var releaseSeconds = currentNote.Value.OnsetSeconds + playbackState.SameOnsetRollElapsedSeconds;
            ReleaseCurrentTrackSound(playbackState, releaseSeconds);
            StartTrackSound(trackIndex, playbackState, nextNote);

            PruneSameOnsetRollQueue(playbackState, nextNote);
            if (playbackState.SameOnsetRollQueue.Count == 0)
                CompleteSameOnsetRoll(playbackState);
            else
                ScheduleSameOnsetRoll(trackIndex, playbackState);
        }
    }

    private static void PruneSameOnsetRollQueue(TrackPlaybackState playbackState, HeldNote currentNote)
    {
        playbackState.SameOnsetRollQueue.RemoveAll(note =>
            note.OnsetTick != currentNote.OnsetTick ||
            note.MidiNote <= currentNote.MidiNote ||
            !playbackState.HeldNotes.Any(held => held.Sequence == note.Sequence));
    }

    private static void CompleteSameOnsetRoll(TrackPlaybackState playbackState)
    {
        var rollTick = playbackState.SameOnsetRollTick;
        CancelSameOnsetRoll(playbackState, pruneLowerNotes: false);
        if (rollTick.HasValue)
            RemoveSameOnsetLowerNotes(playbackState, rollTick.Value);
    }

    private static void CancelSameOnsetRoll(TrackPlaybackState playbackState, bool pruneLowerNotes)
    {
        var rollTick = playbackState.SameOnsetRollTick;
        playbackState.SameOnsetRollVersion++;
        playbackState.SameOnsetRollSchedule?.Dispose();
        playbackState.SameOnsetRollSchedule = null;
        playbackState.SameOnsetRollQueue.Clear();
        playbackState.SameOnsetRollTick = null;
        playbackState.SameOnsetRollElapsedSeconds = 0.0;

        if (pruneLowerNotes && rollTick.HasValue)
            RemoveSameOnsetLowerNotes(playbackState, rollTick.Value);
    }
}
