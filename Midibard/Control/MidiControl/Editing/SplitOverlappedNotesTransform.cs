using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class SplitOverlappedNotesTransform : IMidiEditorTransform<MidiForgeSplitOverlappedNotesOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.split-overlapped-notes", "Split Overlapped Notes");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeSplitOverlappedNotesOptions options)
        => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeSplitOverlappedNotesOptions options)
    {
        var result = Apply(context.File, context.SelectedTrackIndices);

        return MidiForgeNoteTransformResult.CreatedTrackResult(
            result.CreatedTracks,
            $"created {result.CreatedTracks} overlap split track(s)");
    }

    public static MidiForgeSplitOverlappedNotesResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices)
    {
        var validTrackIndices = trackIndices
            .Where(index => index >= 0 && index < file.Tracks.Count && !file.Tracks[index].IsConductorTrack)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();

        var sourceTracks = 0;
        var createdTracks = 0;
        var overlapGroups = 0;
        var overlappedNotes = 0;
        var nonOverlappedNotes = 0;

        foreach (var trackIndex in validTrackIndices)
        {
            var track = file.Tracks[trackIndex];
            var sourceChunk = track.CloneCurrentChunk();
            var notes = sourceChunk.GetNotes().ToArray();
            if (notes.Length == 0)
                continue;

            var groups = notes
                .GroupBy(note => ((int)(byte)note.NoteNumber, note.Time))
                .OrderBy(group => group.Key.Time)
                .ThenBy(group => group.Key.Item1)
                .ToArray();
            var duplicateGroups = groups
                .Where(group => group.Count() >= 2)
                .ToArray();
            if (duplicateGroups.Length == 0)
                continue;

            var trackGroups = new Dictionary<string, List<Melanchall.DryWetMidi.Interaction.Note>>(StringComparer.Ordinal);
            foreach (var group in groups)
            {
                var groupNotes = group.ToArray();
                var isOverlapped = groupNotes.Length >= 2;

                for (int i = 0; i < groupNotes.Length; i++)
                {
                    var trackName = isOverlapped
                        ? $"{track.DisplayName} overlap ({i + 1})"
                        : $"{track.DisplayName} no overlap";

                    if (!trackGroups.TryGetValue(trackName, out var splitNotes))
                    {
                        splitNotes = new List<Melanchall.DryWetMidi.Interaction.Note>();
                        trackGroups.Add(trackName, splitNotes);
                    }

                    splitNotes.Add(MidiForgeNoteFactory.CloneWithLength(groupNotes[i], groupNotes[i].Length));
                }
            }

            foreach (var (trackName, splitNotes) in trackGroups
                .OrderBy(pair => pair.Key.Contains(" no overlap", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (splitNotes.Count == 0)
                    continue;

                file.Tracks.Add(new EditableTrack(
                    MidiForgeNoteFactory.CreateTrackFromNotes(sourceChunk, trackName, splitNotes),
                    file.Tracks.Count));
                createdTracks++;
            }

            sourceTracks++;
            overlapGroups += duplicateGroups.Length;
            overlappedNotes += duplicateGroups.Sum(group => group.Count());
            nonOverlappedNotes += groups.Where(group => group.Count() == 1).Sum(group => group.Count());
        }

        if (createdTracks > 0)
            MidiForgeTrackMutation.RefreshTrackIndexesAndDirty(file);

        return new MidiForgeSplitOverlappedNotesResult(
            sourceTracks,
            createdTracks,
            overlapGroups,
            overlappedNotes,
            nonOverlappedNotes);
    }
}
