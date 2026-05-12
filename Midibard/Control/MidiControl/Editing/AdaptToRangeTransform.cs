using System.Collections.Generic;
using System.Linq;

using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class AdaptToRangeTransform : IMidiEditorTransform<MidiForgeAdaptToRangeOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.adapt-to-range", "Adapt to Playable Range");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeAdaptToRangeOptions options)
        => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeAdaptToRangeOptions options)
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
            Summary: $"adapted {result.SourceTracks} track(s), changed {result.ChangedNotes} note(s)",
            ClearTrackSelection: changed,
            ClearEventSelection: changed && replacedSelectedTrack,
            ReloadSelectedTrack: changed && replacedSelectedTrack);
    }

    public static MidiForgeAdaptToRangeResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeAdaptToRangeOptions options)
    {
        var validTrackIndices = trackIndices
            .Where(index => index >= 0 && index < file.Tracks.Count && !file.Tracks[index].IsConductorTrack)
            .Distinct()
            .OrderByDescending(index => index)
            .ToArray();

        var sourceTracks = 0;
        var createdTracks = 0;
        var replacedTracks = 0;
        var octaveShiftedTracks = 0;
        var changedNotes = 0;

        foreach (var trackIndex in validTrackIndices)
        {
            var track = file.Tracks[trackIndex];
            var sourceChunk = track.CloneCurrentChunk();
            var notes = sourceChunk.GetNotes().ToArray();
            if (notes.Length == 0)
                continue;

            sourceTracks++;

            var octaveShift = options.SmartTranspose
                ? MidiForgeAnalysis.GetOptimalTransposeAmount(notes.Select(note => (int)(byte)note.NoteNumber))
                : 0;
            if (octaveShift != 0)
                octaveShiftedTracks++;

            var adaptedChunk = new TrackChunk(sourceChunk.Events.Select(e => e.Clone()));
            var changedNotesInTrack = MidiForgePlayableRange.AdaptChunkNoteNumbers(adaptedChunk, octaveShift);
            changedNotes += changedNotesInTrack;

            if (options.RenameTracks)
                MidiForgeTrackNameEditor.SetTrackName(adaptedChunk, $"{track.DisplayName} (Adapted {changedNotesInTrack} notes)");

            if (options.CreateNewTracks)
            {
                var newTrack = new EditableTrack(adaptedChunk, trackIndex + 1);
                file.Tracks.Insert(trackIndex + 1, newTrack);
                createdTracks++;
            }
            else
            {
                track.Dispose();
                file.Tracks[trackIndex] = new EditableTrack(adaptedChunk, trackIndex);
                replacedTracks++;
            }
        }

        if (createdTracks > 0 || replacedTracks > 0)
            MidiForgeTrackMutation.RefreshTrackIndexesAndDirty(file);

        return new MidiForgeAdaptToRangeResult(
            sourceTracks,
            createdTracks,
            replacedTracks,
            octaveShiftedTracks,
            changedNotes);
    }
}
