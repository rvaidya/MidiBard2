using System.Linq;

namespace MidiBard.Control.MidiControl.Editing;

public sealed record MidiForgeFillEmptyTrackNamesTransformOptions(MidiForgeTrackNameFillMode FillMode);

public sealed record MidiForgeClearTrackNamesTransformOptions();

public static class MidiForgeTrackNameTransforms
{
    public static IMidiEditorTransform<MidiForgeFillEmptyTrackNamesTransformOptions> FillEmpty { get; } =
        new FillEmptyTrackNamesTransform();

    public static IMidiEditorTransform<MidiForgeClearTrackNamesTransformOptions> Clear { get; } =
        new ClearTrackNamesTransform();

    private sealed class FillEmptyTrackNamesTransform : IMidiEditorTransform<MidiForgeFillEmptyTrackNamesTransformOptions>
    {
        public MidiEditorTransformDescriptor Descriptor { get; } = new("track-names.fill-empty", "Fill Empty Track Names");

        public MidiEditorTransformValidation Validate(
            MidiEditorTransformContext context,
            MidiForgeFillEmptyTrackNamesTransformOptions options)
        {
            if (context.SelectedTrackIndices.Count == 0)
                return MidiEditorTransformValidation.Failure("No performance tracks selected.");

            return context.SelectedTrackIndices.Any(i => string.IsNullOrWhiteSpace(context.File.Tracks[i].Name))
                ? MidiEditorTransformValidation.Success
                : MidiEditorTransformValidation.Failure("Selected tracks already have names.");
        }

        public MidiEditorTransformResult Execute(
            MidiEditorTransformContext context,
            MidiForgeFillEmptyTrackNamesTransformOptions options)
        {
            var result = MidiForgeOperations.FillEmptyTrackNames(
                context.File,
                context.SelectedTrackIndices,
                options.FillMode);

            return new MidiEditorTransformResult(
                Changed: result.RenamedTracks > 0,
                Summary: $"renamed {result.RenamedTracks} track(s)",
                ClearTrackSelection: result.RenamedTracks > 0);
        }
    }

    private sealed class ClearTrackNamesTransform : IMidiEditorTransform<MidiForgeClearTrackNamesTransformOptions>
    {
        public MidiEditorTransformDescriptor Descriptor { get; } = new("track-names.clear", "Clear Track Names");

        public MidiEditorTransformValidation Validate(
            MidiEditorTransformContext context,
            MidiForgeClearTrackNamesTransformOptions options)
        {
            if (context.SelectedTrackIndices.Count == 0)
                return MidiEditorTransformValidation.Failure("No performance tracks selected.");

            return context.SelectedTrackIndices.Any(i => !string.IsNullOrWhiteSpace(context.File.Tracks[i].Name))
                ? MidiEditorTransformValidation.Success
                : MidiEditorTransformValidation.Failure("Selected tracks already have empty names.");
        }

        public MidiEditorTransformResult Execute(
            MidiEditorTransformContext context,
            MidiForgeClearTrackNamesTransformOptions options)
        {
            var result = MidiForgeOperations.ClearTrackNames(context.File, context.SelectedTrackIndices);

            return new MidiEditorTransformResult(
                Changed: result.RenamedTracks > 0,
                Summary: $"cleared {result.RenamedTracks} track name(s)",
                ClearTrackSelection: result.RenamedTracks > 0);
        }
    }
}
