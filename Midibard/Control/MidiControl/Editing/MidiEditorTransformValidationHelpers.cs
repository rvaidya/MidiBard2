using System.Linq;

namespace MidiBard.Control.MidiControl.Editing;

internal static class MidiEditorTransformValidationHelpers
{
    public static MidiEditorTransformValidation RequireSelectedTracks(MidiEditorTransformContext context)
        => context.SelectedTrackIndices.Count == 0
            ? MidiEditorTransformValidation.Failure("No performance tracks selected.")
            : MidiEditorTransformValidation.Success;

    public static bool IncludesSelectedTrack(MidiEditorTransformContext context)
        => context.SelectedTrackIndex >= 0 && context.SelectedTrackIndices.Contains(context.SelectedTrackIndex);
}
