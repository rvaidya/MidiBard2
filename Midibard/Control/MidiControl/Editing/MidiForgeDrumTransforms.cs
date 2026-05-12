namespace MidiBard.Control.MidiControl.Editing;

public static class MidiForgeDrumTransforms
{
    public static IMidiEditorTransform<MidiForgeSplitDrumkitOptions> SplitDrumkit { get; } =
        new SplitDrumkitTransform();

    public static IMidiEditorTransform<MidiForgeDisassembleDrumkitOptions> DisassembleDrumkit { get; } =
        new DisassembleDrumkitTransform();

    public static IMidiEditorTransform<MidiForgeTransposeToDrumNoteOptions> TransposeSingleNoteTracksToDrumNote { get; } =
        new TransposeSingleNoteTracksToDrumNoteTransform();

    private sealed class SplitDrumkitTransform : IMidiEditorTransform<MidiForgeSplitDrumkitOptions>
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
            var result = MidiForgeOperations.SplitDrumkitTracks(
                context.File,
                context.SelectedTrackIndices,
                options);

            return CreateDrumResult(
                result.CreatedTracks > 0,
                $"created {result.CreatedTracks} drum track(s)");
        }
    }

    private sealed class DisassembleDrumkitTransform : IMidiEditorTransform<MidiForgeDisassembleDrumkitOptions>
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
            var result = MidiForgeOperations.DisassembleDrumkitTracks(
                context.File,
                context.SelectedTrackIndices,
                options);

            return CreateDrumResult(
                result.CreatedTracks > 0 || result.DeletedSourceTracks > 0,
                $"created {result.CreatedTracks} drum note track(s)");
        }
    }

    private sealed class TransposeSingleNoteTracksToDrumNoteTransform :
        IMidiEditorTransform<MidiForgeTransposeToDrumNoteOptions>
    {
        public MidiEditorTransformDescriptor Descriptor { get; } =
            new("drum.transpose-single-note-tracks", "Transpose Single-Note Tracks to Drum Note");

        public MidiEditorTransformValidation Validate(
            MidiEditorTransformContext context,
            MidiForgeTransposeToDrumNoteOptions options)
            => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

        public MidiEditorTransformResult Execute(
            MidiEditorTransformContext context,
            MidiForgeTransposeToDrumNoteOptions options)
        {
            var result = MidiForgeOperations.TransposeSingleNoteTracksToDrumNote(
                context.File,
                context.SelectedTrackIndices,
                options);

            return CreateDrumResult(
                result.CreatedTracks > 0 || result.DeletedSourceTracks > 0,
                $"created {result.CreatedTracks} transposed drum track(s)");
        }
    }

    private static MidiEditorTransformResult CreateDrumResult(bool changed, string summary)
        => new(
            Changed: changed,
            Summary: summary,
            ClearTrackSelection: changed,
            ClearEventSelection: changed,
            ClearSelectedTrack: changed);
}
