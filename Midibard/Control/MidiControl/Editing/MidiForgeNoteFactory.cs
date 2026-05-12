using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

internal static class MidiForgeNoteFactory
{
    public static Note CloneWithNumber(Note note, int noteNumber)
        => new(
            (SevenBitNumber)(byte)Math.Clamp(noteNumber, 0, 127),
            note.Length,
            note.Time)
        {
            Channel = note.Channel,
            Velocity = note.Velocity,
            OffVelocity = note.OffVelocity,
        };

    public static Note CloneWithLength(Note note, long length)
        => new(
            note.NoteNumber,
            Math.Max(0, length),
            note.Time)
        {
            Channel = note.Channel,
            Velocity = note.Velocity,
            OffVelocity = note.OffVelocity,
        };

    public static TrackChunk CreateTrackFromNotes(
        TrackChunk sourceChunk,
        string trackName,
        IEnumerable<Note> notes,
        bool includePitchBendEvents = true)
    {
        var chunk = new TrackChunk();
        using var manager = chunk.ManageTimedEvents();

        manager.Objects.Add(new TimedEvent(new SequenceTrackNameEvent(trackName), 0));

        foreach (var timedEvent in sourceChunk.GetTimedEvents()
            .Where(te => te.Event is not NoteOnEvent and not NoteOffEvent and not SequenceTrackNameEvent))
        {
            if (!includePitchBendEvents && timedEvent.Event is PitchBendEvent)
                continue;

            manager.Objects.Add(new TimedEvent(timedEvent.Event.Clone(), timedEvent.Time));
        }

        foreach (var note in notes.OrderBy(note => note.Time).ThenBy(note => (byte)note.NoteNumber))
        {
            manager.Objects.Add(new TimedEvent(
                new NoteOnEvent(note.NoteNumber, note.Velocity) { Channel = note.Channel },
                note.Time));
            manager.Objects.Add(new TimedEvent(
                new NoteOffEvent(note.NoteNumber, note.OffVelocity) { Channel = note.Channel },
                note.EndTime));
        }

        return chunk;
    }
}
