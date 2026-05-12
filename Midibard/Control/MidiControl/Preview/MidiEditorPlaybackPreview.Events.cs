using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;

namespace MidiBard.Control.MidiControl.Preview;

internal sealed unsafe partial class MidiEditorPlaybackPreview
{
    private bool SendPreviewEvent(MidiEvent midiEvent, object metadata)
    {
        try
        {
            if (metadata is not PreviewPlaybackMetadata previewMetadata)
                return true;

            lock (playbackLock)
            {
                var delayMs = GetCompensationDelayMs(midiEvent, previewMetadata, out var resolvedMetadata);
                if (delayMs <= 0)
                    ProcessEvent(midiEvent, resolvedMetadata);
                else
                    ScheduleCompensatedEvent(midiEvent.Clone(), resolvedMetadata, delayMs);
            }

            return true;
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e, "[MidiEditorPreview] Error processing preview playback event.");
            return false;
        }
    }

    internal void ProcessEventForTesting(MidiEvent midiEvent, int trackIndex, long time, double? timeSeconds = null)
    {
        lock (playbackLock)
            ProcessEvent(midiEvent, new PreviewPlaybackMetadata(trackIndex, time, timeSeconds ?? time / 1000.0, -1));
    }

    internal bool SendEventForTesting(MidiEvent midiEvent, int trackIndex, long time, double? timeSeconds = null)
        => SendPreviewEvent(midiEvent, new PreviewPlaybackMetadata(trackIndex, time, timeSeconds ?? time / 1000.0, -1));

    internal IReadOnlyList<TrackSnapshot> GetTrackSnapshots()
    {
        lock (playbackLock)
        {
            return trackPlaybackStates
                .Select((state, index) =>
                {
                    var current = state.CurrentNote;
                    return new TrackSnapshot(
                        index,
                        state.HeldNotes.Count,
                        current?.MidiNote,
                        current?.GameNote,
                        current?.InstrumentId,
                        state.CurrentSound);
                })
                .ToArray();
        }
    }

    internal void RefreshVisibilityForTesting()
    {
        lock (playbackLock)
            RefreshAllTrackPlayback(MidiEditorPreviewReleasePolicy.MaximumDynamicReleaseFadeMs);
    }

    private void ProcessEvent(MidiEvent midiEvent, PreviewPlaybackMetadata metadata)
    {
        switch (midiEvent)
        {
            case ProgramChangeEvent programChange:
                if ((uint)metadata.TrackIndex >= (uint)trackStates.Length)
                    return;
                ProcessProgramChange(metadata.TrackIndex, (byte)programChange.Channel, programChange.ProgramNumber);
                break;

            case NoteOffEvent noteOff:
                StopNote(metadata.TrackIndex, (byte)noteOff.Channel, (byte)noteOff.NoteNumber, metadata.TimeSeconds);
                break;

            case NoteOnEvent noteOn when (byte)noteOn.Velocity == 0:
                StopNote(metadata.TrackIndex, (byte)noteOn.Channel, (byte)noteOn.NoteNumber, metadata.TimeSeconds);
                break;

            case NoteOnEvent noteOn:
                PlayNote(
                    metadata.TrackIndex,
                    (byte)noteOn.Channel,
                    (byte)noteOn.NoteNumber,
                    metadata.Time,
                    metadata.TimeSeconds,
                    metadata.ResolvedGameNote,
                    metadata.ResolvedInstrumentId);
                break;
        }
    }

    private int GetCompensationDelayMs(MidiEvent midiEvent, PreviewPlaybackMetadata metadata, out PreviewPlaybackMetadata resolvedMetadata)
    {
        resolvedMetadata = metadata;
        if (!TryGetNoteEventInfo(midiEvent, out var channel, out var midiNote, out var isNoteOn))
            return 0;

        if ((uint)metadata.TrackIndex >= (uint)trackStates.Length)
            return 0;

        var trackState = trackStates[metadata.TrackIndex];
        var gameNote = TrackInfo.TranslateNoteNumber(
            midiNote + trackState.Transpose,
            settings.TransposeGlobal,
            settings.AdaptNotesOOR);

        if (gameNote is < 0 or > 36)
            return 0;

        var instrumentId = ResolveInstrumentForEvent(metadata.TrackIndex, trackState, channel);
        if (instrumentId is null or 0)
            return 0;

        resolvedMetadata = metadata with
        {
            ResolvedGameNote = gameNote,
            ResolvedInstrumentId = instrumentId.Value,
        };
        return compensationPolicy.GetDelayMs(
            instrumentId.Value,
            gameNote,
            metadata.TrackIndex,
            metadata.Time,
            isNoteOn);
    }

    private static bool TryGetNoteEventInfo(MidiEvent midiEvent, out int channel, out int midiNote, out bool isNoteOn)
    {
        channel = 0;
        midiNote = 0;
        isNoteOn = false;

        switch (midiEvent)
        {
            case NoteOffEvent noteOff:
                channel = (byte)noteOff.Channel;
                midiNote = (byte)noteOff.NoteNumber;
                return true;
            case NoteOnEvent noteOn:
                channel = (byte)noteOn.Channel;
                midiNote = (byte)noteOn.NoteNumber;
                isNoteOn = (byte)noteOn.Velocity > 0;
                return true;
            default:
                return false;
        }
    }

    private void ScheduleCompensatedEvent(MidiEvent midiEvent, PreviewPlaybackMetadata metadata, int delayMs)
    {
        var pending = new PendingPreviewSchedule();
        var scheduleVersion = compensatedEventScheduleVersion;
        var delayedMetadata = metadata with
        {
            TimeSeconds = metadata.TimeSeconds + delayMs / 1000.0,
        };
        pendingCompensatedEventSchedules.Add(pending);
        pending.Schedule = scheduler.Schedule(
            TimeSpan.FromMilliseconds(delayMs),
            () => ProcessScheduledCompensatedEvent(pending, midiEvent, delayedMetadata, scheduleVersion));
    }

    private void ProcessScheduledCompensatedEvent(
        PendingPreviewSchedule pending,
        MidiEvent midiEvent,
        PreviewPlaybackMetadata metadata,
        long scheduleVersion)
    {
        lock (playbackLock)
        {
            pendingCompensatedEventSchedules.Remove(pending);
            if (pending.Cancelled || scheduleVersion != compensatedEventScheduleVersion)
                return;

            try
            {
                ProcessEvent(midiEvent, metadata);
            }
            catch (Exception e)
            {
                DalamudApi.PluginLog.Error(e, "[MidiEditorPreview] Error processing compensated preview event.");
            }
        }
    }
}
