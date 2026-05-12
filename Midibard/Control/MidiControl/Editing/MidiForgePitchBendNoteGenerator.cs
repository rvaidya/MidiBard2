using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

internal static class MidiForgePitchBendNoteGenerator
{
    public static IEnumerable<Note> GenerateForNote(
        Note note,
        IReadOnlyList<TimedEvent> pitchBends)
    {
        var noteStartTick = note.Time;
        var noteEndTick = note.EndTime;
        var notePitchBends = pitchBends
            .Where(timedEvent => timedEvent.Event is PitchBendEvent bend && bend.Channel == note.Channel)
            .OrderBy(timedEvent => timedEvent.Time)
            .ToArray();

        if (notePitchBends.Length == 0)
            return [MidiForgeNoteFactory.CloneWithLength(note, note.Length)];

        var firstNotePitchBendEvent = notePitchBends.LastOrDefault(timedEvent => timedEvent.Time <= noteStartTick);
        var pitchBendsDuringNote = notePitchBends
            .Where(timedEvent =>
                (timedEvent.Time > noteStartTick && timedEvent.Time <= noteEndTick) ||
                ReferenceEquals(timedEvent, firstNotePitchBendEvent))
            .OrderBy(timedEvent => timedEvent.Time)
            .ToArray();

        var uniquePitchBends = new List<TimedEvent>();
        foreach (var pitchBend in pitchBendsDuringNote)
        {
            if (uniquePitchBends.Count == 0 ||
                GetPitchBendSemitones((PitchBendEvent)pitchBend.Event) !=
                GetPitchBendSemitones((PitchBendEvent)uniquePitchBends[^1].Event))
            {
                uniquePitchBends.Add(pitchBend);
            }
        }

        if (uniquePitchBends.Count == 0)
            return [MidiForgeNoteFactory.CloneWithLength(note, note.Length)];

        var generatedNotes = new List<Note>();
        for (int i = 0; i < uniquePitchBends.Count; i++)
        {
            var pitchBend = uniquePitchBends[i];
            if (i == 0 && pitchBend.Time > noteStartTick)
                AddGeneratedNote(generatedNotes, note, (byte)note.NoteNumber, noteStartTick, pitchBend.Time);

            var semitones = GetPitchBendSemitones((PitchBendEvent)pitchBend.Event);
            var noteNumber = System.Math.Clamp((byte)note.NoteNumber + semitones, 0, 127);
            var segmentStartTick = i == 0 && pitchBend.Time <= noteStartTick
                ? noteStartTick
                : pitchBend.Time;
            var segmentEndTick = i == uniquePitchBends.Count - 1
                ? noteEndTick
                : uniquePitchBends[i + 1].Time;

            AddGeneratedNote(generatedNotes, note, noteNumber, segmentStartTick, segmentEndTick);
        }

        return generatedNotes;
    }

    public static int GetPitchBendSemitones(PitchBendEvent pitchBend)
        => pitchBend.PitchValue switch
        {
            < 4096 => -2,
            < 8192 => -1,
            < 12288 => 0,
            < 16383 => 1,
            _ => 2,
        };

    private static void AddGeneratedNote(
        ICollection<Note> notes,
        Note sourceNote,
        int noteNumber,
        long startTick,
        long endTick)
    {
        var length = endTick - startTick;
        if (length <= 0) return;

        var note = MidiForgeNoteFactory.CloneWithNumber(sourceNote, noteNumber);
        note.Time = System.Math.Max(0, startTick);
        note.Length = length;
        notes.Add(note);
    }
}
