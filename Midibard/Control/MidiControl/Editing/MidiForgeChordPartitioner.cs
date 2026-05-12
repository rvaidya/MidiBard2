using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

internal static class MidiForgeChordPartitioner
{
    public static IEnumerable<SplitChordGroup> SplitChordNotes(
        IEnumerable<Note> notes,
        string trackName,
        MidiForgeChordSplitStrategy strategy,
        MidiForgeChordGroupMode groupMode,
        int minimumSimultaneousNotes)
    {
        var splitGroups = new Dictionary<string, SplitChordGroup>();

        foreach (var group in notes
            .GroupBy(note => strategy == MidiForgeChordSplitStrategy.SameStartTickAndLength
                ? (note.Time, note.Length)
                : (note.Time, Length: 0))
            .OrderBy(group => group.Key.Time))
        {
            var groupNotes = group
                .OrderByDescending(note => (byte)note.NoteNumber)
                .ToArray();
            var groupSize = groupNotes.Length;
            var isChord = groupSize >= minimumSimultaneousNotes;

            for (int i = 0; i < groupNotes.Length; i++)
            {
                var partOrder = i + 1;
                var trackGroupName = GetSplitChordGroupTrackName(trackName, groupSize, partOrder, isChord, groupMode);
                if (!splitGroups.TryGetValue(trackGroupName, out var splitGroup))
                {
                    splitGroup = new SplitChordGroup(
                        trackGroupName,
                        isChord ? groupSize : 0,
                        isChord ? partOrder : 0,
                        isChord,
                        new List<Note>());
                    splitGroups.Add(trackGroupName, splitGroup);
                }

                splitGroup.Notes.Add(groupNotes[i]);
            }
        }

        return splitGroups.Values
            .OrderBy(group => group.GroupSize)
            .ThenBy(group => group.Order)
            .ThenBy(group => group.TrackName, StringComparer.Ordinal);
    }

    public static bool ShouldPickAutoEditGroup(
        SplitChordGroup group,
        int maxSimultaneousNotes,
        MidiForgeChordPickStrategy pickStrategy)
    {
        if (!group.IsChord || group.Order == 1)
            return true;

        if (maxSimultaneousNotes <= 1)
            return false;

        if (maxSimultaneousNotes == 2)
        {
            if (pickStrategy == MidiForgeChordPickStrategy.OddChords && group.GroupSize >= 3)
                return group.Order == 3;

            return group.Order == 2;
        }

        return group.Order is 2 or 3;
    }

    public static Note[] AutoEditDrumNotes(
        Note[] notes,
        string trackName,
        ref int autoEditedTracks,
        ref int transposedNotes)
    {
        var pickedNotes = SplitChordNotes(
            notes,
            trackName,
            MidiForgeChordSplitStrategy.SameStartTick,
            MidiForgeChordGroupMode.GroupMerged,
            2)
            .Where(group => !group.IsChord || group.Order == 1)
            .SelectMany(group => group.Notes)
            .ToArray();

        var changed = pickedNotes.Length != notes.Length;

        if (pickedNotes.Count(note => (byte)note.NoteNumber == MidiForgeAnalysis.PlayableLowestMidiNote) > pickedNotes.Length / 2)
        {
            pickedNotes = pickedNotes
                .Select(note => MidiForgeNoteFactory.CloneWithNumber(note, Math.Clamp((byte)note.NoteNumber + 4, 0, 127)))
                .ToArray();
            transposedNotes += pickedNotes.Length;
            changed = true;
        }

        if (changed)
            autoEditedTracks++;

        return pickedNotes;
    }

    private static string GetSplitChordGroupTrackName(
        string trackName,
        int groupSize,
        int partOrder,
        bool isChord,
        MidiForgeChordGroupMode groupMode)
    {
        if (!isChord)
            return $"{trackName} no chords";

        return groupMode switch
        {
            MidiForgeChordGroupMode.Group => $"{trackName} chords of {groupSize}",
            MidiForgeChordGroupMode.Individual => $"{trackName} chords of {groupSize} ({partOrder})",
            _ => $"{trackName} chords parts ({partOrder})",
        };
    }
}
