using System.Collections.Generic;
using System.Linq;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class FillEmptyTrackNamesTransform :
    IMidiEditorTransform<MidiForgeFillEmptyTrackNamesTransformOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("track-names.fill-empty", "Fill Empty Track Names");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeFillEmptyTrackNamesTransformOptions options)
    {
        if (context.SelectedTrackIndices.Count == 0)
            return MidiEditorTransformValidation.Failure("No performance tracks selected.");

        return context.SelectedTrackIndices.Any(i => string.IsNullOrWhiteSpace(context.File.Tracks[i].Name))
            ? MidiEditorTransformValidation.Success
            : MidiEditorTransformValidation.Failure("Selected tracks already have names.");
    }

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeFillEmptyTrackNamesTransformOptions options)
    {
        var result = Apply(context.File, context.SelectedTrackIndices, options.FillMode);

        return new MidiEditorTransformResult(
            Changed: result.RenamedTracks > 0,
            Summary: $"renamed {result.RenamedTracks} track(s)",
            ClearTrackSelection: result.RenamedTracks > 0);
    }

    public static MidiForgeTrackNameResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeTrackNameFillMode fillMode)
    {
        var validTrackIndices = trackIndices
            .Where(index => index >= 0 && index < file.Tracks.Count && !file.Tracks[index].IsConductorTrack)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();

        var renamedTracks = 0;

        foreach (var (trackIndex, fallbackIndex) in validTrackIndices.Select((index, order) => (index, order + 1)))
        {
            var track = file.Tracks[trackIndex];
            if (!string.IsNullOrWhiteSpace(track.Name))
                continue;

            var defaultName = MidiForgeTrackNameEditor.GetDefaultTrackName(track, fillMode, fallbackIndex);
            if (MidiForgeTrackNameEditor.SetEditableTrackName(track, defaultName))
                renamedTracks++;
        }

        if (renamedTracks > 0)
            file.MarkChanged();

        return new MidiForgeTrackNameResult(validTrackIndices.Length, renamedTracks);
    }
}
