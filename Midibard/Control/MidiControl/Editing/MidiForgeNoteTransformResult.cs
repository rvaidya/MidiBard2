using System.Linq;

namespace MidiBard.Control.MidiControl.Editing;

internal static class MidiForgeNoteTransformResult
{
    public static MidiEditorTransformResult CreatedTrackResult(int createdTracks, string summary)
        => new(
            Changed: createdTracks > 0,
            Summary: summary,
            ClearTrackSelection: createdTracks > 0);

    public static MidiEditorTransformValidation ValidateComparison(
        MidiEditorTransformContext context,
        MidiForgeComparisonTrackOptions options)
    {
        if (context.SelectedTrackIndices.Count < 2)
            return MidiEditorTransformValidation.Failure("At least two performance tracks must be selected.");
        if (!context.SelectedTrackIndices.Contains(options.TargetTrackIndex))
            return MidiEditorTransformValidation.Failure("The target track must be selected.");

        return MidiEditorTransformValidation.Success;
    }
}
