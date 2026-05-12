using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class SplitChordsTransform : IMidiEditorTransform<MidiForgeSplitChordsOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.split-chords", "Split Chords");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeSplitChordsOptions options)
    {
        if (options.MinimumSimultaneousNotes < 2)
            return MidiEditorTransformValidation.Failure("Minimum simultaneous notes must be at least 2.");

        return MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);
    }

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeSplitChordsOptions options)
    {
        var result = Apply(
            context.File,
            context.SelectedTrackIndices,
            options);

        return new MidiEditorTransformResult(
            Changed: result.CreatedTracks > 0,
            Summary: $"created {result.CreatedTracks} split track(s) from {result.ChordGroups} chord group(s)",
            ClearTrackSelection: result.CreatedTracks > 0);
    }

    public static MidiForgeSplitChordsResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeSplitChordsOptions options)
    {
        var validTrackIndices = trackIndices
            .Where(index => index >= 0 && index < file.Tracks.Count && !file.Tracks[index].IsConductorTrack)
            .Distinct()
            .OrderByDescending(index => index)
            .ToArray();

        var sourceTracks = 0;
        var createdTracks = 0;
        var chordGroups = 0;
        var minimumSimultaneousNotes = Math.Clamp(options.MinimumSimultaneousNotes, 2, 10);

        foreach (var trackIndex in validTrackIndices)
        {
            var track = file.Tracks[trackIndex];
            var sourceChunk = track.CloneCurrentChunk();
            var notes = sourceChunk.GetNotes().ToArray();
            if (notes.Length == 0)
                continue;

            var splitGroups = MidiForgeChordPartitioner.SplitChordNotes(
                notes,
                track.DisplayName,
                options.Strategy,
                options.GroupMode,
                minimumSimultaneousNotes)
                .ToArray();
            if (splitGroups.Length == 0)
                continue;

            sourceTracks++;
            chordGroups += splitGroups.Count(group => group.IsChord);

            var splitTracks = splitGroups
                .Select(group => MidiForgeNoteFactory.CreateTrackFromNotes(sourceChunk, group.TrackName, group.Notes))
                .Select(chunk => new EditableTrack(chunk, 0))
                .ToArray();

            if (options.InsertPartsAtEnd)
            {
                foreach (var splitTrack in splitTracks)
                    file.Tracks.Insert(file.Tracks.Count, splitTrack);
            }
            else
            {
                foreach (var splitTrack in splitTracks.Reverse())
                    file.Tracks.Insert(trackIndex + 1, splitTrack);
            }

            createdTracks += splitTracks.Length;
        }

        if (createdTracks > 0)
            MidiForgeTrackMutation.RefreshTrackIndexesAndDirty(file);

        return new MidiForgeSplitChordsResult(sourceTracks, createdTracks, chordGroups);
    }
}
