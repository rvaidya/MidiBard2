using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

internal static class MidiForgeRangeSplitter
{
    public static MidiForgeSplitNotesRangeResult SplitTracksByRange(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        Func<Note, bool> isInRange,
        Func<string, string> getInRangeTrackName,
        Func<string, string> getOutOfRangeTrackName)
    {
        var validTrackIndices = MidiForgeTrackMutation.GetValidTrackIndices(file, trackIndices, descending: true);

        var sourceTracks = 0;
        var createdTracks = 0;
        var inRangeTracks = 0;
        var outOfRangeTracks = 0;
        var inRangeNotesTotal = 0;
        var outOfRangeNotesTotal = 0;

        foreach (var trackIndex in validTrackIndices)
        {
            var track = file.Tracks[trackIndex];
            var sourceChunk = track.CloneCurrentChunk();
            var notes = sourceChunk.GetNotes().ToArray();
            if (notes.Length == 0)
                continue;

            var inRangeNotes = notes
                .Where(isInRange)
                .Select(note => MidiForgeNoteFactory.CloneWithLength(note, note.Length))
                .ToArray();
            var outOfRangeNotes = notes
                .Where(note => !isInRange(note))
                .Select(note => MidiForgeNoteFactory.CloneWithLength(note, note.Length))
                .ToArray();

            sourceTracks++;

            if (outOfRangeNotes.Length > 0)
            {
                file.Tracks.Insert(trackIndex + 1, new EditableTrack(
                    MidiForgeNoteFactory.CreateTrackFromNotes(sourceChunk, getOutOfRangeTrackName(track.DisplayName), outOfRangeNotes),
                    trackIndex + 1));
                createdTracks++;
                outOfRangeTracks++;
                outOfRangeNotesTotal += outOfRangeNotes.Length;
            }

            if (inRangeNotes.Length > 0)
            {
                file.Tracks.Insert(trackIndex + 1, new EditableTrack(
                    MidiForgeNoteFactory.CreateTrackFromNotes(sourceChunk, getInRangeTrackName(track.DisplayName), inRangeNotes),
                    trackIndex + 1));
                createdTracks++;
                inRangeTracks++;
                inRangeNotesTotal += inRangeNotes.Length;
            }
        }

        if (createdTracks > 0)
            MidiForgeTrackMutation.RefreshTrackIndexesAndDirty(file);

        return new MidiForgeSplitNotesRangeResult(
            sourceTracks,
            createdTracks,
            inRangeTracks,
            outOfRangeTracks,
            inRangeNotesTotal,
            outOfRangeNotesTotal);
    }
}
