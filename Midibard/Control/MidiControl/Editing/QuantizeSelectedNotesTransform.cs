namespace MidiBard.Control.MidiControl.Editing;

public sealed class QuantizeSelectedNotesTransform : IMidiEditorTransform<MidiForgeQuantizeSelectedNotesOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("track.quantize-selected-notes", "Quantize Selected Notes");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeQuantizeSelectedNotesOptions options)
    {
        if (options.TrackIndex < 0 || options.TrackIndex >= context.File.Tracks.Count)
            return MidiEditorTransformValidation.Failure("No editable track is selected.");
        if (options.SelectedKeys.Count == 0)
            return MidiEditorTransformValidation.Failure("No notes are selected.");

        return MidiEditorTransformValidation.Success;
    }

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeQuantizeSelectedNotesOptions options)
    {
        var changed = context.File.QuantizeNotes(
            options.TrackIndex,
            options.SelectedKeys,
            options.Grid,
            options.Settings);

        return new MidiEditorTransformResult(
            Changed: changed,
            Summary: changed ? $"quantized {options.SelectedKeys.Count} selected note(s)" : "selected notes already match the grid",
            ClearEventSelection: changed,
            ReloadSelectedTrack: changed);
    }
}
