using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class ChangeNoteLengthsTransform : IMidiEditorTransform<MidiForgeChangeNoteLengthOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("track.change-note-lengths", "Change Note Lengths");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeChangeNoteLengthOptions options)
    {
        if (options.NewLengthTicks <= 0)
            return MidiEditorTransformValidation.Failure("New length must be greater than zero.");

        return MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);
    }

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeChangeNoteLengthOptions options)
    {
        var result = Apply(context.File, context.SelectedTrackIndices, options);
        var changed = result.CreatedTracks > 0 || result.ReplacedTracks > 0;
        var replacedSelectedTrack = options.DeleteOriginalTracks
            && MidiEditorTransformValidationHelpers.IncludesSelectedTrack(context);

        return new MidiEditorTransformResult(
            Changed: changed,
            Summary: $"changed {result.ChangedNotes} note length(s)",
            ClearTrackSelection: changed,
            ClearEventSelection: changed && replacedSelectedTrack,
            ReloadSelectedTrack: changed && replacedSelectedTrack);
    }

    public static MidiForgeChangeNoteLengthResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeChangeNoteLengthOptions options)
    {
        var validTrackIndices = trackIndices
            .Where(index => index >= 0 && index < file.Tracks.Count && !file.Tracks[index].IsConductorTrack)
            .Distinct()
            .OrderByDescending(index => index)
            .ToArray();

        var minimumLengthTicks = Math.Max(0, options.MinimumLengthTicks);
        var maximumLengthTicks = Math.Max(0, options.MaximumLengthTicks);
        if (minimumLengthTicks > maximumLengthTicks)
            (minimumLengthTicks, maximumLengthTicks) = (maximumLengthTicks, minimumLengthTicks);
        var newLengthTicks = Math.Max(1, options.NewLengthTicks);

        var sourceTracks = 0;
        var createdTracks = 0;
        var replacedTracks = 0;
        var changedNotes = 0;

        foreach (var trackIndex in validTrackIndices)
        {
            var track = file.Tracks[trackIndex];
            var sourceChunk = track.CloneCurrentChunk();
            var notes = sourceChunk.GetNotes().ToArray();
            if (notes.Length == 0)
                continue;

            var changedNotesInTrack = 0;
            var modifiedNotes = notes
                .Select(note =>
                {
                    if (note.Length < minimumLengthTicks || note.Length > maximumLengthTicks)
                        return MidiForgeNoteFactory.CloneWithLength(note, note.Length);

                    changedNotesInTrack++;
                    return MidiForgeNoteFactory.CloneWithLength(note, newLengthTicks);
                })
                .ToArray();

            if (changedNotesInTrack == 0)
                continue;

            sourceTracks++;
            changedNotes += changedNotesInTrack;

            var changedChunk = MidiForgeNoteFactory.CreateTrackFromNotes(
                sourceChunk,
                $"{track.DisplayName} (Changed {changedNotesInTrack} notes)",
                modifiedNotes);

            if (options.DeleteOriginalTracks)
            {
                track.Dispose();
                file.Tracks[trackIndex] = new EditableTrack(changedChunk, trackIndex);
                replacedTracks++;
            }
            else
            {
                file.Tracks.Insert(trackIndex + 1, new EditableTrack(changedChunk, trackIndex + 1));
                createdTracks++;
            }
        }

        if (createdTracks > 0 || replacedTracks > 0)
            MidiForgeTrackMutation.RefreshTrackIndexesAndDirty(file);

        return new MidiForgeChangeNoteLengthResult(
            sourceTracks,
            createdTracks,
            replacedTracks,
            changedNotes);
    }
}
