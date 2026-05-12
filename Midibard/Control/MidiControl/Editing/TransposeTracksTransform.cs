namespace MidiBard.Control.MidiControl.Editing;

public sealed class TransposeTracksTransform : IMidiEditorTransform<MidiForgeTransposeTracksOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("track.transpose", "Transpose Tracks");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeTransposeTracksOptions options)
        => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeTransposeTracksOptions options)
    {
        var beforeVersion = context.File.Version;
        var changedNotes = context.File.TransposeTracks(
            context.SelectedTrackIndices,
            options.Semitones,
            options.MinimumNoteNumber,
            options.MaximumNoteNumber,
            options.CreateNewTracks);
        var changed = context.File.Version != beforeVersion;
        var replacedSelectedTrack = !options.CreateNewTracks
            && MidiEditorTransformValidationHelpers.IncludesSelectedTrack(context);

        return new MidiEditorTransformResult(
            Changed: changed,
            Summary: $"transposed {changedNotes} note(s)",
            ClearTrackSelection: changed,
            ClearEventSelection: changed && replacedSelectedTrack,
            ReloadSelectedTrack: changed && replacedSelectedTrack);
    }
}
