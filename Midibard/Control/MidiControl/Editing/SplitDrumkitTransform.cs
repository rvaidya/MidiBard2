using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class SplitDrumkitTransform : IMidiEditorTransform<MidiForgeSplitDrumkitOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("drum.split-drumkit", "Split Drumkit");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeSplitDrumkitOptions options)
        => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeSplitDrumkitOptions options)
    {
        var result = Apply(
            context.File,
            context.SelectedTrackIndices,
            options);

        return MidiForgeDrumTransformResult.Create(
            result.CreatedTracks > 0,
            $"created {result.CreatedTracks} drum track(s)");
    }

    public static MidiForgeSplitDrumkitResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeSplitDrumkitOptions options)
    {
        var validTrackIndices = trackIndices
            .Where(index => index >= 0 && index < file.Tracks.Count && !file.Tracks[index].IsConductorTrack)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();

        var sourceTracks = 0;
        var createdTracks = 0;
        var restTracks = 0;
        var autoEditedTracks = 0;
        var transposedNotes = 0;
        var sourceTrackRefs = new List<EditableTrack>();

        foreach (var trackIndex in validTrackIndices)
        {
            var track = file.Tracks[trackIndex];
            var sourceChunk = track.CloneCurrentChunk();
            var drumNotes = sourceChunk.GetNotes()
                .Where(note => (byte)note.Channel == MidiForgeAnalysis.DrumChannel)
                .ToArray();
            if (drumNotes.Length == 0)
                continue;

            var createdFromSource = 0;
            sourceTracks++;

            foreach (var mapping in MidiForgeDrumMaps.DefaultDrumkitMappings.Where(mapping => mapping.SourceNotes.Count > 0))
            {
                var mappedNotes = drumNotes
                    .Where(note => mapping.SourceNotes.Contains((byte)note.NoteNumber))
                    .Select(note =>
                    {
                        var sourceNoteNumber = (byte)note.NoteNumber;
                        var outputNoteNumber = MidiForgeDrumMaps.TransposeToOutputNote(
                            sourceNoteNumber,
                            options.TransposePreset);
                        if (outputNoteNumber != sourceNoteNumber)
                            transposedNotes++;
                        return MidiForgeNoteFactory.CloneWithNumber(note, outputNoteNumber);
                    })
                    .ToArray();

                if (mappedNotes.Length == 0)
                    continue;

                if (options.AutoEditAfterSplit)
                    mappedNotes = MidiForgeChordPartitioner.AutoEditDrumNotes(mappedNotes, mapping.TrackName, ref autoEditedTracks, ref transposedNotes);

                file.Tracks.Add(new EditableTrack(
                    MidiForgeNoteFactory.CreateTrackFromNotes(sourceChunk, mapping.TrackName, mappedNotes),
                    file.Tracks.Count));
                createdTracks++;
                createdFromSource++;
            }

            if (options.CreateRestTrack)
            {
                var restNotes = drumNotes
                    .Where(note => !MidiForgeDrumMaps.IsMappedSourceNote((byte)note.NoteNumber))
                    .Select(note => MidiForgeNoteFactory.CloneWithNumber(note, (byte)note.NoteNumber))
                    .ToArray();

                if (restNotes.Length > 0)
                {
                    file.Tracks.Add(new EditableTrack(
                        MidiForgeNoteFactory.CreateTrackFromNotes(sourceChunk, MidiForgeDrumMaps.RestTrackName, restNotes),
                        file.Tracks.Count));
                    createdTracks++;
                    createdFromSource++;
                    restTracks++;
                }
            }

            if (createdFromSource > 0)
                sourceTrackRefs.Add(track);
        }

        if (createdTracks > 0 && options.MoveSourceTracksToEnd)
            MidiForgeTrackMutation.MoveTracksToEnd(file, sourceTrackRefs);

        if (createdTracks > 0)
            MidiForgeTrackMutation.RefreshTrackIndexesAndDirty(file);

        return new MidiForgeSplitDrumkitResult(
            sourceTracks,
            createdTracks,
            restTracks,
            autoEditedTracks,
            transposedNotes);
    }
}
