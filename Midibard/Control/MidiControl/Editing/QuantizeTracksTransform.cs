namespace MidiBard.Control.MidiControl.Editing;

public sealed class QuantizeTracksTransform : IMidiEditorTransform<MidiForgeQuantizeTracksOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("track.quantize", "Quantize Tracks");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeQuantizeTracksOptions options)
        => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeQuantizeTracksOptions options)
    {
        var changedTracks = context.File.QuantizeTracks(
            context.SelectedTrackIndices,
            options.Grid,
            options.Settings,
            options.CreateNewTrack);
        var changed = changedTracks > 0;
        var replacedSelectedTrack = !options.CreateNewTrack
            && MidiEditorTransformValidationHelpers.IncludesSelectedTrack(context);

        return new MidiEditorTransformResult(
            Changed: changed,
            Summary: $"quantized {changedTracks} track(s)",
            ClearTrackSelection: changed,
            ClearEventSelection: changed && replacedSelectedTrack,
            ReloadSelectedTrack: changed && replacedSelectedTrack);
    }
}
