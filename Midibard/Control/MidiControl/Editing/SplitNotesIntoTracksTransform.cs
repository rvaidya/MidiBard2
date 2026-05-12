using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class SplitNotesIntoTracksTransform : IMidiEditorTransform<MidiForgeSplitNotesIntoTracksOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.split-notes-into-tracks", "Split Notes Into Tracks");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeSplitNotesIntoTracksOptions options)
    {
        if (options.NumberOfTracks <= 0)
            return MidiEditorTransformValidation.Failure("Number of tracks must be greater than zero.");
        if (options.EveryNotesAmount <= 0)
            return MidiEditorTransformValidation.Failure("Every N notes must be greater than zero.");

        return MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);
    }

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeSplitNotesIntoTracksOptions options)
    {
        var result = Apply(context.File, context.SelectedTrackIndices, options);

        return MidiForgeNoteTransformResult.CreatedTrackResult(
            result.CreatedTracks,
            $"created {result.CreatedTracks} distributed-note track(s)");
    }

    public static MidiForgeSplitNotesIntoTracksResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeSplitNotesIntoTracksOptions options)
    {
        var validTrackIndices = trackIndices
            .Where(index => index >= 0 && index < file.Tracks.Count && !file.Tracks[index].IsConductorTrack)
            .Distinct()
            .OrderByDescending(index => index)
            .ToArray();
        var numberOfTracks = Math.Clamp(options.NumberOfTracks, 1, 64);
        var everyNotesAmount = Math.Max(1, options.EveryNotesAmount);
        var sourceTracks = 0;
        var createdTracks = 0;
        var distributedNotes = 0;

        foreach (var trackIndex in validTrackIndices)
        {
            var track = file.Tracks[trackIndex];
            var sourceChunk = track.CloneCurrentChunk();
            var notes = sourceChunk.GetNotes()
                .OrderBy(note => note.Time)
                .ThenBy(note => (byte)note.NoteNumber)
                .ToArray();
            if (notes.Length == 0)
                continue;

            var splitNotes = Enumerable.Range(0, numberOfTracks)
                .Select(_ => new List<Note>())
                .ToArray();
            var destinationTrackIndex = 0;
            var noteCountInDestination = 0;

            foreach (var note in notes)
            {
                if (destinationTrackIndex >= numberOfTracks)
                    destinationTrackIndex = 0;

                splitNotes[destinationTrackIndex].Add(MidiForgeNoteFactory.CloneWithLength(note, note.Length));
                noteCountInDestination++;

                if (noteCountInDestination == everyNotesAmount)
                    noteCountInDestination = 0;

                if (noteCountInDestination == 0)
                    destinationTrackIndex++;
            }

            var newTracks = splitNotes
                .Select((notesGroup, index) => (notesGroup, index))
                .Where(group => group.notesGroup.Count > 0)
                .Select(group => new EditableTrack(
                    MidiForgeNoteFactory.CreateTrackFromNotes(sourceChunk, $"{track.DisplayName} (Group {group.index + 1})", group.notesGroup),
                    0))
                .ToList();

            if (newTracks.Count == 0)
                continue;

            file.Tracks.InsertRange(trackIndex + 1, newTracks);
            sourceTracks++;
            createdTracks += newTracks.Count;
            distributedNotes += notes.Length;
        }

        if (createdTracks > 0)
            MidiForgeTrackMutation.RefreshTrackIndexesAndDirty(file);

        return new MidiForgeSplitNotesIntoTracksResult(
            sourceTracks,
            createdTracks,
            distributedNotes);
    }
}
