using System;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Tools;

using MidiBard.Control.MidiControl.Editing;

namespace MidiBard;

public partial class MidiEditorWindow
{

    //  Transpose Popup

    private void DrawTransposePopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##TransposeTracksPopup");
        if (!popup) return;
        if (_file == null) return;

        ImGui.Text("Transpose Selected Tracks");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(140f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("Semitones##transpSemi", ref _trackOperationState.TransposeSemitones, 12, 12);

        ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("Min note##transposeMinNote", ref _trackOperationState.TransposeMinNoteNumber);
        _trackOperationState.TransposeMinNoteNumber = Math.Clamp(_trackOperationState.TransposeMinNoteNumber, 0, 127);

        ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("Max note##transposeMaxNote", ref _trackOperationState.TransposeMaxNoteNumber);
        _trackOperationState.TransposeMaxNoteNumber = Math.Clamp(_trackOperationState.TransposeMaxNoteNumber, 0, 127);
        if (_trackOperationState.TransposeMinNoteNumber > _trackOperationState.TransposeMaxNoteNumber)
            (_trackOperationState.TransposeMinNoteNumber, _trackOperationState.TransposeMaxNoteNumber) = (_trackOperationState.TransposeMaxNoteNumber, _trackOperationState.TransposeMinNoteNumber);

        ImGui.Checkbox("Create transposed tracks (keep originals)##transposeCreateNew", ref _trackOperationState.TransposeCreateNewTracks);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGuiUtil.SuccessButton("Apply##doTranspose"))
        {
            var execution = ExecuteEditorTransform(
                MidiForgeTrackTransforms.TransposeTracks,
                new MidiForgeTransposeTracksOptions(
                    _trackOperationState.TransposeSemitones,
                    _trackOperationState.TransposeMinNoteNumber,
                    _trackOperationState.TransposeMaxNoteNumber,
                    _trackOperationState.TransposeCreateNewTracks));

            if (execution.Succeeded)
                ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelTranspose"))
            ImGui.CloseCurrentPopup();
    }

    //  Merge Popup

    private void DrawMergePopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##MergeTracksPopup");
        if (!popup) return;
        if (_file == null) return;

        ImGui.Text("Merge Selected Tracks");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Checkbox("Include Program Change events", ref _trackOperationState.MergeIncludePC);
        ImGui.Checkbox("Include Pitch Bend events", ref _trackOperationState.MergeIncludePB);
        ImGui.Checkbox("Include Control Change events", ref _trackOperationState.MergeIncludeCC);
        ImGui.Checkbox("Remove duplicate equal notes", ref _trackOperationState.MergeRemoveEqualNotes);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Removes duplicate notes with the same MIDI note number and start tick.");
        ImGui.Checkbox("Delete original tracks after merge", ref _trackOperationState.MergeDeleteOriginalTracks);
        ImGui.Spacing();
        ImGui.SetNextItemWidth(160f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("Note merge tolerance (ms)##mergeTolerance", ref _trackOperationState.MergeToleranceMs, 10, 100);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When > 0 overlapping or adjacent same-pitch notes are merged\ninto a single longer note using DryWetMidi's native merger.");
        _trackOperationState.MergeToleranceMs = Math.Max(0, _trackOperationState.MergeToleranceMs);

        ImGui.Spacing();
        ImGui.Text("Target track (merge INTO this track's clone):");

        var validIndices = _selectedTrackIndices
            .Where(i => i < _file.Tracks.Count && !_file.Tracks[i].IsConductorTrack)
            .OrderBy(i => i)
            .ToList();

        if (_trackOperationState.MergeTargetRelIdx >= validIndices.Count)
            _trackOperationState.MergeTargetRelIdx = 0;

        for (int r = 0; r < validIndices.Count; r++)
        {
            var track = _file.Tracks[validIndices[r]];
            bool sel = _trackOperationState.MergeTargetRelIdx == r;
            if (ImGui.RadioButton($"{track.DisplayName}##mergeTarget_{r}", sel))
                _trackOperationState.MergeTargetRelIdx = r;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        bool canMerge = validIndices.Count >= 2;
        using (ImRaii.Disabled(!canMerge))
        {
            if (ImGuiUtil.SuccessButton("Merge##doMerge"))
            {
                var targetIdx = validIndices[_trackOperationState.MergeTargetRelIdx];
                var execution = ExecuteEditorTransform(
                    MidiForgeTrackTransforms.MergeTracks,
                    new MidiForgeMergeTracksOptions(
                        targetIdx,
                        IncludeProgramChange: _trackOperationState.MergeIncludePC,
                        IncludePitchBend: _trackOperationState.MergeIncludePB,
                        IncludeControlChange: _trackOperationState.MergeIncludeCC,
                        ToleranceMs: _trackOperationState.MergeToleranceMs,
                        RemoveEqualNotes: _trackOperationState.MergeRemoveEqualNotes,
                        DeleteOriginalTracks: _trackOperationState.MergeDeleteOriginalTracks),
                    validIndices.ToArray());

                if (execution.Succeeded)
                    ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelMerge"))
            ImGui.CloseCurrentPopup();
    }

    //  Quantize Popup

    private static readonly string[] QuantizeStepLabels =
        { "1/4 Note", "1/8 Note", "1/16 Note", "1/32 Note", "1/64 Note" };

    private static readonly string[] QuantizeTargetLabels = { "Start", "End", "Start & End" };
    private static readonly QuantizerTarget[] QuantizeTargetValues =
        { QuantizerTarget.Start, QuantizerTarget.End, QuantizerTarget.Start | QuantizerTarget.End };

    private void DrawQuantizePopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##QuantizeTracksPopup");
        if (!popup) return;
        if (_file == null) return;

        ImGui.Text(_trackOperationState.QuantizeNotesOnly ? "Quantize Selected Notes" : "Quantize Selected Tracks");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(160f * ImGuiHelpers.GlobalScale);
        ImGui.Combo("Grid##quantStep", ref _trackOperationState.QuantizeStepIndex,
            QuantizeStepLabels, QuantizeStepLabels.Length);

        // Target: Start / End / Both
        int targetIdx = Array.IndexOf(QuantizeTargetValues, _trackOperationState.QuantizeTarget);
        if (targetIdx < 0) targetIdx = 0;
        ImGui.SetNextItemWidth(160f * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo("Target##quantTarget", ref targetIdx, QuantizeTargetLabels, QuantizeTargetLabels.Length))
            _trackOperationState.QuantizeTarget = QuantizeTargetValues[targetIdx];

        ImGui.SetNextItemWidth(160f * ImGuiHelpers.GlobalScale);
        ImGui.SliderFloat("Strength##quantLevel", ref _trackOperationState.QuantizeLevel, 0f, 1f, "%.2f");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("1.0 = fully snapped to grid, 0.5 = halfway, 0.0 = no change.");

        ImGui.Checkbox("Preserve note length##quantFixEnd", ref _trackOperationState.QuantizeFixOppositeEnd);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When quantizing Start, moves the NoteOff by the same delta so duration is preserved.");

        if (!_trackOperationState.QuantizeNotesOnly)
            ImGui.Checkbox("Create new quantized track (keep original)", ref _trackOperationState.QuantizeToNewTrack);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGuiUtil.SuccessButton("Apply##doQuantize"))
        {
            var grid = BuildQuantizeGrid();
            var settings = new QuantizingSettings
            {
                Target = _trackOperationState.QuantizeTarget,
                QuantizingLevel = _trackOperationState.QuantizeLevel,
                FixOppositeEnd = _trackOperationState.QuantizeFixOppositeEnd,
                QuantizingBeyondZeroPolicy = QuantizingBeyondZeroPolicy.FixAtZero,
                QuantizingBeyondFixedEndPolicy = QuantizingBeyondFixedEndPolicy.CollapseAndFix,
            };

            if (_trackOperationState.QuantizeNotesOnly)
            {
                var execution = ExecuteEditorTransform(
                    MidiForgeTrackTransforms.QuantizeSelectedNotes,
                    new MidiForgeQuantizeSelectedNotesOptions(
                        _selectedTrackIndex,
                        _selectedTrackIndex < 0
                            ? []
                            : MidiEditorSelectionKeys.FromSelectedEvents(CurrentEvents, _selectedEventIndices),
                        grid,
                        settings));

                if (execution.Succeeded)
                    ImGui.CloseCurrentPopup();
            }
            else
            {
                var execution = ExecuteEditorTransform(
                    MidiForgeTrackTransforms.QuantizeTracks,
                    new MidiForgeQuantizeTracksOptions(
                        grid,
                        settings,
                        _trackOperationState.QuantizeToNewTrack));

                if (execution.Succeeded)
                    ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelQuantize"))
            ImGui.CloseCurrentPopup();
    }

    private IGrid BuildQuantizeGrid()
    {
        ITimeSpan[] steps =
        {
            MusicalTimeSpan.Quarter,
            MusicalTimeSpan.Eighth,
            MusicalTimeSpan.Sixteenth,
            MusicalTimeSpan.ThirtySecond,
            MusicalTimeSpan.SixtyFourth,
        };
        return new SteppedGrid(steps[Math.Clamp(_trackOperationState.QuantizeStepIndex, 0, steps.Length - 1)]);
    }

    //  Change Note Length Popup

    private void DrawChangeNoteLengthPopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##ChangeNoteLengthPopup");
        if (!popup) return;
        if (_file == null) return;

        var validIndices = _selectedTrackIndices
            .Where(i => i < _file.Tracks.Count && !_file.Tracks[i].IsConductorTrack)
            .OrderBy(i => i)
            .ToArray();

        ImGui.Text("Change Selected Track Note Length");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(160f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("Min length ticks##changeLengthMin", ref _trackOperationState.ChangeNoteLengthMinTicks);
        _trackOperationState.ChangeNoteLengthMinTicks = Math.Max(0, _trackOperationState.ChangeNoteLengthMinTicks);

        ImGui.SetNextItemWidth(160f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("Max length ticks##changeLengthMax", ref _trackOperationState.ChangeNoteLengthMaxTicks);
        _trackOperationState.ChangeNoteLengthMaxTicks = Math.Max(0, _trackOperationState.ChangeNoteLengthMaxTicks);
        if (_trackOperationState.ChangeNoteLengthMinTicks > _trackOperationState.ChangeNoteLengthMaxTicks)
            (_trackOperationState.ChangeNoteLengthMinTicks, _trackOperationState.ChangeNoteLengthMaxTicks) = (_trackOperationState.ChangeNoteLengthMaxTicks, _trackOperationState.ChangeNoteLengthMinTicks);

        ImGui.SetNextItemWidth(160f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("New length ticks##changeLengthNew", ref _trackOperationState.ChangeNoteLengthNewTicks);
        _trackOperationState.ChangeNoteLengthNewTicks = Math.Max(1, _trackOperationState.ChangeNoteLengthNewTicks);

        if (ImGui.SmallButton("x2##changeLengthNewDouble"))
            _trackOperationState.ChangeNoteLengthNewTicks = Math.Max(1, _trackOperationState.ChangeNoteLengthNewTicks * 2);
        ImGui.SameLine();
        if (ImGui.SmallButton("/2##changeLengthNewHalf"))
            _trackOperationState.ChangeNoteLengthNewTicks = Math.Max(1, _trackOperationState.ChangeNoteLengthNewTicks / 2);

        ImGui.Checkbox("Delete original tracks after change length##changeLengthDeleteOriginal", ref _trackOperationState.ChangeNoteLengthDeleteOriginalTracks);

        ImGui.Spacing();
        ImGui.TextDisabled($"{validIndices.Length} selected performance track(s)");
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.Disabled(validIndices.Length == 0))
        {
            if (ImGuiUtil.SuccessButton("Apply##doChangeNoteLength"))
            {
                var execution = ExecuteEditorTransform(
                    MidiForgeTrackTransforms.ChangeNoteLengths,
                    new MidiForgeChangeNoteLengthOptions(
                        MinimumLengthTicks: _trackOperationState.ChangeNoteLengthMinTicks,
                        MaximumLengthTicks: _trackOperationState.ChangeNoteLengthMaxTicks,
                        NewLengthTicks: _trackOperationState.ChangeNoteLengthNewTicks,
                        DeleteOriginalTracks: _trackOperationState.ChangeNoteLengthDeleteOriginalTracks),
                    validIndices);

                if (execution.Succeeded)
                    ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelChangeNoteLength"))
            ImGui.CloseCurrentPopup();
    }

    //  Set Track Program Popup

    private void DrawSetTrackProgramPopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##SetTrackProgramPopup");
        if (!popup) return;
        if (_file == null) return;

        var validIndices = _selectedTrackIndices
            .Where(i => i < _file.Tracks.Count && !_file.Tracks[i].IsConductorTrack)
            .OrderBy(i => i)
            .ToArray();

        _trackOperationState.SetTrackProgramNumber = Math.Clamp(_trackOperationState.SetTrackProgramNumber, 0, 127);
        var preview = GmProgramComboItems[_trackOperationState.SetTrackProgramNumber];

        ImGui.Text("Set Selected Track MIDI Program");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(260f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("Program##setTrackProgramCombo", preview))
        {
            for (int i = 0; i < GmProgramComboItems.Length; i++)
            {
                var selected = i == _trackOperationState.SetTrackProgramNumber;
                if (ImGui.Selectable(GmProgramComboItems[i], selected))
                    _trackOperationState.SetTrackProgramNumber = i;
                if (selected) ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.Checkbox("Replace all existing Program Change events##setTrackProgramReplaceAll", ref _trackOperationState.SetTrackProgramReplaceAll);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When off, only the earliest Program Change event is updated. Tracks without one get a new event at tick 0.");

        ImGui.Checkbox("Rename tracks from selected program##setTrackProgramRename", ref _trackOperationState.SetTrackProgramRenameTracks);
        using (ImRaii.Disabled(!_trackOperationState.SetTrackProgramRenameTracks))
        {
            ImGui.RadioButton("FFXIV instrument name##setTrackProgramRenameFfxiv", ref _trackOperationState.SetTrackProgramRenameModeIndex, 0);
            ImGui.SameLine();
            ImGui.RadioButton("MIDI program name##setTrackProgramRenameMidi", ref _trackOperationState.SetTrackProgramRenameModeIndex, 1);
        }

        ImGui.Spacing();
        ImGui.TextDisabled($"{validIndices.Length} selected performance track(s)");
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.Disabled(validIndices.Length == 0))
        {
            if (ImGuiUtil.SuccessButton("Apply##doSetTrackProgram"))
            {
                var execution = ExecuteEditorTransform(
                    MidiForgeTrackTransforms.SetPrograms,
                    new MidiForgeSetTrackProgramOptions(
                        ProgramNumber: _trackOperationState.SetTrackProgramNumber,
                        ReplaceAllProgramChanges: _trackOperationState.SetTrackProgramReplaceAll,
                        RenameTracks: _trackOperationState.SetTrackProgramRenameTracks,
                        RenameMode: _trackOperationState.SetTrackProgramRenameModeIndex == 0
                            ? MidiForgeTrackNameFillMode.Ffxiv
                            : MidiForgeTrackNameFillMode.Midi),
                    validIndices);

                if (execution.Succeeded)
                    ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelSetTrackProgram"))
            ImGui.CloseCurrentPopup();
    }

    //  Merge Song Popup

    private void DrawMergeSongPopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##MergeSongPopup");
        if (!popup) return;
        if (_file == null) return;

        ImGui.Text("Merge Song");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextDisabled("How to place the imported file:");
        ImGui.RadioButton("Simultaneously (overlay tracks at time 0)##mergeSongSim", ref _mergeSongMode, 0);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("All tracks from both files start at time 0.\nUse when the two files play together (ensemble parts).");
        ImGui.RadioButton("Sequentially (append after this file)##mergeSongSeq", ref _mergeSongMode, 1);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("The imported file is placed after the current file ends.\nUse for medleys or song sections.");

        if (_mergeSongMode == 0)
        {
            ImGui.Spacing();
            ImGui.Checkbox("Ignore different tempo maps##mergeSongIgnoreTempo", ref _mergeSongIgnoreDifferentTempo);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("When enabled, uses this file's tempo map and ignores the imported file's tempo.\nRequired when the two files have different BPM/time signatures.");
            if (!_mergeSongIgnoreDifferentTempo)
            {
                using (ImRaii.PushColor(ImGuiCol.Text, Style.Colors.Orange))
                    ImGui.TextWrapped("Warning: both files must share an identical tempo map or an error will occur.");
            }
        }

        if (_mergeSongMode == 1)
        {
            ImGui.Spacing();
            ImGui.SetNextItemWidth(130f * ImGuiHelpers.GlobalScale);
            ImGui.InputInt("Delay between files (ms)##mergeSongDelay", ref _mergeSongDelayMs, 100, 1000);
            _mergeSongDelayMs = Math.Max(0, _mergeSongDelayMs);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGuiUtil.SuccessButton("Open File...##mergeSongOpen"))
        {
            _mergeSongSequential = _mergeSongMode == 1;
            ImGui.CloseCurrentPopup();
            OpenMergeSongDialog();
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelMergeSong"))
            ImGui.CloseCurrentPopup();
    }

    //  Sanitize Popup

    private void DrawSanitizePopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##SanitizePopup");
        if (!popup) return;
        if (_file == null) return;

        ImGui.Text("Sanitize MIDI File");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Checkbox("Remove duplicated notes##sanDuplNotes", ref _sanitizeRemoveDuplNotes);
        ImGui.Checkbox("Remove empty track chunks##sanEmptyTracks", ref _sanitizeRemoveEmptyTracks);
        ImGui.Checkbox("Remove orphaned Note Off events##sanOrphanOff", ref _sanitizeRemoveOrphanedNoteOff);

        ImGui.Spacing();
        string[] orphanOnLabels = { "Remove", "Ignore", "Complete note (use max length)" };
        int onPolicyIdx = (int)_sanitizeOrphanedNoteOnPolicy;
        ImGui.SetNextItemWidth(240f * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo("Orphaned Note On##sanOrphanOn", ref onPolicyIdx, orphanOnLabels, orphanOnLabels.Length))
            _sanitizeOrphanedNoteOnPolicy = (OrphanedNoteOnEventsPolicy)onPolicyIdx;

        ImGui.Checkbox("Remove duplicate Set Tempo events##sanDuplTempo", ref _sanitizeRemoveDuplTempo);
        ImGui.Checkbox("Remove duplicate Time Signature events##sanDuplTimeSig", ref _sanitizeRemoveDuplTimeSig);
        ImGui.Checkbox("Trim silence at start##sanTrim", ref _sanitizeTrim);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGuiUtil.SuccessButton("Apply##doSanitize"))
        {
            var settings = new SanitizingSettings
            {
                RemoveDuplicatedNotes = _sanitizeRemoveDuplNotes,
                RemoveEmptyTrackChunks = _sanitizeRemoveEmptyTracks,
                RemoveOrphanedNoteOffEvents = _sanitizeRemoveOrphanedNoteOff,
                OrphanedNoteOnEventsPolicy = _sanitizeOrphanedNoteOnPolicy,
                RemoveDuplicatedSetTempoEvents = _sanitizeRemoveDuplTempo,
                RemoveDuplicatedTimeSignatureEvents = _sanitizeRemoveDuplTimeSig,
                Trim = _sanitizeTrim,
            };
            var execution = ExecuteEditorTransform(
                MidiForgeTrackTransforms.SanitizeFile,
                new MidiForgeSanitizeOptions(settings));

            if (execution.Succeeded)
                ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelSanitize"))
            ImGui.CloseCurrentPopup();
    }

    //  Transpose Notes Popup

    private void DrawTransposeNotesPopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##TransposeNotesPopup");
        if (!popup) return;

        ImGui.Text("Transpose Selected Notes");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(140f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("Semitones##transpNotesSemi", ref _transposeNotesSemitones, 12, 12);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGuiUtil.SuccessButton("Apply##doTransposeNotes"))
        {
            TransposeSelectedNotes(_transposeNotesSemitones);
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelTransposeNotes"))
            ImGui.CloseCurrentPopup();
    }
}
