using System;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;

namespace MidiBard.Control.MidiControl.Editing;

internal static class MidiForgeTrackNameEditor
{
    public static readonly HashSet<string> PreservedDrumTrackNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "BassDrum",
        "SnareDrum",
        "Cymbal",
        "Bongo",
        "Timpani",
        "Drumkit",
    };

    public static string GetDefaultTrackName(EditableTrack track, MidiForgeTrackNameFillMode fillMode, int fallbackIndex)
        => MidiForgeTrackNaming.GetDefaultTrackName(track.Chunk, fallbackIndex, fillMode);

    public static bool SetEditableTrackName(EditableTrack track, string name)
    {
        if (string.Equals(track.Name, name, StringComparison.Ordinal))
            return false;

        track.Name = name;
        track.MarkNameDirty();
        return true;
    }

    public static void SetTrackName(TrackChunk chunk, string name)
    {
        chunk.Events.RemoveAll(e => e is SequenceTrackNameEvent);
        chunk.Events.Insert(0, new SequenceTrackNameEvent(name));
    }
}
