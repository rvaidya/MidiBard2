namespace MidiBard.Control.MidiControl.Editing;

internal static class MidiForgeDrumTransformResult
{
    public static MidiEditorTransformResult Create(bool changed, string summary)
        => new(
            Changed: changed,
            Summary: summary,
            ClearTrackSelection: changed,
            ClearEventSelection: changed,
            ClearSelectedTrack: changed);
}
