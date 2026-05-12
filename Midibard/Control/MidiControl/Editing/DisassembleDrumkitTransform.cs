using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class DisassembleDrumkitTransform : IMidiEditorTransform<MidiForgeDisassembleDrumkitOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("drum.disassemble-drumkit", "Disassemble Drumkit");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeDisassembleDrumkitOptions options)
        => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeDisassembleDrumkitOptions options)
    {
        var result = Apply(
            context.File,
            context.SelectedTrackIndices,
            options);

        return MidiForgeDrumTransformResult.Create(
            result.CreatedTracks > 0 || result.DeletedSourceTracks > 0,
            $"created {result.CreatedTracks} drum note track(s)");
    }

    public static MidiForgeDisassembleDrumkitResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeDisassembleDrumkitOptions options)
    {
        var validTrackIndices = trackIndices
            .Where(index => index >= 0 && index < file.Tracks.Count && !file.Tracks[index].IsConductorTrack)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();

        var sourceTracks = 0;
        var createdTracks = 0;
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

            foreach (var group in drumNotes
                .GroupBy(note => (byte)note.NoteNumber)
                .OrderBy(group => group.Key))
            {
                var trackName = MidiForgeDrumMaps.GetDrumkitInstrumentName(group.Key);
                file.Tracks.Add(new EditableTrack(
                    MidiForgeNoteFactory.CreateTrackFromNotes(
                        sourceChunk,
                        trackName,
                        group.Select(note => MidiForgeNoteFactory.CloneWithNumber(note, group.Key))),
                    file.Tracks.Count));
                createdTracks++;
                createdFromSource++;
            }

            if (createdFromSource > 0)
            {
                sourceTracks++;
                sourceTrackRefs.Add(track);
            }
        }

        var deletedSourceTracks = 0;
        if (options.DeleteOriginalTracks && sourceTrackRefs.Count > 0)
        {
            foreach (var track in sourceTrackRefs)
            {
                var index = file.Tracks.IndexOf(track);
                if (index < 0)
                    continue;

                track.Dispose();
                file.Tracks.RemoveAt(index);
                deletedSourceTracks++;
            }
        }

        if (createdTracks > 0 || deletedSourceTracks > 0)
            MidiForgeTrackMutation.RefreshTrackIndexesAndDirty(file);

        return new MidiForgeDisassembleDrumkitResult(
            sourceTracks,
            createdTracks,
            deletedSourceTracks);
    }
}
