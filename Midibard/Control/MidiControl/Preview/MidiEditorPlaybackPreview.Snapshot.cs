using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using MidiBard.Util.MidiPreprocessor;

namespace MidiBard.Control.MidiControl.Preview;

internal sealed unsafe partial class MidiEditorPlaybackPreview
{
    private void BuildTrackStates(EditableMidiFile file)
    {
        trackStates = new TrackPreviewState[file.Tracks.Count];
        trackPlaybackStates = new TrackPlaybackState[file.Tracks.Count];
        for (var i = 0; i < file.Tracks.Count; i++)
        {
            var track = file.Tracks[i];
            var baseInstrumentId = instrumentCatalog.ResolveTrackInstrument(
                track.Name,
                settings.DefaultInstrumentId,
                settings.ForceDefaultInstrument);
            trackPlaybackStates[i] = new TrackPlaybackState();
            trackStates[i] = new TrackPreviewState
            {
                TrackName = track.Name,
                Transpose = TrackInfo.GetTransposeByName(track.Name),
                BaseInstrumentId = baseInstrumentId,
                IsProgramElectricGuitar = TrackInfo.IsProgramElectricGuitarTrackName(track.Name),
            };
        }
    }

    private List<PreviewTimedEvent> BuildPlaybackEvents(EditableMidiFile file, out TempoMap tempoMap)
    {
        var chunks = new TrackChunk[file.Tracks.Count];
        for (var trackIndex = 0; trackIndex < file.Tracks.Count; trackIndex++)
        {
            chunks[trackIndex] = BuildPlaybackTrackChunk(file.Tracks[trackIndex]);
            MidiPreprocessor.FixNoteOffChannels(chunks[trackIndex]);
        }

        var snapshot = new MidiFile(chunks)
        {
            TimeDivision = file.Source.TimeDivision,
        };

        if (settings.AntiStackType != AntiStackType.Off)
            MidiPreprocessor.RemoveStackedNotes(snapshot, settings.AntiStackType);

        tempoMap = snapshot.GetTempoMap();
        var playbackEvents = new List<PreviewTimedEvent>();
        programEvents.Clear();
        eventSnapshots.Clear();

        for (var trackIndex = 0; trackIndex < file.Tracks.Count; trackIndex++)
        {
            if (file.Tracks[trackIndex].IsConductorTrack)
                continue;

            foreach (var timedEvent in chunks[trackIndex].GetTimedEvents())
            {
                if (!TryCreatePlaybackEvent(trackIndex, timedEvent, tempoMap, out var playbackEvent))
                    continue;

                playbackEvents.Add(playbackEvent);
                eventSnapshots.Add(CreateEventSnapshot(playbackEvent));
            }
        }

        programEvents.Sort((a, b) =>
        {
            var timeCompare = a.TimeSeconds.CompareTo(b.TimeSeconds);
            return timeCompare != 0 ? timeCompare : a.TrackIndex.CompareTo(b.TrackIndex);
        });

        return playbackEvents
            .OrderBy(ev => ev.Time)
            .ThenBy(ev => ((PreviewPlaybackMetadata)ev.Metadata).EventValue)
            .ToList();
    }

    private static TrackChunk BuildPlaybackTrackChunk(EditableTrack track)
    {
        if (track.Events == null)
            return new TrackChunk(track.Chunk.Events.Select(ev => ev.Clone()));

        var chunk = new TrackChunk();
        using var manager = chunk.ManageTimedEvents();
        foreach (var timedEvent in EnumerateLiveTimedEvents(track))
            manager.Objects.Add(CloneTimedEvent(timedEvent));

        return chunk;
    }

    private static IEnumerable<TimedEvent> EnumerateLiveTimedEvents(EditableTrack track)
    {
        foreach (var editableEvent in track.Events)
        {
            yield return editableEvent.Source;
            if (editableEvent.NoteOffSource != null)
                yield return editableEvent.NoteOffSource;
        }
    }

    private static TimedEvent CloneTimedEvent(TimedEvent timedEvent)
        => new(timedEvent.Event.Clone(), timedEvent.Time);

    private bool TryCreatePlaybackEvent(int trackIndex, TimedEvent timedEvent, TempoMap tempoMap, out PreviewTimedEvent playbackEvent)
    {
        playbackEvent = null;
        if (!TryGetEventInfo(timedEvent.Event, out var channel, out var eventValue))
            return false;

        var seconds = ToSeconds(TimeConverter.ConvertTo<MetricTimeSpan>(timedEvent.Time, tempoMap));
        if (timedEvent.Event is ProgramChangeEvent programChange)
            programEvents.Add(new PreviewProgramEvent(seconds, trackIndex, channel, programChange.ProgramNumber));

        playbackEvent = new PreviewTimedEvent(
            timedEvent.Event,
            timedEvent.Time,
            new PreviewPlaybackMetadata(trackIndex, timedEvent.Time, seconds, eventValue));
        return true;
    }

    private static bool TryGetEventInfo(MidiEvent midiEvent, out int channel, out int eventValue)
    {
        channel = 0;
        eventValue = -1;

        switch (midiEvent)
        {
            case ProgramChangeEvent programChange:
                channel = (byte)programChange.Channel;
                eventValue = -2;
                return true;
            case NoteOffEvent noteOff:
                channel = (byte)noteOff.Channel;
                eventValue = (byte)noteOff.NoteNumber;
                return true;
            case NoteOnEvent noteOn:
                channel = (byte)noteOn.Channel;
                eventValue = (byte)noteOn.NoteNumber;
                return true;
            default:
                return false;
        }
    }

    private static EventSnapshot CreateEventSnapshot(PreviewTimedEvent playbackEvent)
    {
        var metadata = (PreviewPlaybackMetadata)playbackEvent.Metadata;
        TryGetEventInfo(playbackEvent.Event, out var channel, out var eventValue);
        var programNumber = playbackEvent.Event is ProgramChangeEvent programChange
            ? (int)(byte)programChange.ProgramNumber
            : (int?)null;

        return new EventSnapshot(
            metadata.TrackIndex,
            playbackEvent.Time,
            playbackEvent.Event.EventType.ToString(),
            channel,
            eventValue,
            programNumber);
    }

    private Playback CreatePlayback(List<PreviewTimedEvent> playbackEvents, TempoMap tempoMap)
    {
        var playbackSettings = new PlaybackSettings
        {
            ClockSettings = new MidiClockSettings
            {
                CreateTickGeneratorCallback = () => new HighPrecisionTickGenerator()
            },
        };

        var result = new InternalPlayback(playbackEvents, tempoMap, playbackSettings, SendPreviewEvent)
        {
            InterruptNotesOnStop = true,
            TrackNotes = true,
            TrackProgram = true,
            Speed = Math.Max(0.1, settings.PlaySpeed),
            SendNoteOnEventsForActiveNotes = true,
            SendNoteOffEventsForNonActiveNotes = true,
        };

        result.Finished += PlaybackFinished;
        return result;
    }
}
