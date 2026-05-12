using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

internal static class MidiForgeTrackMutation
{
    public static int[] GetValidTrackIndices(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        bool descending = false)
    {
        var query = trackIndices
            .Where(index => index >= 0 && index < file.Tracks.Count && !file.Tracks[index].IsConductorTrack)
            .Distinct();

        return descending
            ? query.OrderByDescending(index => index).ToArray()
            : query.OrderBy(index => index).ToArray();
    }

    public static int[] GetValidComparisonTrackIndices(
        EditableMidiFile file,
        IEnumerable<int> trackIndices)
        => GetValidTrackIndices(file, trackIndices);

    public static int InsertDerivedTrackAfterTarget(
        EditableMidiFile file,
        int targetTrackIndex,
        TrackChunk sourceChunk,
        string trackName,
        IReadOnlyCollection<Note> notes)
    {
        if (notes.Count == 0)
            return 0;

        file.Tracks.Insert(targetTrackIndex + 1, new EditableTrack(
            MidiForgeNoteFactory.CreateTrackFromNotes(sourceChunk, trackName, notes),
            targetTrackIndex + 1));
        return 1;
    }

    public static void MoveTracksToEnd(EditableMidiFile file, IEnumerable<EditableTrack> tracks)
    {
        foreach (var track in tracks)
        {
            var index = file.Tracks.IndexOf(track);
            if (index < 0)
                continue;

            file.Tracks.RemoveAt(index);
            file.Tracks.Add(track);
        }
    }

    public static void RefreshTrackIndexesAndDirty(EditableMidiFile file)
    {
        for (int i = 0; i < file.Tracks.Count; i++)
            file.Tracks[i].Index = i;
        file.MarkChanged();
    }
}
