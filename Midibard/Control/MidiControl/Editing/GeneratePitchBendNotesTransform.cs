using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class GeneratePitchBendNotesTransform : IMidiEditorTransform<MidiForgeGeneratePitchBendNotesOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } =
        new("forge.generate-pitch-bend-notes", "Generate Pitch-Bend Notes");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeGeneratePitchBendNotesOptions options)
        => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeGeneratePitchBendNotesOptions options)
    {
        var result = Apply(context.File, context.SelectedTrackIndices, options);
        var changed = result.CreatedTracks > 0 || result.ReplacedTracks > 0;
        var replacedSelectedTrack = options.DeleteOriginalTracks
            && MidiEditorTransformValidationHelpers.IncludesSelectedTrack(context);

        return new MidiEditorTransformResult(
            Changed: changed,
            Summary: $"generated {result.GeneratedNotes} pitch-bend note segment(s)",
            ClearTrackSelection: changed,
            ClearEventSelection: changed && replacedSelectedTrack,
            ReloadSelectedTrack: changed && replacedSelectedTrack);
    }

    public static MidiForgeGeneratePitchBendNotesResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeGeneratePitchBendNotesOptions options)
    {
        var validTrackIndices = trackIndices
            .Where(index => index >= 0 && index < file.Tracks.Count && !file.Tracks[index].IsConductorTrack)
            .Distinct()
            .OrderByDescending(index => index)
            .ToArray();

        var sourceTracks = 0;
        var createdTracks = 0;
        var replacedTracks = 0;
        var generatedNotes = 0;
        var skippedTracks = 0;

        foreach (var trackIndex in validTrackIndices)
        {
            var track = file.Tracks[trackIndex];
            var sourceChunk = track.CloneCurrentChunk();
            var notes = sourceChunk.GetNotes()
                .OrderBy(note => note.Time)
                .ThenBy(note => (byte)note.NoteNumber)
                .ToArray();
            var pitchBends = sourceChunk.GetTimedEvents()
                .Where(timedEvent => timedEvent.Event is PitchBendEvent)
                .OrderBy(timedEvent => timedEvent.Time)
                .ToArray();

            if (notes.Length == 0 || pitchBends.Length == 0)
            {
                skippedTracks++;
                continue;
            }

            var generatedTrackNotes = new List<Note>();
            foreach (var note in notes)
                generatedTrackNotes.AddRange(MidiForgePitchBendNoteGenerator.GenerateForNote(note, pitchBends));

            if (generatedTrackNotes.Count == 0)
            {
                skippedTracks++;
                continue;
            }

            var generatedTrack = new EditableTrack(
                MidiForgeNoteFactory.CreateTrackFromNotes(
                    sourceChunk,
                    $"{track.DisplayName} (Pitch Bend Notes)",
                    generatedTrackNotes,
                    includePitchBendEvents: false),
                0);

            sourceTracks++;
            generatedNotes += generatedTrackNotes.Count;

            if (options.DeleteOriginalTracks)
            {
                track.Dispose();
                file.Tracks[trackIndex] = generatedTrack;
                replacedTracks++;
            }
            else
            {
                file.Tracks.Insert(trackIndex + 1, generatedTrack);
                createdTracks++;
            }
        }

        if (createdTracks > 0 || replacedTracks > 0)
            MidiForgeTrackMutation.RefreshTrackIndexesAndDirty(file);

        return new MidiForgeGeneratePitchBendNotesResult(
            sourceTracks,
            createdTracks,
            replacedTracks,
            generatedNotes,
            skippedTracks);
    }
}
