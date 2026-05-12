using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class ExtendNotesDurationTransform : IMidiEditorTransform<MidiForgeExtendNotesDurationOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.extend-note-duration", "Extend Notes Duration");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeExtendNotesDurationOptions options)
        => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeExtendNotesDurationOptions options)
    {
        var result = Apply(context.File, context.SelectedTrackIndices, options);

        return MidiForgeNoteTransformResult.CreatedTrackResult(
            result.CreatedTracks,
            $"created {result.CreatedTracks} extended track(s)");
    }

    public static MidiForgeExtendNotesDurationResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeExtendNotesDurationOptions options)
    {
        var validTrackIndices = trackIndices
            .Where(index => index >= 0 && index < file.Tracks.Count && !file.Tracks[index].IsConductorTrack)
            .Distinct()
            .OrderByDescending(index => index)
            .ToArray();

        var maximumDurationTicks = Math.Max(0, options.MaximumDurationTicks);
        var barDurationTicks = MidiForgeNoteTiming.GetBarDurationTicks(file);
        var sourceTracks = 0;
        var createdTracks = 0;
        var changedNotes = 0;

        foreach (var trackIndex in validTrackIndices)
        {
            var track = file.Tracks[trackIndex];
            var sourceChunk = track.CloneCurrentChunk();
            var notes = sourceChunk.GetNotes()
                .OrderBy(note => note.Time)
                .ThenBy(note => (byte)note.NoteNumber)
                .ToArray();
            if (notes.Length == 0)
                continue;

            var changedNotesInTrack = 0;
            var extendedNotes = notes
                .Select(note =>
                {
                    var noteEndTime = note.Time + note.Length;
                    var nextNote = notes.FirstOrDefault(other => other.Time >= noteEndTime);
                    if (nextNote == null)
                        return MidiForgeNoteFactory.CloneWithLength(note, note.Length);

                    var newLength = nextNote.Time - note.Time;
                    if (options.RespectEmptyMeasures)
                        newLength = MidiForgeNoteTiming.LimitDurationToCurrentMeasureWhenNextMeasureIsEmpty(
                            note,
                            notes,
                            newLength,
                            barDurationTicks);

                    if (maximumDurationTicks > 0 && newLength > maximumDurationTicks)
                        newLength = maximumDurationTicks;

                    newLength = Math.Max(1, newLength);
                    if (newLength == note.Length)
                        return MidiForgeNoteFactory.CloneWithLength(note, note.Length);

                    changedNotesInTrack++;
                    return MidiForgeNoteFactory.CloneWithLength(note, newLength);
                })
                .ToArray();

            if (changedNotesInTrack == 0)
                continue;

            file.Tracks.Insert(trackIndex + 1, new EditableTrack(
                MidiForgeNoteFactory.CreateTrackFromNotes(sourceChunk, $"{track.DisplayName} (Extended)", extendedNotes),
                trackIndex + 1));
            sourceTracks++;
            createdTracks++;
            changedNotes += changedNotesInTrack;
        }

        if (createdTracks > 0)
            MidiForgeTrackMutation.RefreshTrackIndexesAndDirty(file);

        return new MidiForgeExtendNotesDurationResult(sourceTracks, createdTracks, changedNotes);
    }
}
