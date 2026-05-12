using System.Threading;

using Melanchall.DryWetMidi.Tools;

using MidiBard.Control.MidiControl.Editing;

namespace MidiBard;

public partial class MidiEditorWindow
{
    private readonly ImportPopupState _importState = new();
    private readonly TrackOperationPopupState _trackOperationState = new();
    private readonly ForgeOperationPopupState _forgeOperationState = new();
    private readonly DrumOperationPopupState _drumOperationState = new();

    private sealed class ImportPopupState
    {
        public bool SplitTracksByChannel;
        public bool SortTracks;
        public bool OverwriteTrackNames;
        public bool RemoveNonLyricMetadata;
        public bool RemoveLyricsAndText;
        public bool RemoveSequencerSpecificEvents;
        public bool OptimizeChannels;
        public int TrimStartModeIndex;

        public string SourceUrl = string.Empty;
        public bool SourceImportInProgress;
        public bool SourceImportClosePopup;
        public string SourceImportStatus = string.Empty;
        public string SourceImportError = string.Empty;
        public CancellationTokenSource? SourceImportCancellation;
    }

    private sealed class TrackOperationPopupState
    {
        public int TransposeSemitones;
        public int TransposeMinNoteNumber;
        public int TransposeMaxNoteNumber = 127;
        public bool TransposeCreateNewTracks;

        public bool MergeIncludePC = true;
        public bool MergeIncludePB = true;
        public bool MergeIncludeCC = true;
        public bool MergeRemoveEqualNotes = true;
        public bool MergeDeleteOriginalTracks;
        public int MergeTargetRelIdx;
        public int MergeToleranceMs;

        public int QuantizeStepIndex = 2;
        public bool QuantizeToNewTrack;
        public QuantizerTarget QuantizeTarget = QuantizerTarget.Start;
        public float QuantizeLevel = 1.0f;
        public bool QuantizeFixOppositeEnd = true;
        public bool QuantizeNotesOnly;

        public int ChangeNoteLengthMinTicks;
        public int ChangeNoteLengthMaxTicks;
        public int ChangeNoteLengthNewTicks = 240;
        public bool ChangeNoteLengthDeleteOriginalTracks;

        public int SetTrackProgramNumber;
        public bool SetTrackProgramReplaceAll = true;
        public bool SetTrackProgramRenameTracks = true;
        public int SetTrackProgramRenameModeIndex;
    }

    private sealed class ForgeOperationPopupState
    {
        public bool AdaptToRangeCreateNewTracks = true;
        public bool AdaptToRangeSmartTranspose = true;

        public int SplitChordsStrategyIndex;
        public int SplitChordsGroupModeIndex;
        public int SplitChordsMinimumSimultaneousNotes = 2;
        public bool SplitChordsInsertPartsAtEnd = true;

        public int SplitToneMinNote = MidiForgeAnalysis.PlayableLowestMidiNote;
        public int SplitToneMaxNote = MidiForgeAnalysis.PlayableHighestMidiNote;
        public int SplitLengthMinTicks;
        public int SplitLengthMaxTicks;
        public int ExtendNotesMaximumDurationTicks;
        public bool ExtendNotesRespectEmptyMeasures = true;
        public int SplitEqualNotesTargetRelIdx;
        public int DifferenceTracksTargetRelIdx;
        public int SplitIntoTracksNumberOfTracks = 2;
        public int SplitIntoTracksEveryNotesAmount = 1;
        public bool GeneratePitchBendDeleteOriginalTracks;

        public int AutoEditMaxSimultaneousNotes = 1;
        public int AutoEditPickStrategyIndex;
        public bool AutoEditAdaptOutOfRange = true;
        public bool AutoEditCreateNewTracks = true;
    }

    private sealed class DrumOperationPopupState
    {
        public int SplitDrumkitTransposePresetIndex;
        public bool SplitDrumkitAutoEditAfterSplit = true;
        public bool SplitDrumkitCreateRestTrack = true;
        public bool SplitDrumkitMoveSourceTracksToEnd = true;

        public bool DisassembleDrumkitDeleteOriginalTracks;

        public int TransposeToDrumPresetIndex;
        public int TransposeToDrumTargetIndex;
        public string TransposeToDrumTrackName = "BassDrum";
        public bool TransposeToDrumDeleteOriginalTracks = true;
    }
}
