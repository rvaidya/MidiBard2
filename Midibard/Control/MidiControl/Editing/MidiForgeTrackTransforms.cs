namespace MidiBard.Control.MidiControl.Editing;

public static class MidiForgeTrackTransforms
{
    public static IMidiEditorTransform<MidiForgeChangeNoteLengthOptions> ChangeNoteLengths { get; } =
        new ChangeNoteLengthsTransform();

    public static IMidiEditorTransform<MidiForgeSetTrackProgramOptions> SetPrograms { get; } =
        new SetProgramsTransform();

    private sealed class ChangeNoteLengthsTransform : IMidiEditorTransform<MidiForgeChangeNoteLengthOptions>
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
            var result = MidiForgeOperations.ChangeTrackNoteLengths(
                context.File,
                context.SelectedTrackIndices,
                options);
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
    }

    private sealed class SetProgramsTransform : IMidiEditorTransform<MidiForgeSetTrackProgramOptions>
    {
        public MidiEditorTransformDescriptor Descriptor { get; } = new("track.set-programs", "Set Track Programs");

        public MidiEditorTransformValidation Validate(
            MidiEditorTransformContext context,
            MidiForgeSetTrackProgramOptions options)
            => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

        public MidiEditorTransformResult Execute(
            MidiEditorTransformContext context,
            MidiForgeSetTrackProgramOptions options)
        {
            var result = MidiForgeOperations.SetTrackPrograms(
                context.File,
                context.SelectedTrackIndices,
                options);
            var changed = result.ChangedTracks > 0;
            var changedSelectedTrack = MidiEditorTransformValidationHelpers.IncludesSelectedTrack(context);

            return new MidiEditorTransformResult(
                Changed: changed,
                Summary: $"updated {result.ChangedTracks} track program(s)",
                ClearTrackSelection: changed,
                ClearEventSelection: changed && changedSelectedTrack,
                ReloadSelectedTrack: changed && changedSelectedTrack);
        }
    }
}
