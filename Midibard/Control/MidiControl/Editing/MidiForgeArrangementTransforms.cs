namespace MidiBard.Control.MidiControl.Editing;

public static class MidiForgeArrangementTransforms
{
    public static IMidiEditorTransform<MidiForgeAdaptToRangeOptions> AdaptToRange { get; } =
        new AdaptToRangeTransform();

    public static IMidiEditorTransform<MidiForgeAutoEditOptions> AutoEdit { get; } =
        new AutoEditTransform();

    public static IMidiEditorTransform<MidiForgeSplitChordsOptions> SplitChords { get; } =
        new SplitChordsTransform();
}
