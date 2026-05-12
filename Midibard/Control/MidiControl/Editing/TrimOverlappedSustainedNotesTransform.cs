using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class TrimOverlappedSustainedNotesTransform : IMidiEditorTransform<MidiForgeTrimOverlappedNotesOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } =
        new("forge.trim-overlapped-sustained-notes", "Trim Overlapped Sustained Notes");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeTrimOverlappedNotesOptions options)
        => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeTrimOverlappedNotesOptions options)
    {
        var result = Apply(context.File, context.SelectedTrackIndices);

        return MidiForgeNoteTransformResult.CreatedTrackResult(
            result.CreatedTracks,
            $"created {result.CreatedTracks} trimmed track(s)");
    }

    public static MidiForgeTrimOverlappedNotesResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices)
    {
        var validTrackIndices = trackIndices
            .Where(index => index >= 0 && index < file.Tracks.Count && !file.Tracks[index].IsConductorTrack)
            .Distinct()
            .OrderByDescending(index => index)
            .ToArray();

        var sourceTracks = 0;
        var createdTracks = 0;
        var changedNotes = 0;

        foreach (var trackIndex in validTrackIndices)
        {
            var track = file.Tracks[trackIndex];
            var sourceChunk = track.CloneCurrentChunk();
            var notes = sourceChunk.GetNotes().ToArray();
            if (notes.Length == 0)
                continue;

            var changedNotesInTrack = 0;
            var trimmedNotes = notes
                .Select(note =>
                {
                    var overlapStart = notes
                        .Where(other => other.Time != note.Time)
                        .Where(other => MidiForgeNoteTiming.NotesOverlap(note, other))
                        .Where(other => other.Time > note.Time)
                        .Select(other => other.Time)
                        .OrderBy(time => time)
                        .Cast<long?>()
                        .FirstOrDefault();

                    if (overlapStart == null)
                        return MidiForgeNoteFactory.CloneWithLength(note, note.Length);

                    var newLength = Math.Max(1, overlapStart.Value - note.Time);
                    if (newLength == note.Length)
                        return MidiForgeNoteFactory.CloneWithLength(note, note.Length);

                    changedNotesInTrack++;
                    return MidiForgeNoteFactory.CloneWithLength(note, newLength);
                })
                .ToArray();

            if (changedNotesInTrack == 0)
                continue;

            file.Tracks.Insert(trackIndex + 1, new EditableTrack(
                MidiForgeNoteFactory.CreateTrackFromNotes(sourceChunk, $"{track.DisplayName} (Trimmed)", trimmedNotes),
                trackIndex + 1));
            sourceTracks++;
            createdTracks++;
            changedNotes += changedNotesInTrack;
        }

        if (createdTracks > 0)
            MidiForgeTrackMutation.RefreshTrackIndexesAndDirty(file);

        return new MidiForgeTrimOverlappedNotesResult(sourceTracks, createdTracks, changedNotes);
    }
}
