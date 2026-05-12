using System;

namespace MidiBard.Control.MidiControl.Editing;

internal static class MidiForgeNoteNames
{
    public static string GetMidiNoteName(int noteNumber)
    {
        var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        var clampedNoteNumber = Math.Clamp(noteNumber, 0, 127);
        return $"{noteNames[clampedNoteNumber % 12]}{clampedNoteNumber / 12 - 1}";
    }
}
