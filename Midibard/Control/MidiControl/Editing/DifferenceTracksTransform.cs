using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class DifferenceTracksTransform : IMidiEditorTransform<MidiForgeComparisonTrackOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.difference-tracks", "Difference Tracks");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeComparisonTrackOptions options)
        => MidiForgeNoteTransformResult.ValidateComparison(context, options);

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeComparisonTrackOptions options)
    {
        var result = Apply(context.File, context.SelectedTrackIndices, options.TargetTrackIndex);

        return MidiForgeNoteTransformResult.CreatedTrackResult(
            result.CreatedTracks,
            $"created {result.CreatedTracks} difference track(s)");
    }

    public static MidiForgeDifferenceTracksResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        int targetTrackIndex)
    {
        var validTrackIndices = MidiForgeTrackMutation.GetValidComparisonTrackIndices(file, trackIndices);
        if (validTrackIndices.Length < 2 || !validTrackIndices.Contains(targetTrackIndex))
            return new MidiForgeDifferenceTracksResult(validTrackIndices.Length, 0, 0, 0);

        var targetTrack = file.Tracks[targetTrackIndex];
        var sourceChunk = targetTrack.CloneCurrentChunk();
        var targetNotes = sourceChunk.GetNotes().ToArray();
        var comparisonNotes = validTrackIndices
            .Where(index => index != targetTrackIndex)
            .SelectMany(index => file.Tracks[index].CloneCurrentChunk().GetNotes())
            .ToArray();

        if (targetNotes.Length == 0 || comparisonNotes.Length == 0)
            return new MidiForgeDifferenceTracksResult(validTrackIndices.Length, 0, 0, 0);

        var diffNotes = targetNotes
            .Where(note => !comparisonNotes.Any(comparison => MidiForgeNoteTiming.NotesOverlap(note, comparison)))
            .Select(note => MidiForgeNoteFactory.CloneWithLength(note, note.Length))
            .ToArray();
        var restNotes = targetNotes
            .Where(note => comparisonNotes.Any(comparison => MidiForgeNoteTiming.NotesOverlap(note, comparison)))
            .Select(note => MidiForgeNoteFactory.CloneWithLength(note, note.Length))
            .ToArray();

        var createdTracks = 0;
        createdTracks += MidiForgeTrackMutation.InsertDerivedTrackAfterTarget(
            file,
            targetTrackIndex,
            sourceChunk,
            $"{targetTrack.DisplayName} (Diff Rest)",
            restNotes);
        createdTracks += MidiForgeTrackMutation.InsertDerivedTrackAfterTarget(
            file,
            targetTrackIndex,
            sourceChunk,
            $"{targetTrack.DisplayName} (Diff)",
            diffNotes);

        if (createdTracks > 0)
            MidiForgeTrackMutation.RefreshTrackIndexesAndDirty(file);

        return new MidiForgeDifferenceTracksResult(
            validTrackIndices.Length,
            createdTracks,
            diffNotes.Length,
            restNotes.Length);
    }
}
