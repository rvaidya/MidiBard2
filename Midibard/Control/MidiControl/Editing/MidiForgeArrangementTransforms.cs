namespace MidiBard.Control.MidiControl.Editing;

public static class MidiForgeArrangementTransforms
{
    public static IMidiEditorTransform<MidiForgeAdaptToRangeOptions> AdaptToRange { get; } =
        new AdaptToRangeTransform();

    public static IMidiEditorTransform<MidiForgeAutoEditOptions> AutoEdit { get; } =
        new AutoEditTransform();

    public static IMidiEditorTransform<MidiForgeSplitChordsOptions> SplitChords { get; } =
        new SplitChordsTransform();

    private sealed class AdaptToRangeTransform : IMidiEditorTransform<MidiForgeAdaptToRangeOptions>
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
            var result = MidiForgeOperations.AdaptTracksToPlayableRange(
                context.File,
                context.SelectedTrackIndices,
                options);
            var changed = result.CreatedTracks > 0 || result.ReplacedTracks > 0;

            return new MidiEditorTransformResult(
                Changed: changed,
                Summary: $"adapted {result.SourceTracks} track(s), changed {result.ChangedNotes} note(s)",
                ClearTrackSelection: changed,
                ClearEventSelection: changed && !options.CreateNewTracks && MidiEditorTransformValidationHelpers.IncludesSelectedTrack(context),
                ReloadSelectedTrack: changed && !options.CreateNewTracks && MidiEditorTransformValidationHelpers.IncludesSelectedTrack(context));
        }
    }

    private sealed class AutoEditTransform : IMidiEditorTransform<MidiForgeAutoEditOptions>
    {
        public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.auto-edit", "Auto Edit");

        public MidiEditorTransformValidation Validate(
            MidiEditorTransformContext context,
            MidiForgeAutoEditOptions options)
            => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

        public MidiEditorTransformResult Execute(
            MidiEditorTransformContext context,
            MidiForgeAutoEditOptions options)
        {
            var result = MidiForgeOperations.AutoEditTracks(
                context.File,
                context.SelectedTrackIndices,
                options);
            var changed = result.CreatedTracks > 0 || result.ReplacedTracks > 0;

            return new MidiEditorTransformResult(
                Changed: changed,
                Summary: $"auto-edited {result.SourceTracks} track(s), picked {result.PickedParts} part(s)",
                ClearTrackSelection: changed,
                ClearEventSelection: changed && !options.CreateNewTracks && MidiEditorTransformValidationHelpers.IncludesSelectedTrack(context),
                ReloadSelectedTrack: changed && !options.CreateNewTracks && MidiEditorTransformValidationHelpers.IncludesSelectedTrack(context));
        }
    }

    private sealed class SplitChordsTransform : IMidiEditorTransform<MidiForgeSplitChordsOptions>
    {
        public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.split-chords", "Split Chords");

        public MidiEditorTransformValidation Validate(
            MidiEditorTransformContext context,
            MidiForgeSplitChordsOptions options)
        {
            if (options.MinimumSimultaneousNotes < 2)
                return MidiEditorTransformValidation.Failure("Minimum simultaneous notes must be at least 2.");

            return MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);
        }

        public MidiEditorTransformResult Execute(
            MidiEditorTransformContext context,
            MidiForgeSplitChordsOptions options)
        {
            var result = MidiForgeOperations.SplitTracksChords(
                context.File,
                context.SelectedTrackIndices,
                options);

            return new MidiEditorTransformResult(
                Changed: result.CreatedTracks > 0,
                Summary: $"created {result.CreatedTracks} split track(s) from {result.ChordGroups} chord group(s)",
                ClearTrackSelection: result.CreatedTracks > 0);
        }
    }
}
