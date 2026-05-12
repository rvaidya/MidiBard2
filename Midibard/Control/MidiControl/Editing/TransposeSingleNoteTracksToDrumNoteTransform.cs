using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class TransposeSingleNoteTracksToDrumNoteTransform :
    IMidiEditorTransform<MidiForgeTransposeToDrumNoteOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } =
        new("drum.transpose-single-note-tracks", "Transpose Single-Note Tracks to Drum Note");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeTransposeToDrumNoteOptions options)
        => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeTransposeToDrumNoteOptions options)
    {
        var result = Apply(
            context.File,
            context.SelectedTrackIndices,
            options);

        return MidiForgeDrumTransformResult.Create(
            result.CreatedTracks > 0 || result.DeletedSourceTracks > 0,
            $"created {result.CreatedTracks} transposed drum track(s)");
    }

    public static MidiForgeTransposeToDrumNoteResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeTransposeToDrumNoteOptions options)
    {
        var validTrackIndices = trackIndices
            .Where(index => index >= 0 && index < file.Tracks.Count && !file.Tracks[index].IsConductorTrack)
            .Distinct()
            .OrderByDescending(index => index)
            .ToArray();

        var targetNote = Math.Clamp(options.TargetNote, 0, 127);
        var sourceTracks = validTrackIndices.Length;
        var createdTracks = 0;
        var deletedSourceTracks = 0;
        var skippedTracks = 0;

        foreach (var trackIndex in validTrackIndices)
        {
            var track = file.Tracks[trackIndex];
            var sourceChunk = track.CloneCurrentChunk();
            var notes = sourceChunk.GetNotes().ToArray();
            var uniqueNoteNumbers = notes
                .Select(note => (int)(byte)note.NoteNumber)
                .Distinct()
                .Take(2)
                .ToArray();

            if (uniqueNoteNumbers.Length != 1)
            {
                skippedTracks++;
                continue;
            }

            var transposeSemitones = targetNote - uniqueNoteNumbers[0];
            var trackName = string.IsNullOrWhiteSpace(options.TrackName)
                ? $"{track.DisplayName} (Transposed {transposeSemitones})"
                : options.TrackName.Trim();
            var transposedChunk = MidiForgeNoteFactory.CreateTrackFromNotes(
                sourceChunk,
                trackName,
                notes.Select(note => MidiForgeNoteFactory.CloneWithNumber(note, targetNote)));

            if (options.DeleteOriginalTracks)
            {
                track.Dispose();
                file.Tracks[trackIndex] = new EditableTrack(transposedChunk, trackIndex);
                deletedSourceTracks++;
            }
            else
            {
                file.Tracks.Insert(trackIndex + 1, new EditableTrack(transposedChunk, trackIndex + 1));
            }

            createdTracks++;
        }

        if (createdTracks > 0 || deletedSourceTracks > 0)
            MidiForgeTrackMutation.RefreshTrackIndexesAndDirty(file);

        return new MidiForgeTransposeToDrumNoteResult(
            sourceTracks,
            createdTracks,
            deletedSourceTracks,
            skippedTracks);
    }
}
