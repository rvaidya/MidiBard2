using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

internal static class MidiForgeNoteTiming
{
    public static bool IsEqualNoteAtStart(Note note, Note other)
        => note.Time == other.Time && (byte)note.NoteNumber == (byte)other.NoteNumber;

    public static bool NotesOverlap(Note note, Note other)
    {
        var noteStart = note.Time;
        var noteEnd = note.Time + note.Length;
        var otherStart = other.Time;
        var otherEnd = other.Time + other.Length;

        return otherEnd > noteStart && otherStart < noteEnd;
    }

    public static long LimitDurationToCurrentMeasureWhenNextMeasureIsEmpty(
        Note note,
        IReadOnlyCollection<Note> trackNotes,
        long newLength,
        long barDurationTicks)
    {
        if (barDurationTicks <= 0)
            return newLength;

        var noteMeasureIndex = note.Time / barDurationTicks;
        var currentMeasureEnd = (noteMeasureIndex + 1) * barDurationTicks;
        var nextMeasureEnd = currentMeasureEnd + barDurationTicks;
        if (note.Time + newLength <= currentMeasureEnd)
            return newLength;

        var nextMeasureHasNotes = trackNotes.Any(other =>
            other.Time >= currentMeasureEnd && other.Time < nextMeasureEnd);
        if (nextMeasureHasNotes)
            return newLength;

        return Math.Max(1, currentMeasureEnd - note.Time);
    }

    public static long GetBarDurationTicks(EditableMidiFile file)
    {
        var ticksPerQuarter = file.Source.TimeDivision is TicksPerQuarterNoteTimeDivision timeDivision
            ? timeDivision.TicksPerQuarterNote
            : 480;

        return ticksPerQuarter * 4L;
    }
}
