using System.Collections.Generic;
using System.Linq;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class ClearTrackNamesTransform :
    IMidiEditorTransform<MidiForgeClearTrackNamesTransformOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("track-names.clear", "Clear Track Names");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeClearTrackNamesTransformOptions options)
    {
        if (context.SelectedTrackIndices.Count == 0)
            return MidiEditorTransformValidation.Failure("No performance tracks selected.");

        return context.SelectedTrackIndices.Any(i => !string.IsNullOrWhiteSpace(context.File.Tracks[i].Name))
            ? MidiEditorTransformValidation.Success
            : MidiEditorTransformValidation.Failure("Selected tracks already have empty names.");
    }

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeClearTrackNamesTransformOptions options)
    {
        var result = Apply(context.File, context.SelectedTrackIndices);

        return new MidiEditorTransformResult(
            Changed: result.RenamedTracks > 0,
            Summary: $"cleared {result.RenamedTracks} track name(s)",
            ClearTrackSelection: result.RenamedTracks > 0);
    }

    public static MidiForgeTrackNameResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        bool preserveDrumInstrumentNames = true)
    {
        var validTrackIndices = trackIndices
            .Where(index => index >= 0 && index < file.Tracks.Count && !file.Tracks[index].IsConductorTrack)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();

        var renamedTracks = 0;

        foreach (var trackIndex in validTrackIndices)
        {
            var track = file.Tracks[trackIndex];
            if (string.IsNullOrWhiteSpace(track.Name))
                continue;

            if (preserveDrumInstrumentNames && MidiForgeTrackNameEditor.PreservedDrumTrackNames.Contains(track.Name))
                continue;

            if (MidiForgeTrackNameEditor.SetEditableTrackName(track, string.Empty))
                renamedTracks++;
        }

        if (renamedTracks > 0)
            file.MarkChanged();

        return new MidiForgeTrackNameResult(validTrackIndices.Length, renamedTracks);
    }
}
