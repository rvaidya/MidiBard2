using System;
using System.Collections.Generic;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class SplitByLengthRangeTransform : IMidiEditorTransform<MidiForgeSplitLengthRangeOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.split-by-length-range", "Split Notes by Length Range");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeSplitLengthRangeOptions options)
        => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeSplitLengthRangeOptions options)
    {
        var result = Apply(context.File, context.SelectedTrackIndices, options);

        return MidiForgeNoteTransformResult.CreatedTrackResult(
            result.CreatedTracks,
            $"created {result.CreatedTracks} length-range track(s)");
    }

    public static MidiForgeSplitNotesRangeResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeSplitLengthRangeOptions options)
    {
        var minimumLengthTicks = Math.Max(0, options.MinimumLengthTicks);
        var maximumLengthTicks = Math.Max(0, options.MaximumLengthTicks);
        if (minimumLengthTicks > maximumLengthTicks)
            (minimumLengthTicks, maximumLengthTicks) = (maximumLengthTicks, minimumLengthTicks);

        var rangeLabel = $"{minimumLengthTicks} - {maximumLengthTicks}";
        return MidiForgeRangeSplitter.SplitTracksByRange(
            file,
            trackIndices,
            note => note.Length >= minimumLengthTicks && note.Length <= maximumLengthTicks,
            trackName => $"{trackName} (In Range {rangeLabel})",
            trackName => $"{trackName} (Out of Range {rangeLabel})");
    }
}
