using System.Linq;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class MergeTracksTransform : IMidiEditorTransform<MidiForgeMergeTracksOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("track.merge", "Merge Tracks");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeMergeTracksOptions options)
    {
        if (context.SelectedTrackIndices.Count < 2)
            return MidiEditorTransformValidation.Failure("At least two performance tracks must be selected.");
        if (!context.SelectedTrackIndices.Contains(options.TargetTrackIndex))
            return MidiEditorTransformValidation.Failure("The target track must be selected.");

        return MidiEditorTransformValidation.Success;
    }

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeMergeTracksOptions options)
    {
        var newTrackIndex = context.File.MergeTracks(
            options.TargetTrackIndex,
            context.SelectedTrackIndices,
            includeProgramChange: options.IncludeProgramChange,
            includePitchBend: options.IncludePitchBend,
            includeControlChange: options.IncludeControlChange,
            toleranceMs: options.ToleranceMs,
            removeEqualNotes: options.RemoveEqualNotes,
            deleteOriginalTracks: options.DeleteOriginalTracks);
        var changed = newTrackIndex >= 0;

        return new MidiEditorTransformResult(
            Changed: changed,
            Summary: changed ? $"created merged track #{newTrackIndex + 1}" : "no tracks merged",
            ClearTrackSelection: changed,
            ClearEventSelection: changed,
            ClearSelectedTrack: changed);
    }
}
