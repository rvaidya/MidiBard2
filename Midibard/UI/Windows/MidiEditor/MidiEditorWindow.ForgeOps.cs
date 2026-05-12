using System.Linq;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

using MidiBard.Control.MidiControl.Editing;

namespace MidiBard;

public partial class MidiEditorWindow
{
    private static readonly string[] SplitChordStrategyLabels =
    {
        "Same start tick",
        "Same start tick and length",
    };

    private static readonly string[] SplitChordGroupModeLabels =
    {
        "Merge by chord part",
        "Individual by chord size and part",
        "Group whole chords by size",
    };

    private static readonly string[] AutoEditPickStrategyLabels =
    {
        "Highest chord lines",
        "Odd chord lines",
    };

    private void DrawAdaptToRangePopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##AdaptToRangePopup");
        if (!popup) return;
        if (_file == null) return;

        var validIndices = _selectedTrackIndices
            .Where(i => i < _file.Tracks.Count && !_file.Tracks[i].IsConductorTrack)
            .OrderBy(i => i)
            .ToArray();

        ImGui.Text("Adapt Selected Tracks to C3-C6");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Checkbox("Create adapted tracks (keep originals)##adaptCreateNew", ref _forgeOperationState.AdaptToRangeCreateNewTracks);
        ImGui.Checkbox("Smart octave shift before wrapping##adaptSmart", ref _forgeOperationState.AdaptToRangeSmartTranspose);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Applies a best octave shift first when it reduces out-of-range notes, then wraps remaining notes into C3-C6.");

        ImGui.Spacing();
        ImGui.TextDisabled($"{validIndices.Length} selected performance track(s)");
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.Disabled(validIndices.Length == 0))
        {
            if (ImGuiUtil.SuccessButton("Apply##doAdaptToRange"))
            {
                var execution = ExecuteEditorTransform(
                    MidiForgeArrangementTransforms.AdaptToRange,
                    new MidiForgeAdaptToRangeOptions(
                        CreateNewTracks: _forgeOperationState.AdaptToRangeCreateNewTracks,
                        SmartTranspose: _forgeOperationState.AdaptToRangeSmartTranspose),
                    validIndices);

                if (execution.Succeeded)
                    ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelAdaptToRange"))
            ImGui.CloseCurrentPopup();
    }

    private void DrawAutoEditPopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##AutoEditPopup");
        if (!popup) return;
        if (_file == null) return;

        var validIndices = _selectedTrackIndices
            .Where(i => i < _file.Tracks.Count && !_file.Tracks[i].IsConductorTrack)
            .OrderBy(i => i)
            .ToArray();

        ImGui.Text("Auto Edit");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(120f);
        ImGui.InputInt("Max simultaneous notes##autoEditMax", ref _forgeOperationState.AutoEditMaxSimultaneousNotes);
        _forgeOperationState.AutoEditMaxSimultaneousNotes = int.Clamp(_forgeOperationState.AutoEditMaxSimultaneousNotes, 1, 3);

        ImGui.SetNextItemWidth(240f);
        ImGui.Combo("Chord line strategy##autoEditStrategy", ref _forgeOperationState.AutoEditPickStrategyIndex,
            AutoEditPickStrategyLabels, AutoEditPickStrategyLabels.Length);

        ImGui.Checkbox("Adapt out-of-range notes to C3-C6##autoEditAdaptRange", ref _forgeOperationState.AutoEditAdaptOutOfRange);
        ImGui.Checkbox("Create edited tracks (keep originals)##autoEditCreateNew", ref _forgeOperationState.AutoEditCreateNewTracks);

        ImGui.Spacing();
        ImGui.TextDisabled($"{validIndices.Length} selected performance track(s)");
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.Disabled(validIndices.Length == 0))
        {
            if (ImGuiUtil.SuccessButton("Apply##doAutoEdit"))
            {
                var execution = ExecuteEditorTransform(
                    MidiForgeArrangementTransforms.AutoEdit,
                    new MidiForgeAutoEditOptions(
                        MaxSimultaneousNotes: _forgeOperationState.AutoEditMaxSimultaneousNotes,
                        PickStrategy: _forgeOperationState.AutoEditPickStrategyIndex == 1
                            ? MidiForgeChordPickStrategy.OddChords
                            : MidiForgeChordPickStrategy.HighestChords,
                        AdaptOutOfRangeNotes: _forgeOperationState.AutoEditAdaptOutOfRange,
                        CreateNewTracks: _forgeOperationState.AutoEditCreateNewTracks),
                    validIndices);

                if (execution.Succeeded)
                    ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelAutoEdit"))
            ImGui.CloseCurrentPopup();
    }

    private void DrawSplitChordsPopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##SplitChordsPopup");
        if (!popup) return;
        if (_file == null) return;

        var validIndices = _selectedTrackIndices
            .Where(i => i < _file.Tracks.Count && !_file.Tracks[i].IsConductorTrack)
            .OrderBy(i => i)
            .ToArray();

        ImGui.Text("Split Chords");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(240f);
        ImGui.Combo("Strategy##splitChordStrategy", ref _forgeOperationState.SplitChordsStrategyIndex,
            SplitChordStrategyLabels, SplitChordStrategyLabels.Length);

        ImGui.SetNextItemWidth(240f);
        ImGui.Combo("Group mode##splitChordGroupMode", ref _forgeOperationState.SplitChordsGroupModeIndex,
            SplitChordGroupModeLabels, SplitChordGroupModeLabels.Length);

        ImGui.SetNextItemWidth(120f);
        ImGui.InputInt("Minimum simultaneous notes##splitChordMin", ref _forgeOperationState.SplitChordsMinimumSimultaneousNotes);
        _forgeOperationState.SplitChordsMinimumSimultaneousNotes = int.Clamp(_forgeOperationState.SplitChordsMinimumSimultaneousNotes, 2, 10);

        ImGui.Checkbox("Insert split tracks at end##splitChordInsertEnd", ref _forgeOperationState.SplitChordsInsertPartsAtEnd);

        ImGui.Spacing();
        ImGui.TextDisabled($"{validIndices.Length} selected performance track(s)");
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.Disabled(validIndices.Length == 0))
        {
            if (ImGuiUtil.SuccessButton("Apply##doSplitChords"))
            {
                var execution = ExecuteEditorTransform(
                    MidiForgeArrangementTransforms.SplitChords,
                    new MidiForgeSplitChordsOptions(
                        Strategy: _forgeOperationState.SplitChordsStrategyIndex == 1
                            ? MidiForgeChordSplitStrategy.SameStartTickAndLength
                            : MidiForgeChordSplitStrategy.SameStartTick,
                        GroupMode: _forgeOperationState.SplitChordsGroupModeIndex switch
                        {
                            1 => MidiForgeChordGroupMode.Individual,
                            2 => MidiForgeChordGroupMode.Group,
                            _ => MidiForgeChordGroupMode.GroupMerged,
                        },
                        MinimumSimultaneousNotes: _forgeOperationState.SplitChordsMinimumSimultaneousNotes,
                        InsertPartsAtEnd: _forgeOperationState.SplitChordsInsertPartsAtEnd),
                    validIndices);

                if (execution.Succeeded)
                    ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelSplitChords"))
            ImGui.CloseCurrentPopup();
    }

    private void DrawSplitNotesByToneRangePopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##SplitNotesByToneRangePopup");
        if (!popup) return;
        if (_file == null) return;

        var validIndices = GetSelectedPerformanceTrackIndices();

        ImGui.Text("Split Notes by Tone Range");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(120f);
        ImGui.InputInt("Minimum note##splitToneMin", ref _forgeOperationState.SplitToneMinNote);
        _forgeOperationState.SplitToneMinNote = int.Clamp(_forgeOperationState.SplitToneMinNote, 0, 127);

        ImGui.SetNextItemWidth(120f);
        ImGui.InputInt("Maximum note##splitToneMax", ref _forgeOperationState.SplitToneMaxNote);
        _forgeOperationState.SplitToneMaxNote = int.Clamp(_forgeOperationState.SplitToneMaxNote, 0, 127);

        ImGui.Spacing();
        ImGui.TextDisabled($"{validIndices.Length} selected performance track(s)");
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.Disabled(validIndices.Length == 0))
        {
            if (ImGuiUtil.SuccessButton("Apply##doSplitNotesByToneRange"))
            {
                var execution = ExecuteEditorTransform(
                    MidiForgeNoteTransforms.SplitByToneRange,
                    new MidiForgeSplitToneRangeOptions(
                        MinimumNote: _forgeOperationState.SplitToneMinNote,
                        MaximumNote: _forgeOperationState.SplitToneMaxNote),
                    validIndices);

                if (execution.Succeeded)
                    ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelSplitNotesByToneRange"))
            ImGui.CloseCurrentPopup();
    }

    private void DrawSplitNotesByLengthRangePopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##SplitNotesByLengthRangePopup");
        if (!popup) return;
        if (_file == null) return;

        var validIndices = GetSelectedPerformanceTrackIndices();

        ImGui.Text("Split Notes by Length Range");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(120f);
        ImGui.InputInt("Minimum ticks##splitLengthMin", ref _forgeOperationState.SplitLengthMinTicks);
        if (_forgeOperationState.SplitLengthMinTicks < 0)
            _forgeOperationState.SplitLengthMinTicks = 0;

        ImGui.SetNextItemWidth(120f);
        ImGui.InputInt("Maximum ticks##splitLengthMax", ref _forgeOperationState.SplitLengthMaxTicks);
        if (_forgeOperationState.SplitLengthMaxTicks < 0)
            _forgeOperationState.SplitLengthMaxTicks = 0;

        ImGui.Spacing();
        ImGui.TextDisabled($"{validIndices.Length} selected performance track(s)");
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.Disabled(validIndices.Length == 0))
        {
            if (ImGuiUtil.SuccessButton("Apply##doSplitNotesByLengthRange"))
            {
                var execution = ExecuteEditorTransform(
                    MidiForgeNoteTransforms.SplitByLengthRange,
                    new MidiForgeSplitLengthRangeOptions(
                        MinimumLengthTicks: _forgeOperationState.SplitLengthMinTicks,
                        MaximumLengthTicks: _forgeOperationState.SplitLengthMaxTicks),
                    validIndices);

                if (execution.Succeeded)
                    ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelSplitNotesByLengthRange"))
            ImGui.CloseCurrentPopup();
    }

    private void SplitSelectedOverlappedNotes()
    {
        if (_file == null) return;

        var validIndices = GetSelectedPerformanceTrackIndices();
        if (validIndices.Length == 0) return;

        ExecuteEditorTransform(
            MidiForgeNoteTransforms.SplitOverlappedNotes,
            new MidiForgeSplitOverlappedNotesOptions(),
            validIndices);
    }

    private void TrimSelectedOverlappedSustainedNotes()
    {
        if (_file == null) return;

        var validIndices = GetSelectedPerformanceTrackIndices();
        if (validIndices.Length == 0) return;

        ExecuteEditorTransform(
            MidiForgeNoteTransforms.TrimOverlappedSustainedNotes,
            new MidiForgeTrimOverlappedNotesOptions(),
            validIndices);
    }

    private void DrawExtendNotesDurationPopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##ExtendNotesDurationPopup");
        if (!popup) return;
        if (_file == null) return;

        var validIndices = GetSelectedPerformanceTrackIndices();

        ImGui.Text("Extend Notes Duration");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(120f);
        ImGui.InputInt("Maximum duration ticks (0 = unlimited)##extendMaxDuration", ref _forgeOperationState.ExtendNotesMaximumDurationTicks);
        if (_forgeOperationState.ExtendNotesMaximumDurationTicks < 0)
            _forgeOperationState.ExtendNotesMaximumDurationTicks = 0;

        ImGui.Checkbox("Respect empty measures##extendRespectEmptyMeasures", ref _forgeOperationState.ExtendNotesRespectEmptyMeasures);

        ImGui.Spacing();
        ImGui.TextDisabled($"{validIndices.Length} selected performance track(s)");
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.Disabled(validIndices.Length == 0))
        {
            if (ImGuiUtil.SuccessButton("Apply##doExtendNotesDuration"))
            {
                var execution = ExecuteEditorTransform(
                    MidiForgeNoteTransforms.ExtendNotesDuration,
                    new MidiForgeExtendNotesDurationOptions(
                        MaximumDurationTicks: _forgeOperationState.ExtendNotesMaximumDurationTicks,
                        RespectEmptyMeasures: _forgeOperationState.ExtendNotesRespectEmptyMeasures),
                    validIndices);

                if (execution.Succeeded)
                    ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelExtendNotesDuration"))
            ImGui.CloseCurrentPopup();
    }

    private void DrawSplitEqualNotesPopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##SplitEqualNotesPopup");
        if (!popup) return;
        if (_file == null) return;

        var validIndices = GetSelectedPerformanceTrackIndices();

        ImGui.Text("Split Equal Notes");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Target track:");
        DrawTargetTrackRadioButtons(validIndices, ref _forgeOperationState.SplitEqualNotesTargetRelIdx, "splitEqualTarget");

        ImGui.Spacing();
        ImGui.TextDisabled($"{validIndices.Length} selected performance track(s)");
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.Disabled(validIndices.Length < 2))
        {
            if (ImGuiUtil.SuccessButton("Apply##doSplitEqualNotes"))
            {
                var targetIdx = validIndices[_forgeOperationState.SplitEqualNotesTargetRelIdx];
                var execution = ExecuteEditorTransform(
                    MidiForgeNoteTransforms.SplitEqualNotes,
                    new MidiForgeComparisonTrackOptions(targetIdx),
                    validIndices);

                if (execution.Succeeded)
                    ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelSplitEqualNotes"))
            ImGui.CloseCurrentPopup();
    }

    private void DrawDifferenceTracksPopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##DifferenceTracksPopup");
        if (!popup) return;
        if (_file == null) return;

        var validIndices = GetSelectedPerformanceTrackIndices();

        ImGui.Text("Difference Tracks");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Target track:");
        DrawTargetTrackRadioButtons(validIndices, ref _forgeOperationState.DifferenceTracksTargetRelIdx, "differenceTarget");

        ImGui.Spacing();
        ImGui.TextDisabled($"{validIndices.Length} selected performance track(s)");
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.Disabled(validIndices.Length < 2))
        {
            if (ImGuiUtil.SuccessButton("Apply##doDifferenceTracks"))
            {
                var targetIdx = validIndices[_forgeOperationState.DifferenceTracksTargetRelIdx];
                var execution = ExecuteEditorTransform(
                    MidiForgeNoteTransforms.DifferenceTracks,
                    new MidiForgeComparisonTrackOptions(targetIdx),
                    validIndices);

                if (execution.Succeeded)
                    ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelDifferenceTracks"))
            ImGui.CloseCurrentPopup();
    }

    private void DrawSplitNotesIntoTracksPopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##SplitNotesIntoTracksPopup");
        if (!popup) return;
        if (_file == null) return;

        var validIndices = GetSelectedPerformanceTrackIndices();

        ImGui.Text("Split Notes Into Tracks");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(120f);
        ImGui.InputInt("Number of tracks##splitIntoTracksCount", ref _forgeOperationState.SplitIntoTracksNumberOfTracks);
        _forgeOperationState.SplitIntoTracksNumberOfTracks = int.Clamp(_forgeOperationState.SplitIntoTracksNumberOfTracks, 1, 64);

        ImGui.SetNextItemWidth(120f);
        ImGui.InputInt("Every N notes##splitIntoTracksEvery", ref _forgeOperationState.SplitIntoTracksEveryNotesAmount);
        if (_forgeOperationState.SplitIntoTracksEveryNotesAmount < 1)
            _forgeOperationState.SplitIntoTracksEveryNotesAmount = 1;

        ImGui.Spacing();
        ImGui.TextDisabled($"{validIndices.Length} selected performance track(s)");
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.Disabled(validIndices.Length == 0))
        {
            if (ImGuiUtil.SuccessButton("Apply##doSplitNotesIntoTracks"))
            {
                var execution = ExecuteEditorTransform(
                    MidiForgeNoteTransforms.SplitNotesIntoTracks,
                    new MidiForgeSplitNotesIntoTracksOptions(
                        NumberOfTracks: _forgeOperationState.SplitIntoTracksNumberOfTracks,
                        EveryNotesAmount: _forgeOperationState.SplitIntoTracksEveryNotesAmount),
                    validIndices);

                if (execution.Succeeded)
                    ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelSplitNotesIntoTracks"))
            ImGui.CloseCurrentPopup();
    }

    private void DrawGeneratePitchBendNotesPopup()
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup("##GeneratePitchBendNotesPopup");
        if (!popup) return;
        if (_file == null) return;

        var validIndices = _selectedTrackIndices
            .Where(i => i < _file.Tracks.Count
                        && !_file.Tracks[i].IsConductorTrack
                        && MidiForgeAnalysis.AnalyzeTrack(_file.Tracks[i]).PitchBendCount > 0)
            .OrderBy(i => i)
            .ToArray();

        ImGui.Text("Generate Pitch-Bend Notes");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Checkbox("Delete original tracks after generation##generatePitchBendDeleteOriginal",
            ref _forgeOperationState.GeneratePitchBendDeleteOriginalTracks);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When off, generated note-segment tracks are inserted after the source tracks.");

        ImGui.TextWrapped("Pitch bend values are converted into note segments using BardForge's -2 to +2 semitone mapping. Generated tracks do not keep Pitch Bend events.");

        ImGui.Spacing();
        ImGui.TextDisabled($"{validIndices.Length} selected track(s) with pitch bend events");
        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.Disabled(validIndices.Length == 0))
        {
            if (ImGuiUtil.SuccessButton("Apply##doGeneratePitchBendNotes"))
            {
                var execution = ExecuteEditorTransform(
                    MidiForgeNoteTransforms.GeneratePitchBendNotes,
                    new MidiForgeGeneratePitchBendNotesOptions(
                        DeleteOriginalTracks: _forgeOperationState.GeneratePitchBendDeleteOriginalTracks),
                    validIndices);

                if (execution.Succeeded)
                    ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (ImGuiUtil.DangerButton("Cancel##cancelGeneratePitchBendNotes"))
            ImGui.CloseCurrentPopup();
    }

    private void DrawTargetTrackRadioButtons(int[] validIndices, ref int targetRelIdx, string idPrefix)
    {
        if (_file == null) return;

        if (targetRelIdx >= validIndices.Length)
            targetRelIdx = 0;

        for (int i = 0; i < validIndices.Length; i++)
        {
            var track = _file.Tracks[validIndices[i]];
            var selected = targetRelIdx == i;
            if (ImGui.RadioButton($"{track.DisplayName}##{idPrefix}_{i}", selected))
                targetRelIdx = i;
        }
    }
}
