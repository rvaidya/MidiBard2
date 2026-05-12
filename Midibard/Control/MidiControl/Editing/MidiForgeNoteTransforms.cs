namespace MidiBard.Control.MidiControl.Editing;

public static class MidiForgeNoteTransforms
{
    public static IMidiEditorTransform<MidiForgeSplitToneRangeOptions> SplitByToneRange { get; } =
        new SplitByToneRangeTransform();

    public static IMidiEditorTransform<MidiForgeSplitLengthRangeOptions> SplitByLengthRange { get; } =
        new SplitByLengthRangeTransform();

    public static IMidiEditorTransform<MidiForgeSplitOverlappedNotesOptions> SplitOverlappedNotes { get; } =
        new SplitOverlappedNotesTransform();

    public static IMidiEditorTransform<MidiForgeTrimOverlappedNotesOptions> TrimOverlappedSustainedNotes { get; } =
        new TrimOverlappedSustainedNotesTransform();

    public static IMidiEditorTransform<MidiForgeExtendNotesDurationOptions> ExtendNotesDuration { get; } =
        new ExtendNotesDurationTransform();

    public static IMidiEditorTransform<MidiForgeComparisonTrackOptions> SplitEqualNotes { get; } =
        new SplitEqualNotesTransform();

    public static IMidiEditorTransform<MidiForgeComparisonTrackOptions> DifferenceTracks { get; } =
        new DifferenceTracksTransform();

    public static IMidiEditorTransform<MidiForgeSplitNotesIntoTracksOptions> SplitNotesIntoTracks { get; } =
        new SplitNotesIntoTracksTransform();

    public static IMidiEditorTransform<MidiForgeGeneratePitchBendNotesOptions> GeneratePitchBendNotes { get; } =
        new GeneratePitchBendNotesTransform();
}
