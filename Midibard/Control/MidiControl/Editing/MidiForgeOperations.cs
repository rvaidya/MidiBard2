using System;
using System.Collections.Generic;
using System.Linq;

using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

public static partial class MidiForgeOperations
{
    internal static readonly HashSet<string> PreservedDrumTrackNames = MidiForgeTrackNameEditor.PreservedDrumTrackNames;

    public static MidiForgeAdaptToRangeResult AdaptTracksToPlayableRange(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeAdaptToRangeOptions options)
        => AdaptToRangeTransform.Apply(file, trackIndices, options);

    public static int AdaptMidiNoteToPlayableRange(int midiNote)
        => MidiForgePlayableRange.AdaptMidiNoteToPlayableRange(midiNote);

    public static MidiForgeSplitChordsResult SplitTracksChords(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeSplitChordsOptions options)
        => SplitChordsTransform.Apply(file, trackIndices, options);

    public static MidiForgeAutoEditResult AutoEditTracks(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeAutoEditOptions options)
        => AutoEditTransform.Apply(file, trackIndices, options);

    public static MidiForgeSplitDrumkitResult SplitDrumkitTracks(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeSplitDrumkitOptions options)
        => SplitDrumkitTransform.Apply(file, trackIndices, options);

    public static MidiForgeDisassembleDrumkitResult DisassembleDrumkitTracks(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeDisassembleDrumkitOptions options)
        => DisassembleDrumkitTransform.Apply(file, trackIndices, options);

    public static MidiForgeTransposeToDrumNoteResult TransposeSingleNoteTracksToDrumNote(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeTransposeToDrumNoteOptions options)
        => TransposeSingleNoteTracksToDrumNoteTransform.Apply(file, trackIndices, options);

    public static MidiForgeSplitNotesRangeResult SplitTracksByToneRange(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeSplitToneRangeOptions options)
        => SplitByToneRangeTransform.Apply(file, trackIndices, options);

    public static MidiForgeSplitNotesRangeResult SplitTracksByLengthRange(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeSplitLengthRangeOptions options)
        => SplitByLengthRangeTransform.Apply(file, trackIndices, options);

    public static MidiForgeSplitOverlappedNotesResult SplitTracksOverlappedNotes(
        EditableMidiFile file,
        IEnumerable<int> trackIndices)
        => SplitOverlappedNotesTransform.Apply(file, trackIndices);

    public static MidiForgeTrimOverlappedNotesResult TrimOverlappedSustainedNotes(
        EditableMidiFile file,
        IEnumerable<int> trackIndices)
        => TrimOverlappedSustainedNotesTransform.Apply(file, trackIndices);

    public static MidiForgeExtendNotesDurationResult ExtendNotesDuration(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeExtendNotesDurationOptions options)
        => ExtendNotesDurationTransform.Apply(file, trackIndices, options);

    public static MidiForgeSplitEqualNotesResult SplitTracksEqualNotes(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        int targetTrackIndex)
        => SplitEqualNotesTransform.Apply(file, trackIndices, targetTrackIndex);

    public static MidiForgeDifferenceTracksResult DifferenceTracks(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        int targetTrackIndex)
        => DifferenceTracksTransform.Apply(file, trackIndices, targetTrackIndex);

    public static MidiForgeSplitNotesIntoTracksResult SplitNotesIntoTracks(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeSplitNotesIntoTracksOptions options)
        => SplitNotesIntoTracksTransform.Apply(file, trackIndices, options);

    public static MidiForgeGeneratePitchBendNotesResult GeneratePitchBendNotes(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeGeneratePitchBendNotesOptions options)
        => GeneratePitchBendNotesTransform.Apply(file, trackIndices, options);

    public static MidiForgeChangeNoteLengthResult ChangeTrackNoteLengths(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeChangeNoteLengthOptions options)
        => ChangeNoteLengthsTransform.Apply(file, trackIndices, options);

    public static MidiForgeTrackNameResult FillEmptyTrackNames(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeTrackNameFillMode fillMode)
        => FillEmptyTrackNamesTransform.Apply(file, trackIndices, fillMode);

    public static MidiForgeTrackNameResult ClearTrackNames(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        bool preserveDrumInstrumentNames = true)
        => ClearTrackNamesTransform.Apply(file, trackIndices, preserveDrumInstrumentNames);

    public static MidiForgeSetTrackProgramResult SetTrackPrograms(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeSetTrackProgramOptions options)
        => SetProgramsTransform.Apply(file, trackIndices, options);

    internal static MidiForgeSplitNotesRangeResult SplitTracksByRange(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        Func<Note, bool> isInRange,
        Func<string, string> getInRangeTrackName,
        Func<string, string> getOutOfRangeTrackName)
        => MidiForgeRangeSplitter.SplitTracksByRange(
            file,
            trackIndices,
            isInRange,
            getInRangeTrackName,
            getOutOfRangeTrackName);

    internal static int[] GetValidComparisonTrackIndices(
        EditableMidiFile file,
        IEnumerable<int> trackIndices)
        => MidiForgeTrackMutation.GetValidComparisonTrackIndices(file, trackIndices);

    internal static int InsertDerivedTrackAfterTarget(
        EditableMidiFile file,
        int targetTrackIndex,
        TrackChunk sourceChunk,
        string trackName,
        IReadOnlyCollection<Note> notes)
        => MidiForgeTrackMutation.InsertDerivedTrackAfterTarget(file, targetTrackIndex, sourceChunk, trackName, notes);

    internal static void RefreshTrackIndexesAndDirty(EditableMidiFile file)
        => MidiForgeTrackMutation.RefreshTrackIndexesAndDirty(file);

    internal static bool IsEqualNoteAtStart(Note note, Note other)
        => MidiForgeNoteTiming.IsEqualNoteAtStart(note, other);

    internal static bool NotesOverlap(Note note, Note other)
        => MidiForgeNoteTiming.NotesOverlap(note, other);

    internal static long LimitDurationToCurrentMeasureWhenNextMeasureIsEmpty(
        Note note,
        IReadOnlyCollection<Note> trackNotes,
        long newLength,
        long barDurationTicks)
        => MidiForgeNoteTiming.LimitDurationToCurrentMeasureWhenNextMeasureIsEmpty(
            note,
            trackNotes,
            newLength,
            barDurationTicks);

    internal static long GetBarDurationTicks(EditableMidiFile file)
        => MidiForgeNoteTiming.GetBarDurationTicks(file);

    internal static int AdaptChunkNoteNumbers(TrackChunk chunk, int octaveShift)
        => MidiForgePlayableRange.AdaptChunkNoteNumbers(chunk, octaveShift);

    internal static void SetTrackName(TrackChunk chunk, string name)
        => MidiForgeTrackNameEditor.SetTrackName(chunk, name);

    internal static IEnumerable<SplitChordGroup> SplitChordNotes(
        IEnumerable<Note> notes,
        string trackName,
        MidiForgeChordSplitStrategy strategy,
        MidiForgeChordGroupMode groupMode,
        int minimumSimultaneousNotes)
        => MidiForgeChordPartitioner.SplitChordNotes(
            notes,
            trackName,
            strategy,
            groupMode,
            minimumSimultaneousNotes);

    internal static string GetSplitChordGroupTrackName(
        string trackName,
        int groupSize,
        int partOrder,
        bool isChord,
        MidiForgeChordGroupMode groupMode)
        => groupMode switch
        {
            _ when !isChord => $"{trackName} no chords",
            MidiForgeChordGroupMode.Group => $"{trackName} chords of {groupSize}",
            MidiForgeChordGroupMode.Individual => $"{trackName} chords of {groupSize} ({partOrder})",
            _ => $"{trackName} chords parts ({partOrder})",
        };

    internal static bool ShouldPickAutoEditGroup(
        SplitChordGroup group,
        int maxSimultaneousNotes,
        MidiForgeChordPickStrategy pickStrategy)
        => MidiForgeChordPartitioner.ShouldPickAutoEditGroup(group, maxSimultaneousNotes, pickStrategy);

    internal static Note[] AutoEditDrumNotes(
        Note[] notes,
        string trackName,
        ref int autoEditedTracks,
        ref int transposedNotes)
        => MidiForgeChordPartitioner.AutoEditDrumNotes(notes, trackName, ref autoEditedTracks, ref transposedNotes);

    internal static Note CloneNoteWithNumber(Note note, int noteNumber)
        => MidiForgeNoteFactory.CloneWithNumber(note, noteNumber);

    internal static Note CloneNoteWithLength(Note note, long length)
        => MidiForgeNoteFactory.CloneWithLength(note, length);

    internal static IEnumerable<Note> GeneratePitchBendNotesForNote(
        Note note,
        IReadOnlyList<TimedEvent> pitchBends)
        => MidiForgePitchBendNoteGenerator.GenerateForNote(note, pitchBends);

    internal static void AddPitchBendGeneratedNote(
        ICollection<Note> notes,
        Note sourceNote,
        int noteNumber,
        long startTick,
        long endTick)
    {
        var length = endTick - startTick;
        if (length <= 0) return;

        var note = MidiForgeNoteFactory.CloneWithNumber(sourceNote, noteNumber);
        note.Time = Math.Max(0, startTick);
        note.Length = length;
        notes.Add(note);
    }

    internal static int GetPitchBendSemitones(PitchBendEvent pitchBend)
        => MidiForgePitchBendNoteGenerator.GetPitchBendSemitones(pitchBend);

    internal static string GetMidiNoteName(int noteNumber)
        => MidiForgeNoteNames.GetMidiNoteName(noteNumber);

    internal static string GetDefaultTrackName(EditableTrack track, MidiForgeTrackNameFillMode fillMode, int fallbackIndex)
        => MidiForgeTrackNameEditor.GetDefaultTrackName(track, fillMode, fallbackIndex);

    internal static bool SetEditableTrackName(EditableTrack track, string name)
        => MidiForgeTrackNameEditor.SetEditableTrackName(track, name);

    internal static void MoveTracksToEnd(EditableMidiFile file, IEnumerable<EditableTrack> tracks)
        => MidiForgeTrackMutation.MoveTracksToEnd(file, tracks);

    internal static TrackChunk CreateTrackFromNotes(
        TrackChunk sourceChunk,
        string trackName,
        IEnumerable<Note> notes,
        bool includePitchBendEvents = true)
        => MidiForgeNoteFactory.CreateTrackFromNotes(sourceChunk, trackName, notes, includePitchBendEvents);
}
