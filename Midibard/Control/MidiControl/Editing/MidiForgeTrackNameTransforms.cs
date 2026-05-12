namespace MidiBard.Control.MidiControl.Editing;

public sealed record MidiForgeFillEmptyTrackNamesTransformOptions(MidiForgeTrackNameFillMode FillMode);

public sealed record MidiForgeClearTrackNamesTransformOptions();

public static class MidiForgeTrackNameTransforms
{
    public static IMidiEditorTransform<MidiForgeFillEmptyTrackNamesTransformOptions> FillEmpty { get; } =
        new FillEmptyTrackNamesTransform();

    public static IMidiEditorTransform<MidiForgeClearTrackNamesTransformOptions> Clear { get; } =
        new ClearTrackNamesTransform();
}
