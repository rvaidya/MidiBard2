using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

using MidiBard.Control.MidiControl.Editing;

namespace MidiBard.Tests.Control.MidiControl.Editing;

public class MidiForgeHistoryTests
{
    [Fact]
    public void UndoRedo_RestoresTrackChunksAndDirtyState()
    {
        var file = CreateEditableFile(Note(60, 0, 120));
        var history = new MidiForgeHistory();

        history.Capture(file);
        file.TransposeTracks(new[] { 0 }, 12);

        file.Tracks[0].Chunk.GetNotes().Single().NoteNumber.ShouldBe((SevenBitNumber)72);
        file.IsDirty.ShouldBeTrue();

        history.Undo(file).ShouldBeTrue();
        file.Tracks[0].Chunk.GetNotes().Single().NoteNumber.ShouldBe((SevenBitNumber)60);
        file.IsDirty.ShouldBeFalse();
        history.CanRedo.ShouldBeTrue();

        history.Redo(file).ShouldBeTrue();
        file.Tracks[0].Chunk.GetNotes().Single().NoteNumber.ShouldBe((SevenBitNumber)72);
        file.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void Capture_PreservesLoadedEventManagerStateWithoutFlushing()
    {
        var file = CreateEditableFile(Note(60, 0, 120));
        var history = new MidiForgeHistory();
        var track = file.Tracks[0];
        track.LoadEvents(file.TempoMap);
        var noteEvent = track.Events!.Single(e => e.NoteOffSource != null);

        history.Capture(file);
        noteEvent.EditValue1 = 64;
        noteEvent.ApplyEditValues();
        file.MarkChanged();

        history.Undo(file).ShouldBeTrue();

        var restoredNote = file.Tracks[0].Chunk.GetNotes().Single();
        restoredNote.NoteNumber.ShouldBe((SevenBitNumber)60);
        restoredNote.Length.ShouldBe(120);
    }

    [Fact]
    public void Capture_ClearsRedoAfterNewMutation()
    {
        var file = CreateEditableFile(Note(60, 0, 120));
        var history = new MidiForgeHistory();

        history.Capture(file);
        file.TransposeTracks(new[] { 0 }, 12);
        history.Undo(file).ShouldBeTrue();
        history.CanRedo.ShouldBeTrue();

        history.Capture(file);
        file.TransposeTracks(new[] { 0 }, -12);

        history.CanRedo.ShouldBeFalse();
    }

    [Fact]
    public void PendingCapture_DoesNotCreateUndoEntryWhenFileDoesNotChange()
    {
        var file = CreateEditableFile(Note(60, 0, 120));
        var history = new MidiForgeHistory();

        var capture = history.BeginPendingCapture(file);

        history.CommitPendingCapture(file, capture).ShouldBeFalse();
        history.CanUndo.ShouldBeFalse();
        file.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void TransformExecutor_ValidationFailureDoesNotDirtyOrCaptureHistory()
    {
        var file = CreateEditableFile(Note(60, 0, 120));
        file.Tracks[0].Name = "Piano";
        var history = new MidiForgeHistory();
        var executor = new MidiEditorTransformExecutor(history);
        var beforeVersion = file.Version;

        var result = executor.Execute(
            new MidiEditorTransformContext(file, new[] { 0 }),
            MidiForgeTrackNameTransforms.FillEmpty,
            new MidiForgeFillEmptyTrackNamesTransformOptions(MidiForgeTrackNameFillMode.Midi));

        result.Succeeded.ShouldBeFalse();
        result.Changed.ShouldBeFalse();
        history.CanUndo.ShouldBeFalse();
        file.IsDirty.ShouldBeFalse();
        file.Version.ShouldBe(beforeVersion);
    }

    [Fact]
    public void TransformExecutor_SuccessCapturesOneUndoSnapshotAndMarksDirty()
    {
        var file = CreateEditableFile(Note(60, 0, 120));
        var history = new MidiForgeHistory();
        var executor = new MidiEditorTransformExecutor(history);
        var beforeVersion = file.Version;

        var result = executor.Execute(
            new MidiEditorTransformContext(file, new[] { 0 }),
            MidiForgeTrackNameTransforms.FillEmpty,
            new MidiForgeFillEmptyTrackNamesTransformOptions(MidiForgeTrackNameFillMode.Midi));

        result.Succeeded.ShouldBeTrue();
        result.Changed.ShouldBeTrue();
        result.Result.ClearTrackSelection.ShouldBeTrue();
        history.UndoCount.ShouldBe(1);
        file.IsDirty.ShouldBeTrue();
        file.Version.ShouldBeGreaterThan(beforeVersion);
        file.Tracks[0].Name.ShouldNotBeEmpty();

        history.Undo(file).ShouldBeTrue();
        file.Tracks[0].Name.ShouldBeEmpty();
        file.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void TransformExecutor_AdaptToRangeTransformReplacesSelectedTrackAndRequestsRefresh()
    {
        var file = CreateEditableFile(Note(96, 0, 120));
        var history = new MidiForgeHistory();
        var executor = new MidiEditorTransformExecutor(history);

        var result = executor.Execute(
            new MidiEditorTransformContext(file, new[] { 0 }, SelectedTrackIndex: 0, SelectedEventIndices: new[] { 0 }),
            MidiForgeArrangementTransforms.AdaptToRange,
            new MidiForgeAdaptToRangeOptions(CreateNewTracks: false, SmartTranspose: false));

        result.Succeeded.ShouldBeTrue();
        result.Changed.ShouldBeTrue();
        result.Result.ReloadSelectedTrack.ShouldBeTrue();
        result.Result.ClearEventSelection.ShouldBeTrue();
        result.Result.ClearTrackSelection.ShouldBeTrue();
        history.UndoCount.ShouldBe(1);
        file.Tracks.Count.ShouldBe(1);
        var adaptedNote = (int)(byte)file.Tracks[0].Chunk.GetNotes().Single().NoteNumber;
        adaptedNote.ShouldBeInRange(MidiForgeAnalysis.PlayableLowestMidiNote, MidiForgeAnalysis.PlayableHighestMidiNote);

        history.Undo(file).ShouldBeTrue();
        file.Tracks[0].Chunk.GetNotes().Single().NoteNumber.ShouldBe((SevenBitNumber)96);
        file.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void TransformExecutor_AutoEditTransformReplacesSelectedTrackAndRequestsRefresh()
    {
        var file = CreateEditableFile(
            Note(60, 0, 120),
            Note(64, 0, 120),
            Note(67, 0, 120));
        var history = new MidiForgeHistory();
        var executor = new MidiEditorTransformExecutor(history);

        var result = executor.Execute(
            new MidiEditorTransformContext(file, new[] { 0 }, SelectedTrackIndex: 0, SelectedEventIndices: new[] { 0 }),
            MidiForgeArrangementTransforms.AutoEdit,
            new MidiForgeAutoEditOptions(MaxSimultaneousNotes: 1, CreateNewTracks: false));

        result.Succeeded.ShouldBeTrue();
        result.Changed.ShouldBeTrue();
        result.Result.ReloadSelectedTrack.ShouldBeTrue();
        result.Result.ClearEventSelection.ShouldBeTrue();
        result.Result.ClearTrackSelection.ShouldBeTrue();
        history.UndoCount.ShouldBe(1);
        file.Tracks.Count.ShouldBe(1);
        file.Tracks[0].Chunk.GetNotes().Count().ShouldBe(1);
    }

    [Fact]
    public void TransformExecutor_SplitChordsTransformCreatesTracksAndClearsSelection()
    {
        var file = CreateEditableFile(
            Note(60, 0, 120),
            Note(64, 0, 120));
        var history = new MidiForgeHistory();
        var executor = new MidiEditorTransformExecutor(history);

        var result = executor.Execute(
            new MidiEditorTransformContext(file, new[] { 0 }, SelectedTrackIndex: 0),
            MidiForgeArrangementTransforms.SplitChords,
            new MidiForgeSplitChordsOptions(InsertPartsAtEnd: true));

        result.Succeeded.ShouldBeTrue();
        result.Changed.ShouldBeTrue();
        result.Result.ClearTrackSelection.ShouldBeTrue();
        result.Result.ReloadSelectedTrack.ShouldBeFalse();
        history.UndoCount.ShouldBe(1);
        file.Tracks.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void TransformExecutor_ChangeNoteLengthsTransformNoOpDoesNotCaptureHistory()
    {
        var file = CreateEditableFile(Note(60, 0, 120));
        var history = new MidiForgeHistory();
        var executor = new MidiEditorTransformExecutor(history);
        var beforeVersion = file.Version;

        var result = executor.Execute(
            new MidiEditorTransformContext(file, new[] { 0 }, SelectedTrackIndex: 0),
            MidiForgeTrackTransforms.ChangeNoteLengths,
            new MidiForgeChangeNoteLengthOptions(MinimumLengthTicks: 999, MaximumLengthTicks: 999, NewLengthTicks: 240));

        result.Succeeded.ShouldBeTrue();
        result.Changed.ShouldBeFalse();
        history.CanUndo.ShouldBeFalse();
        file.IsDirty.ShouldBeFalse();
        file.Version.ShouldBe(beforeVersion);
        file.Tracks[0].Chunk.GetNotes().Single().Length.ShouldBe(120);
    }

    [Fact]
    public void TransformExecutor_ChangeNoteLengthsTransformReplacesSelectedTrackAndRequestsRefresh()
    {
        var file = CreateEditableFile(Note(60, 0, 120));
        var history = new MidiForgeHistory();
        var executor = new MidiEditorTransformExecutor(history);

        var result = executor.Execute(
            new MidiEditorTransformContext(file, new[] { 0 }, SelectedTrackIndex: 0, SelectedEventIndices: new[] { 0 }),
            MidiForgeTrackTransforms.ChangeNoteLengths,
            new MidiForgeChangeNoteLengthOptions(
                MinimumLengthTicks: 0,
                MaximumLengthTicks: 200,
                NewLengthTicks: 240,
                DeleteOriginalTracks: true));

        result.Succeeded.ShouldBeTrue();
        result.Changed.ShouldBeTrue();
        result.Result.ReloadSelectedTrack.ShouldBeTrue();
        result.Result.ClearEventSelection.ShouldBeTrue();
        result.Result.ClearTrackSelection.ShouldBeTrue();
        history.UndoCount.ShouldBe(1);
        file.Tracks[0].Chunk.GetNotes().Single().Length.ShouldBe(240);
    }

    [Fact]
    public void TransformExecutor_SetProgramsTransformRequestsSelectedTrackRefresh()
    {
        var file = CreateEditableFile(Note(60, 0, 120));
        var history = new MidiForgeHistory();
        var executor = new MidiEditorTransformExecutor(history);

        var result = executor.Execute(
            new MidiEditorTransformContext(file, new[] { 0 }, SelectedTrackIndex: 0, SelectedEventIndices: new[] { 0 }),
            MidiForgeTrackTransforms.SetPrograms,
            new MidiForgeSetTrackProgramOptions(ProgramNumber: 24, RenameTracks: false));

        result.Succeeded.ShouldBeTrue();
        result.Changed.ShouldBeTrue();
        result.Result.ReloadSelectedTrack.ShouldBeTrue();
        result.Result.ClearEventSelection.ShouldBeTrue();
        result.Result.ClearTrackSelection.ShouldBeTrue();
        history.UndoCount.ShouldBe(1);
        file.Tracks[0].Chunk.Events.OfType<ProgramChangeEvent>().Single().ProgramNumber.ShouldBe((SevenBitNumber)24);
    }

    [Fact]
    public void TransformExecutor_SplitDrumkitTransformClearsSelectedTrackAndCapturesHistory()
    {
        var file = CreateEditableFile(Note(35, 0, 120, MidiForgeAnalysis.DrumChannel));
        var history = new MidiForgeHistory();
        var executor = new MidiEditorTransformExecutor(history);

        var result = executor.Execute(
            new MidiEditorTransformContext(file, new[] { 0 }, SelectedTrackIndex: 0, SelectedEventIndices: new[] { 0 }),
            MidiForgeDrumTransforms.SplitDrumkit,
            new MidiForgeSplitDrumkitOptions(AutoEditAfterSplit: false, CreateRestTrack: false));

        result.Succeeded.ShouldBeTrue();
        result.Changed.ShouldBeTrue();
        result.Result.ClearSelectedTrack.ShouldBeTrue();
        result.Result.ClearEventSelection.ShouldBeTrue();
        result.Result.ClearTrackSelection.ShouldBeTrue();
        history.UndoCount.ShouldBe(1);
        file.Tracks.Count.ShouldBe(2);

        history.Undo(file).ShouldBeTrue();
        file.Tracks.Count.ShouldBe(1);
        file.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void TransformExecutor_DisassembleDrumkitTransformClearsSelectedTrackAndCapturesHistory()
    {
        var file = CreateEditableFile(
            Note(35, 0, 120, MidiForgeAnalysis.DrumChannel),
            Note(38, 120, 120, MidiForgeAnalysis.DrumChannel));
        var history = new MidiForgeHistory();
        var executor = new MidiEditorTransformExecutor(history);

        var result = executor.Execute(
            new MidiEditorTransformContext(file, new[] { 0 }, SelectedTrackIndex: 0, SelectedEventIndices: new[] { 0 }),
            MidiForgeDrumTransforms.DisassembleDrumkit,
            new MidiForgeDisassembleDrumkitOptions(DeleteOriginalTracks: true));

        result.Succeeded.ShouldBeTrue();
        result.Changed.ShouldBeTrue();
        result.Result.ClearSelectedTrack.ShouldBeTrue();
        result.Result.ClearEventSelection.ShouldBeTrue();
        result.Result.ClearTrackSelection.ShouldBeTrue();
        history.UndoCount.ShouldBe(1);
        file.Tracks.Count.ShouldBe(2);
    }

    [Fact]
    public void TransformExecutor_TransposeSingleNoteDrumTransformClearsSelectedTrackAndCapturesHistory()
    {
        var file = CreateEditableFile(Note(60, 0, 120));
        var history = new MidiForgeHistory();
        var executor = new MidiEditorTransformExecutor(history);

        var result = executor.Execute(
            new MidiEditorTransformContext(file, new[] { 0 }, SelectedTrackIndex: 0, SelectedEventIndices: new[] { 0 }),
            MidiForgeDrumTransforms.TransposeSingleNoteTracksToDrumNote,
            new MidiForgeTransposeToDrumNoteOptions(TargetNote: 48, TrackName: "BassDrum", DeleteOriginalTracks: true));

        result.Succeeded.ShouldBeTrue();
        result.Changed.ShouldBeTrue();
        result.Result.ClearSelectedTrack.ShouldBeTrue();
        result.Result.ClearEventSelection.ShouldBeTrue();
        result.Result.ClearTrackSelection.ShouldBeTrue();
        history.UndoCount.ShouldBe(1);
        file.Tracks.Count.ShouldBe(1);
        file.Tracks[0].Name.ShouldBe("BassDrum");
        file.Tracks[0].Chunk.GetNotes().Single().NoteNumber.ShouldBe((SevenBitNumber)48);
    }

    [Fact]
    public void TransformExecutor_ComparisonTransformValidationFailureDoesNotCaptureHistory()
    {
        var file = CreateEditableFile(Note(60, 0, 120));
        var history = new MidiForgeHistory();
        var executor = new MidiEditorTransformExecutor(history);
        var beforeVersion = file.Version;

        var result = executor.Execute(
            new MidiEditorTransformContext(file, new[] { 0 }, SelectedTrackIndex: 0),
            MidiForgeNoteTransforms.SplitEqualNotes,
            new MidiForgeComparisonTrackOptions(TargetTrackIndex: 0));

        result.Succeeded.ShouldBeFalse();
        result.Changed.ShouldBeFalse();
        history.CanUndo.ShouldBeFalse();
        file.IsDirty.ShouldBeFalse();
        file.Version.ShouldBe(beforeVersion);
    }

    [Fact]
    public void TransformExecutor_SplitOverlappedTransformNoOpDoesNotCaptureHistory()
    {
        var file = CreateEditableFile(Note(60, 0, 120), Note(62, 120, 120));
        var history = new MidiForgeHistory();
        var executor = new MidiEditorTransformExecutor(history);
        var beforeVersion = file.Version;

        var result = executor.Execute(
            new MidiEditorTransformContext(file, new[] { 0 }, SelectedTrackIndex: 0),
            MidiForgeNoteTransforms.SplitOverlappedNotes,
            new MidiForgeSplitOverlappedNotesOptions());

        result.Succeeded.ShouldBeTrue();
        result.Changed.ShouldBeFalse();
        history.CanUndo.ShouldBeFalse();
        file.IsDirty.ShouldBeFalse();
        file.Version.ShouldBe(beforeVersion);
    }

    [Fact]
    public void TransformExecutor_GeneratePitchBendNotesTransformReplacesSelectedTrackAndRequestsRefresh()
    {
        var file = CreateEditableFileWithTrackEvents(
            Timed(new ProgramChangeEvent((SevenBitNumber)40) { Channel = (FourBitNumber)0 }, 0),
            Timed(new PitchBendEvent(12288) { Channel = (FourBitNumber)0 }, 120),
            Timed(new PitchBendEvent(8192) { Channel = (FourBitNumber)0 }, 360),
            Note(60, 0, 480));
        var history = new MidiForgeHistory();
        var executor = new MidiEditorTransformExecutor(history);

        var result = executor.Execute(
            new MidiEditorTransformContext(file, new[] { 0 }, SelectedTrackIndex: 0, SelectedEventIndices: new[] { 0 }),
            MidiForgeNoteTransforms.GeneratePitchBendNotes,
            new MidiForgeGeneratePitchBendNotesOptions(DeleteOriginalTracks: true));

        result.Succeeded.ShouldBeTrue();
        result.Changed.ShouldBeTrue();
        result.Result.ReloadSelectedTrack.ShouldBeTrue();
        result.Result.ClearEventSelection.ShouldBeTrue();
        result.Result.ClearTrackSelection.ShouldBeTrue();
        history.UndoCount.ShouldBe(1);
        file.Tracks.Count.ShouldBe(1);
        file.Tracks[0].Chunk.Events.OfType<PitchBendEvent>().ShouldBeEmpty();
        file.Tracks[0].Chunk.GetNotes().Count().ShouldBe(3);
    }

    private static EditableMidiFile CreateEditableFile(params Note[] notes)
    {
        var chunk = new TrackChunk();
        using (var manager = chunk.ManageTimedEvents())
        {
            foreach (var note in notes)
            {
                manager.Objects.Add(new TimedEvent(
                    new NoteOnEvent(note.NoteNumber, note.Velocity) { Channel = note.Channel },
                    note.Time));
                manager.Objects.Add(new TimedEvent(
                    new NoteOffEvent(note.NoteNumber, note.OffVelocity) { Channel = note.Channel },
                    note.EndTime));
            }
        }

        return new EditableMidiFile(new MidiFile(chunk));
    }

    private static EditableMidiFile CreateEditableFileWithTrackEvents(params object[] objects)
    {
        var chunk = new TrackChunk();
        using (var manager = chunk.ManageTimedEvents())
        {
            foreach (var item in objects)
            {
                switch (item)
                {
                    case TimedEvent timedEvent:
                        manager.Objects.Add(timedEvent);
                        break;
                    case Note note:
                        manager.Objects.Add(new TimedEvent(
                            new NoteOnEvent(note.NoteNumber, note.Velocity) { Channel = note.Channel },
                            note.Time));
                        manager.Objects.Add(new TimedEvent(
                            new NoteOffEvent(note.NoteNumber, note.OffVelocity) { Channel = note.Channel },
                            note.EndTime));
                        break;
                }
            }
        }

        return new EditableMidiFile(new MidiFile(chunk));
    }

    private static TimedEvent Timed(MidiEvent midiEvent, long time)
        => new(midiEvent, time);

    private static Note Note(int noteNumber, long time, long length, int channel = 0)
        => new(
            (SevenBitNumber)(byte)noteNumber,
            length,
            time)
        {
            Channel = (FourBitNumber)(byte)channel,
            Velocity = (SevenBitNumber)100,
            OffVelocity = (SevenBitNumber)0,
        };
}
