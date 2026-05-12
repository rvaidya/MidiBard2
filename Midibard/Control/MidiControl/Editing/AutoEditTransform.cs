using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class AutoEditTransform : IMidiEditorTransform<MidiForgeAutoEditOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.auto-edit", "Auto Edit");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeAutoEditOptions options)
        => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeAutoEditOptions options)
    {
        var result = Apply(
            context.File,
            context.SelectedTrackIndices,
            options);
        var changed = result.CreatedTracks > 0 || result.ReplacedTracks > 0;
        var replacedSelectedTrack = !options.CreateNewTracks
            && MidiEditorTransformValidationHelpers.IncludesSelectedTrack(context);

        return new MidiEditorTransformResult(
            Changed: changed,
            Summary: $"auto-edited {result.SourceTracks} track(s), picked {result.PickedParts} part(s)",
            ClearTrackSelection: changed,
            ClearEventSelection: changed && replacedSelectedTrack,
            ReloadSelectedTrack: changed && replacedSelectedTrack);
    }

    public static MidiForgeAutoEditResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeAutoEditOptions options)
    {
        var validTrackIndices = trackIndices
            .Where(index => index >= 0 && index < file.Tracks.Count && !file.Tracks[index].IsConductorTrack)
            .Distinct()
            .OrderByDescending(index => index)
            .ToArray();

        var sourceTracks = 0;
        var createdTracks = 0;
        var replacedTracks = 0;
        var pickedParts = 0;
        var changedNotes = 0;
        var maxSimultaneousNotes = Math.Clamp(options.MaxSimultaneousNotes, 1, 3);

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
                MidiForgeChordSplitStrategy.SameStartTick,
                MidiForgeChordGroupMode.GroupMerged,
                2)
                .Where(group => MidiForgeChordPartitioner.ShouldPickAutoEditGroup(group, maxSimultaneousNotes, options.PickStrategy))
                .ToArray();
            if (splitGroups.Length == 0)
                continue;

            sourceTracks++;
            pickedParts += splitGroups.Length;

            var autoEditTrackName = $"{track.DisplayName} (Auto Edited Max {maxSimultaneousNotes})";
            var autoEditChunk = MidiForgeNoteFactory.CreateTrackFromNotes(
                sourceChunk,
                autoEditTrackName,
                splitGroups.SelectMany(group => group.Notes));

            if (options.AdaptOutOfRangeNotes)
                changedNotes += MidiForgePlayableRange.AdaptChunkNoteNumbers(autoEditChunk, 0);

            if (options.CreateNewTracks)
            {
                file.Tracks.Insert(trackIndex + 1, new EditableTrack(autoEditChunk, trackIndex + 1));
                createdTracks++;
            }
            else
            {
                track.Dispose();
                file.Tracks[trackIndex] = new EditableTrack(autoEditChunk, trackIndex);
                replacedTracks++;
            }
        }

        if (createdTracks > 0 || replacedTracks > 0)
            MidiForgeTrackMutation.RefreshTrackIndexesAndDirty(file);

        return new MidiForgeAutoEditResult(
            sourceTracks,
            createdTracks,
            replacedTracks,
            pickedParts,
            changedNotes);
    }
}
