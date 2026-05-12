namespace MidiBard.Control.MidiControl.Editing;

public sealed class SanitizeFileTransform : IMidiEditorTransform<MidiForgeSanitizeOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("file.sanitize", "Sanitize MIDI File");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeSanitizeOptions options)
        => MidiEditorTransformValidation.Success;

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeSanitizeOptions options)
    {
        var beforeVersion = context.File.Version;
        context.File.SanitizeFile(options.Settings);
        var changed = context.File.Version != beforeVersion;

        return new MidiEditorTransformResult(
            Changed: changed,
            Summary: changed ? "sanitized MIDI file" : "MIDI file did not need sanitizing",
            ClearTrackSelection: changed,
            ClearEventSelection: changed,
            ClearSelectedTrack: changed);
    }
}
