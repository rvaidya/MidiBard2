using System;
using System.Collections.Generic;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class SplitByToneRangeTransform : IMidiEditorTransform<MidiForgeSplitToneRangeOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.split-by-tone-range", "Split Notes by Tone Range");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeSplitToneRangeOptions options)
        => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeSplitToneRangeOptions options)
    {
        var result = Apply(context.File, context.SelectedTrackIndices, options);

        return MidiForgeNoteTransformResult.CreatedTrackResult(
            result.CreatedTracks,
            $"created {result.CreatedTracks} tone-range track(s)");
    }

    public static MidiForgeSplitNotesRangeResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeSplitToneRangeOptions options)
    {
        var minimumNote = Math.Clamp(options.MinimumNote, 0, 127);
        var maximumNote = Math.Clamp(options.MaximumNote, 0, 127);
        if (minimumNote > maximumNote)
            (minimumNote, maximumNote) = (maximumNote, minimumNote);

        var rangeLabel = $"{MidiForgeNoteNames.GetMidiNoteName(minimumNote)} ({minimumNote}) - {MidiForgeNoteNames.GetMidiNoteName(maximumNote)} ({maximumNote})";
        return MidiForgeRangeSplitter.SplitTracksByRange(
            file,
            trackIndices,
            note =>
            {
                var noteNumber = (byte)note.NoteNumber;
                return noteNumber >= minimumNote && noteNumber <= maximumNote;
            },
            trackName => $"{trackName} (In Range {rangeLabel})",
            trackName => $"{trackName} (Out of Range {rangeLabel})");
    }
}
