using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class SplitEqualNotesTransform : IMidiEditorTransform<MidiForgeComparisonTrackOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.split-equal-notes", "Split Equal Notes");

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
            $"created {result.CreatedTracks} equal-note comparison track(s)");
    }

    public static MidiForgeSplitEqualNotesResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        int targetTrackIndex)
    {
        var validTrackIndices = MidiForgeTrackMutation.GetValidComparisonTrackIndices(file, trackIndices);
        if (validTrackIndices.Length < 2 || !validTrackIndices.Contains(targetTrackIndex))
            return new MidiForgeSplitEqualNotesResult(validTrackIndices.Length, 0, 0, 0);

        var targetTrack = file.Tracks[targetTrackIndex];
        var sourceChunk = targetTrack.CloneCurrentChunk();
        var targetNotes = sourceChunk.GetNotes().ToArray();
        var comparisonNotes = validTrackIndices
            .Where(index => index != targetTrackIndex)
            .SelectMany(index => file.Tracks[index].CloneCurrentChunk().GetNotes())
            .ToArray();

        if (targetNotes.Length == 0 || comparisonNotes.Length == 0)
            return new MidiForgeSplitEqualNotesResult(validTrackIndices.Length, 0, 0, 0);

        var equalNotes = targetNotes
            .Where(note => comparisonNotes.Any(comparison => MidiForgeNoteTiming.IsEqualNoteAtStart(note, comparison)))
            .Select(note => MidiForgeNoteFactory.CloneWithLength(note, note.Length))
            .ToArray();
        var nonEqualNotes = targetNotes
            .Where(note => !comparisonNotes.Any(comparison => MidiForgeNoteTiming.IsEqualNoteAtStart(note, comparison)))
            .Select(note => MidiForgeNoteFactory.CloneWithLength(note, note.Length))
            .ToArray();

        var createdTracks = 0;
        createdTracks += MidiForgeTrackMutation.InsertDerivedTrackAfterTarget(
            file,
            targetTrackIndex,
            sourceChunk,
            $"{targetTrack.DisplayName} (Equal Notes)",
            equalNotes);
        createdTracks += MidiForgeTrackMutation.InsertDerivedTrackAfterTarget(
            file,
            targetTrackIndex,
            sourceChunk,
            $"{targetTrack.DisplayName} (Non Equal Notes)",
            nonEqualNotes);

        if (createdTracks > 0)
            MidiForgeTrackMutation.RefreshTrackIndexesAndDirty(file);

        return new MidiForgeSplitEqualNotesResult(
            validTrackIndices.Length,
            createdTracks,
            equalNotes.Length,
            nonEqualNotes.Length);
    }
}
