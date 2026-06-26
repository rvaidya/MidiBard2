using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using Melanchall.DryWetMidi.Interaction;

using MidiBard.Control.MidiControl.Editing;
using MidiBard.Control.MidiControl.Editing.Commands.Track;
using MidiBard.Extensions.Dalamud;
using MidiBard.Util;

namespace MidiBard;

public partial class MidiEditorWindow
{
    private const float TrackInstrumentIconScale = 1.25f;
    private const string TrackNameAccentInset = "  ";

    private void DrawTrackListPanel()
    {
        var available = ImGui.GetContentRegionAvail();
        using var child = ImRaii.Child("##TrackListChild", available, false);
        if (!child) return;

        var frameH = ImGui.GetFrameHeight();
        var scale = ImGuiHelpers.GlobalScale;
        var fixedNR = ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize;
        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV
                       | ImGuiTableFlags.PadOuterX | ImGuiTableFlags.SizingFixedFit
                       | ImGuiTableFlags.ScrollY;

        {
            var tableAvailable = ImGui.GetContentRegionAvail();
            using var table = ImRaii.Table("##TrackTable", 3, tableFlags, tableAvailable);
            if (!table) return;

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("##chk", fixedNR, frameH);
            ImGui.TableSetupColumn("##color", fixedNR, 20f * scale);
            ImGui.TableSetupColumn("Track", ImGuiTableColumnFlags.WidthStretch);

            // Manual header row with global checkbox in the ##chk column
            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(ImGuiCol.TableHeaderBg));

            ImGui.TableNextColumn();
            if (ImGui.Checkbox("##GlobTrackChk", ref _globalTracksChecked))
            {
                if (_globalTracksChecked) SelectAllTracks();
                else ClearTrackSelection();
            }
            ImGuiUtil.ToolTip(MidiEditorOperationHelp.TrackSelectAll);

            ImGui.TableNextColumn();

            ImGui.TableNextColumn();
            ImGui.Text("Name");
            ImGui.SameLine();
            if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.Eye, "##ToggleAllTracksVisibility", MidiEditorOperationHelp.ToggleAllTracksVisibility))
                ToggleAllTracksVisibility();

            // Batch action bar
            using (ImRaii.Disabled(_selectedTrackIndices.Count == 0))
            {
                ImGui.SameLine();
                if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.Eraser, "##clearTrackSel", MidiEditorOperationHelp.TrackClearSelection))
                    ClearTrackSelection();

                ImGui.SameLine();
                if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, "##batchDelTracks",
                   MidiEditorOperationHelp.TrackDeleteSelected))
                {
                    if (ImGui.GetIO().KeyCtrl)
                        DeleteSelectedTracks();
                }
            }

            var tracks = _file!.Tracks;
            _pendingTrackActionOverlayIndex = -1;

            // Ensure display numbers are built before the loop
            if (_trackDisplayNumbers == null || _trackDisplayNumbers.Length != tracks.Count)
                RebuildTrackDisplayNumbers();

            var clipper = new ImGuiListClipper();
            // Workaround for ImGui table clipper calculating rows short when a frozen header is present. Scroll wont show all tracks
            var frozenHeaderPaddingRows = 5;
            var rowHeight = MathF.Max(
                ImGui.GetFrameHeightWithSpacing(),
                ImGui.GetFrameHeight() * TrackInstrumentIconScale + ImGui.GetStyle().CellPadding.Y * 2f);
            clipper.Begin(tracks.Count + frozenHeaderPaddingRows, rowHeight);
            while (clipper.Step())
            {
                for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    if (i >= tracks.Count) break;
                    DrawTrackEntry(tracks[i], i);
                }
            }
            clipper.End();
        }
    }

    private void DrawTrackEntry(EditableTrack track, int index)
    {
        ImGui.TableNextRow();
        ImGui.PushID(index);

        var isRowSelected = _selectedTrackIndex == index;
        bool isEditingThis = _editingTrack == track;
        bool anyEditing = _editingTrack != null;
        int trackCount = _file?.Tracks.Count ?? 1;
        var displayState = (_previewTracks != null && index < _previewTracks.Length) ? _previewTracks[index] : null;

        //  Checkbox column - skipped for conductor track and during inline edit
        ImGui.TableNextColumn();
        if (!track.IsConductorTrack && !isEditingThis)
        {
            bool isChecked = _selectedTrackIndices.Contains(index);
            if (ImGui.Checkbox("##trkChk", ref isChecked))
            {
                if (isChecked) _selectedTrackIndices.Add(index);
                else _selectedTrackIndices.Remove(index);
            }
        }

        //  Color column
        ImGui.TableNextColumn();
        if (displayState != null && !track.IsConductorTrack)
        {
            var autoColor = PianoRollWindow.GetTrackColor(index, trackCount);
            var trackColor = displayState.Color ?? autoColor;
            if (ImGui.ColorButton($"##prevcol{index}", trackColor, ImGuiColorEditFlags.NoTooltip,
                new Vector2(16f * ImGuiHelpers.GlobalScale, 16f * ImGuiHelpers.GlobalScale)))
            {
                ImGui.OpenPopup($"##prevColorPicker{index}");
            }
            if (ImGui.BeginPopup($"##prevColorPicker{index}"))
            {
                var pickerColor = displayState.Color ?? autoColor;
                if (ImGui.ColorPicker4($"##prevpicker{index}", ref pickerColor, ImGuiColorEditFlags.AlphaBar))
                    displayState.Color = pickerColor;
                if (displayState.Color.HasValue && ImGui.Button("Reset##prevColorReset"))
                    displayState.Color = null;
                ImGui.EndPopup();
            }
        }

        //  Track details + actions column
        ImGui.TableNextColumn();
        if (isEditingThis)
        {
            if (_editTrackFocusNext)
            {
                _trackNameAutocomplete.RequestOpen();
                _editTrackFocusNext = false;
            }
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            var iconDrawn = DrawResolvedTrackInstrumentIcon(track, index);
            if (iconDrawn)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            }
            bool confirmed = _trackNameAutocomplete.Draw(
                "##inlineTrackNameEdit",
                ref _editTrackName,
                GetTrackNameOptions(),
                i => i.DisplayName,
                i => i.IconId);
            if (confirmed)
            {
                SaveTrackName();
                ImGui.PopID();
                return;
            }
            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                _editingTrack = null;

            if (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter))
            {
                SaveTrackName();
                ImGui.PopID();
                return;
            }

            if (!track.IsConductorTrack)
            {
                if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.Check, "##saveTrackName", MidiEditorOperationHelp.TrackSaveName))
                    SaveTrackName();

                ImGui.SameLine();

                if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Times, "##cancelTrackName", MidiEditorOperationHelp.TrackCancelNameEdit))
                    _editingTrack = null;
            }
        }
        else
        {
            var detailsCellMin = ImGui.GetCursorScreenPos();
            var detailsCellWidth = ImGui.GetContentRegionAvail().X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 4f * ImGuiHelpers.GlobalScale);
            ImGui.AlignTextToFramePadding();
            var trackNumber = GetCachedTrackDisplayNumber(index);
            var numberStartX = ImGui.GetCursorPosX();
            var numberWidth = MathF.Max(
                ImGui.CalcTextSize("00").X,
                ImGui.CalcTextSize(trackNumber).X);
            using (ImRaii.PushColor(ImGuiCol.Text, Style.Components.TextDisabled))
                ImGui.TextUnformatted(trackNumber);
            ImGui.SameLine();
            ImGui.SetCursorPosX(numberStartX + numberWidth + ImGui.GetStyle().ItemInnerSpacing.X);

            var iconDrawn = DrawTrackNameInstrumentPicker(track, index, displayState);
            if (iconDrawn)
                ImGui.SameLine();

            var trackActionOverlayX = ImGui.GetCursorScreenPos().X;
            var trackNameStartX = ImGui.GetCursorPosX();
            var style = ImGui.GetStyle();
            var diagWidth = ImGui.GetFrameHeight();
            var channelWidth = track.IsConductorTrack
                ? 0f
                : ImGui.CalcTextSize(FormatTrackChannelLabel(9)).X + style.FramePadding.X * 2f;
            var trailingWidth = track.IsConductorTrack
                ? 0f
                : diagWidth + style.ItemSpacing.X + channelWidth;
            var nameWidth = MathF.Max(
                40f * ImGuiHelpers.GlobalScale,
                ImGui.GetContentRegionAvail().X - trailingWidth);
            using (ImRaii.PushColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered, isRowSelected)
               .Push(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered, isRowSelected)
               .Push(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered, isRowSelected)
               .Push(ImGuiCol.Text, Style.Components.Text, !track.IsConductorTrack)
               .Push(ImGuiCol.Text, Style.Colors.Blue, track.IsConductorTrack))
            {
                if (ImGui.Selectable($"{TrackNameAccentInset}{track.DisplayName}##DndTrack_{index}", isRowSelected,
                    ImGuiSelectableFlags.None, new Vector2(nameWidth, 0)))
                    SelectTrack(index);
            }
            var trackNameHovered = ImGui.IsItemHovered();

            if (displayState != null && !track.IsConductorTrack)
            {
                var autoColor = PianoRollWindow.GetTrackColor(index, trackCount);
                var accentColor = displayState.Color ?? autoColor;
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                ImGui.GetWindowDrawList().AddRectFilled(
                    min, new Vector2(min.X + 3f * ImGuiHelpers.GlobalScale, max.Y),
                    ImGui.ColorConvertFloat4ToU32(accentColor));
            }

            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                _pendingContextMenuTrackIndex = index;
                _pendingTrackActionOverlayIndex = -1;
                ClearTrackActionOverlay(index);
            }

            var dragTooltipBlocked = _trackActionOverlayIndex == index
                || _pendingTrackActionOverlayIndex == index
                || _activeContextMenuTrackIndex == index
                || _pendingContextMenuTrackIndex == index;
            if (!track.IsConductorTrack && !anyEditing && trackNameHovered && !dragTooltipBlocked)
                ImGuiUtil.ToolTip(MidiEditorOperationHelp.TrackDragToReorder);

            if (!track.IsConductorTrack && !anyEditing && ImGui.BeginDragDropSource())
            {
                unsafe
                {
                    int from = index;
                    ImGui.SetDragDropPayload("DND_MIDI_TRACK",
                        new System.ReadOnlySpan<byte>(&from, sizeof(int)), ImGuiCond.None);
                }
                if (_trackActionOverlayIndex != index)
                    ImGui.Text($"Track {index + 1}: {track.DisplayName}");
                ImGui.EndDragDropSource();
            }

            if (!track.IsConductorTrack && !anyEditing)
            {
                using (ImRaii.PushColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget))
                {
                    if (ImGui.BeginDragDropTarget())
                    {
                        var payload = ImGui.AcceptDragDropPayload("DND_MIDI_TRACK");
                        if (!payload.IsNull && payload.IsDelivery())
                        {
                            unsafe
                            {
                                int fromIdx = *(int*)payload.Data;
                                if (fromIdx != index)
                                {
                                    var result = _editorCommandExecutor.Execute(
                                        new ReorderTrackCommand(),
                                        CreateEditorCommandContext(),
                                        new ReorderTrackOptions(fromIdx, index));
                                    if (result.Succeeded)
                                    {
                                        if (_selectedTrackIndex == fromIdx)
                                            _selectedTrackIndex = index;
                                        ApplyEditorCommandRefreshHints();
                                    }
                                }
                            }
                        }
                        ImGui.EndDragDropTarget();
                    }
                }
            }

            if (!track.IsConductorTrack)
            {
                ImGui.SameLine();
                var trailingStartX = trackNameStartX + nameWidth + style.ItemSpacing.X;
                ImGui.SetCursorPosX(trailingStartX);
                DrawTrackDiagnosticsIndicator(track);

                ImGui.SameLine();
                ImGui.SetCursorPosX(trailingStartX + diagWidth + style.ItemSpacing.X);
                DrawTrackChannelPicker(track, index, channelWidth);
            }

            if (!track.IsConductorTrack && displayState != null)
            {
                var overlaySize = GetTrackActionOverlaySize();
                var rowHeight = MathF.Max(ImGui.GetFrameHeight(), ImGui.GetFrameHeight() * TrackInstrumentIconScale);
                var overlayPos = new Vector2(
                    trackActionOverlayX,
                    detailsCellMin.Y + MathF.Max(0f, rowHeight - overlaySize.Y) * 0.5f);
                var activeOverlayHovered = _trackActionOverlayIndex >= 0
                    && _trackActionOverlayIndex != index
                    && ImGui.IsMouseHoveringRect(_trackActionOverlayMin, _trackActionOverlayMax, true);
                var overlayHovered = _trackActionOverlayIndex == index &&
                    ImGui.IsMouseHoveringRect(overlayPos, overlayPos + overlaySize, true);
                var detailsCellMax = new Vector2(
                    detailsCellMin.X + detailsCellWidth,
                    detailsCellMin.Y + rowHeight);
                var rowContentHovered = !activeOverlayHovered
                    && ImGui.IsMouseHoveringRect(detailsCellMin, detailsCellMax, true);
                var showActions = rowContentHovered || overlayHovered;
                if (showActions)
                {
                    _trackActionOverlayIndex = index;
                    _trackActionOverlayMin = overlayPos;
                    _trackActionOverlayMax = overlayPos + overlaySize;
                    _pendingTrackActionOverlayIndex = index;
                    _pendingTrackActionOverlayPos = overlayPos;
                    _pendingTrackActionOverlaySize = overlaySize;
                    _pendingTrackActionOverlayAnyEditing = anyEditing;
                }
                else if (_trackActionOverlayIndex == index)
                {
                    _trackActionOverlayIndex = -1;
                    _trackActionOverlayMin = Vector2.Zero;
                    _trackActionOverlayMax = Vector2.Zero;
                }
            }
        }

        ImGui.PopID();
    }

    private void DrawTrackChannelPicker(EditableTrack track, int index, float width)
    {
        string chPopupId = $"##chPop_{index}";
        if (ImGui.Selectable(
                $"{FormatTrackChannelLabel(track.Channel)}{chPopupId}",
                false,
                ImGuiSelectableFlags.None,
                new Vector2(width, 0f)))
            ImGui.OpenPopup(chPopupId);
        ImGuiUtil.ToolTip(MidiEditorOperationHelp.TrackChangeChannel);

        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1);
        if (!ImGui.BeginPopup(chPopupId))
            return;

        for (int c = 0; c < 16; c++)
        {
            var optionLabel = $"{FormatTrackChannelLabel(c)}{(c + 1 == 10 ? " (Drums)" : "")}##chOpt_{index}_{c}";
            if (ImGui.Selectable(optionLabel, track.Channel == c))
            {
                if (track.Channel != c)
                {
                    var result = _editorCommandExecutor.Execute(
                        new SetTrackChannelCommand(),
                        CreateEditorCommandContext(),
                        new SetTrackChannelOptions(index, c));
                    if (result.Succeeded)
                        ApplyEditorCommandRefreshHints();
                }
            }
            if (track.Channel == c) ImGui.SetItemDefaultFocus();
        }
        ImGui.EndPopup();
    }

    private static string FormatTrackChannelLabel(int zeroBasedChannel)
        => $"Ch {zeroBasedChannel + 1:00}";

    private void DrawTrackDiagnosticsIndicator(EditableTrack track)
    {
        if (track.IsConductorTrack) return;

        var analysis = GetTrackAnalysis(track);
        if (analysis == null) return;

        if (!_trackDiagnosticsStringsByIndex.TryGetValue(track.Index, out var strings))
            return;

        ImGui.AlignTextToFramePadding();
        ImGuiUtil.TextIcon(FontAwesomeIcon.InfoCircle, strings.Warnings.Count > 0 ? Style.Colors.Yellow : Style.Colors.Gray);
        ImGuiUtil.ToolTip(string.Join("\n", strings.TooltipLines));
    }

    private MidiForgeTrackAnalysis? GetTrackAnalysis(EditableTrack track)
    {
        if (_file == null) return null;

        if (!ReferenceEquals(_trackDiagnosticsFile, _file)
            || _trackDiagnosticsVersion != _file.Version
            || _trackDiagnosticsTrackCount != _file.Tracks.Count)
        {
            RefreshTrackDiagnosticsCache();
        }

        return _trackDiagnosticsByIndex.TryGetValue(track.Index, out var diagnostics)
            ? diagnostics
            : null;
    }

    private void RefreshTrackDiagnosticsCache()
    {
        if (_file == null)
        {
            _trackDiagnosticsFile = null;
            _trackDiagnosticsVersion = -1;
            _trackDiagnosticsTrackCount = -1;
            _trackDiagnosticsByIndex = new Dictionary<int, MidiForgeTrackAnalysis>();
            _trackDiagnosticsStringsByIndex = new Dictionary<int, (IReadOnlyList<string>, IReadOnlyList<string>)>();
            return;
        }

        _trackDiagnosticsFile = _file;
        _trackDiagnosticsVersion = _file.Version;
        _trackDiagnosticsTrackCount = _file.Tracks.Count;
        RebuildTrackDisplayNumbers();

        var analysisDict = new Dictionary<int, MidiForgeTrackAnalysis>();
        var stringsDict = new Dictionary<int, (IReadOnlyList<string>, IReadOnlyList<string>)>();
        var mapProvider = CreateEditorMidiMapProvider();

        foreach (var track in _file.Tracks)
        {
            var notes = track.Chunk.GetNotes().ToArray();
            var analysis = MidiForgeAnalysis.AnalyzeTrack(track, notes);
            analysisDict[track.Index] = analysis;
            var warnings = MidiForgeAnalysis.GetTrackDiagnostics(analysis, mapProvider);
            var tooltipLines = MidiForgeAnalysis.GetTrackDiagnosticTooltipLines(analysis, mapProvider);
            stringsDict[track.Index] = (warnings, tooltipLines);
        }

        _trackDiagnosticsByIndex = analysisDict;
        _trackDiagnosticsStringsByIndex = stringsDict;
    }

    private bool DrawResolvedTrackInstrumentIcon(EditableTrack track, int index)
    {
        if (!TryResolveTrackInstrumentIcon(track, index, out var iconId, out var instrumentName))
            return false;

        var iconSize = ImGuiHelpers.ScaledVector2(ImGui.GetFrameHeight() * TrackInstrumentIconScale);
        DalamudApi.TextureProvider.DrawIcon(iconId, iconSize);
        if (ImGui.IsItemHovered())
            ImGuiUtil.ToolTip(BuildTrackInstrumentIconTooltip(instrumentName, includePickerHelp: false));

        return true;
    }

    private bool DrawTrackNameInstrumentPicker(EditableTrack track, int index, TrackDisplayState? displayState)
    {
        if (!TryResolveTrackInstrumentIcon(track, index, out var iconId, out var instrumentName))
            return false;

        _frameQuickPickerOptions ??= MidiEditorTrackNameOptions.GetQuickPickerOptions(GetTrackNameOptions());
        _framePickerItems ??= BuildTrackNamePickerItems(_frameQuickPickerOptions);
        var items = _framePickerItems;
        var popupId = $"##TrackNameInstrumentPopup_{index}";
        DrawTrackInstrumentIcon(
            iconId,
            BuildTrackInstrumentIconTooltip(instrumentName, includePickerHelp: items.Count > 0),
            displayState,
            popupId);

        if (items.Count == 0)
            return true;

        using var borderColor = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var popupBorder = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1)
            .Push(ImGuiStyleVar.ItemSpacing, ImGuiHelpers.ScaledVector2(ImGui.GetStyle().FramePadding.Y));
        using var popUp = ImRaii.Popup(popupId);
        if (popUp)
        {
            foreach (var item in items)
            {
                DalamudApi.TextureProvider.DrawIcon(item.IconId, ImGuiHelpers.ScaledVector2(40, 40));
                if (ImGui.IsItemClicked())
                {
                    var selectedIndex = (int)item.Value;
                    if ((uint)selectedIndex < (uint)_frameQuickPickerOptions.Count)
                        RenameTrackFromInstrumentPicker(index, _frameQuickPickerOptions[selectedIndex].DisplayName);
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.IsItemHovered() && !string.IsNullOrWhiteSpace(item.Tooltip))
                    ImGuiUtil.ToolTip(item.Tooltip);

                if (!item.BreakAfter)
                    ImGui.SameLine();
            }
        }

        return true;
    }

    private bool TryResolveTrackInstrumentIcon(
        EditableTrack track,
        int index,
        out uint iconId,
        out string instrumentName)
    {
        iconId = MidiEditorTrackNameOptions.DefaultIconId;
        instrumentName = string.Empty;

        if (track.IsConductorTrack ||
            InstrumentHelper.Instruments == null ||
            InstrumentHelper.Instruments.Length == 0)
        {
            return false;
        }

        var instrumentId = _playbackPreview.GetResolvedInstrumentIdForTrack(index, track.Channel);
        if (instrumentId == null ||
            instrumentId == 0 ||
            instrumentId.Value >= (uint)InstrumentHelper.Instruments.Length)
        {
            return true;
        }

        var instrument = InstrumentHelper.Instruments[(int)instrumentId.Value];
        iconId = instrument.IconId;
        instrumentName = instrument.FFXIVDisplayName;
        return true;
    }

    private static string BuildTrackInstrumentIconTooltip(string instrumentName, bool includePickerHelp)
    {
        var text = string.IsNullOrWhiteSpace(instrumentName)
            ? MidiEditorOperationHelp.TrackUnknownInstrument
            : instrumentName;

        return includePickerHelp
            ? $"{text}\n{MidiEditorOperationHelp.TrackPickInstrumentName}"
            : text;
    }

    private static IReadOnlyList<IconPickerItem> BuildTrackNamePickerItems(
        IReadOnlyList<MidiEditorTrackNameOption> options)
    {
        var items = new List<IconPickerItem>(options.Count);
        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var instrumentId = option.PickerInstrumentId.GetValueOrDefault();
            items.Add(new IconPickerItem(
                (uint)i,
                option.IconId,
                option.DisplayName,
                UiComponents.IsInstrumentGroupBreak(instrumentId)));
        }

        return items;
    }

    private void RenameTrackFromInstrumentPicker(int trackIndex, string trackName)
    {
        if (_file == null || (uint)trackIndex >= (uint)_file.Tracks.Count)
            return;

        if (string.Equals(_file.Tracks[trackIndex].Name, trackName, System.StringComparison.Ordinal))
            return;

        var result = _editorCommandExecutor.Execute(
            new RenameTrackCommand(),
            CreateEditorCommandContext(),
            new RenameTrackOptions(trackIndex, trackName));
        if (result.Succeeded)
            ApplyEditorCommandRefreshHints();
    }

    internal static string GetTrackDisplayNumber(IReadOnlyList<EditableTrack> tracks, int index)
    {
        if ((uint)index >= (uint)tracks.Count)
            return "--";

        if (tracks[index].IsConductorTrack)
            return "00";

        var playableIndex = 0;
        for (var i = 0; i <= index; i++)
        {
            if (!tracks[i].IsConductorTrack)
                playableIndex++;
        }

        return $"{playableIndex:00}";
    }

    private void RebuildTrackDisplayNumbers()
    {
        if (_file == null)
        {
            _trackDisplayNumbers = null;
            return;
        }

        var numbers = new string[_file.Tracks.Count];
        int playableIndex = 0;
        for (int i = 0; i < _file.Tracks.Count; i++)
        {
            if (_file.Tracks[i].IsConductorTrack)
                numbers[i] = "00";
            else
                numbers[i] = $"{++playableIndex:00}";
        }

        _trackDisplayNumbers = numbers;
    }

    private string GetCachedTrackDisplayNumber(int index)
    {
        if (_trackDisplayNumbers != null && (uint)index < (uint)_trackDisplayNumbers.Length)
            return _trackDisplayNumbers[index];
        return "--";
    }

    private void SaveTrackName()
    {
        if (_editingTrack == null) return;
        if (_editingTrack.Name == _editTrackName)
        {
            _editingTrack = null;
            return;
        }

        var result = _editorCommandExecutor.Execute(
            new RenameTrackCommand(),
            CreateEditorCommandContext(),
            new RenameTrackOptions(_editingTrack.Index, _editTrackName));
        if (result.Succeeded)
            ApplyEditorCommandRefreshHints();
        _editingTrack = null;
    }
}
