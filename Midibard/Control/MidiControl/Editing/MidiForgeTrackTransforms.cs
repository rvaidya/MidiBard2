namespace MidiBard.Control.MidiControl.Editing;

public static class MidiForgeTrackTransforms
{
    public static IMidiEditorTransform<MidiForgeTransposeTracksOptions> TransposeTracks { get; } =
        new TransposeTracksTransform();

    public static IMidiEditorTransform<MidiForgeMergeTracksOptions> MergeTracks { get; } =
        new MergeTracksTransform();

    public static IMidiEditorTransform<MidiForgeQuantizeTracksOptions> QuantizeTracks { get; } =
        new QuantizeTracksTransform();

    public static IMidiEditorTransform<MidiForgeQuantizeSelectedNotesOptions> QuantizeSelectedNotes { get; } =
        new QuantizeSelectedNotesTransform();

    public static IMidiEditorTransform<MidiForgeSanitizeOptions> SanitizeFile { get; } =
        new SanitizeFileTransform();

    public static IMidiEditorTransform<MidiForgeChangeNoteLengthOptions> ChangeNoteLengths { get; } =
        new ChangeNoteLengthsTransform();

    public static IMidiEditorTransform<MidiForgeSetTrackProgramOptions> SetPrograms { get; } =
        new SetProgramsTransform();
}
