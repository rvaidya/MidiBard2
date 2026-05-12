using System.Linq;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

internal static class MidiForgePlayableRange
{
    public static int AdaptMidiNoteToPlayableRange(int midiNote)
        => TrackInfo.TranslateNoteNumber(midiNote, adaptOOR: true) + MidiForgeAnalysis.PlayableLowestMidiNote;

    public static int AdaptChunkNoteNumbers(TrackChunk chunk, int octaveShift)
    {
        var changedNotes = chunk.GetNotes()
            .Count(note => AdaptMidiNoteToPlayableRange((byte)note.NoteNumber + octaveShift) != (byte)note.NoteNumber);

        foreach (var midiEvent in chunk.Events)
        {
            switch (midiEvent)
            {
                case NoteOnEvent noteOn:
                    noteOn.NoteNumber = (SevenBitNumber)(byte)AdaptMidiNoteToPlayableRange((byte)noteOn.NoteNumber + octaveShift);
                    break;
                case NoteOffEvent noteOff:
                    noteOff.NoteNumber = (SevenBitNumber)(byte)AdaptMidiNoteToPlayableRange((byte)noteOff.NoteNumber + octaveShift);
                    break;
            }
        }

        return changedNotes;
    }
}
