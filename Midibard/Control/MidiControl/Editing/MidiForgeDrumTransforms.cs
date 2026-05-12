namespace MidiBard.Control.MidiControl.Editing;

public static class MidiForgeDrumTransforms
{
    public static IMidiEditorTransform<MidiForgeSplitDrumkitOptions> SplitDrumkit { get; } =
        new SplitDrumkitTransform();

    public static IMidiEditorTransform<MidiForgeDisassembleDrumkitOptions> DisassembleDrumkit { get; } =
        new DisassembleDrumkitTransform();

    public static IMidiEditorTransform<MidiForgeTransposeToDrumNoteOptions> TransposeSingleNoteTracksToDrumNote { get; } =
        new TransposeSingleNoteTracksToDrumNoteTransform();
}
