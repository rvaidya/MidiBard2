using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MidiBard.Control.MidiControl.Editing;
using MidiBard.Control.MidiControl.Editing.Commands.Track;
using MidiBard.Control.MidiControl.Editing.State;
using MidiBard.Extensions.Dalamud;
using MidiBard.Util;

namespace MidiBard;

public partial class MidiEditorWindow
{
    private const string TrackActionOverlayPopupId = "##TrackActionOverlayPopup";
    private const string TrackContextMenuPopupId = "##TrackContextMenu";
    private const float TrackStatusBadgeSize = 11f;
    private const float TrackStatusBadgeGap = 2f;

    private int _trackActionOverlayIndex = -1;
    private Vector2 _trackActionOverlayMin;
    private Vector2 _trackActionOverlayMax;
    private int _pendingTrackActionOverlayIndex = -1;
    private Vector2 _pendingTrackActionOverlayPos;
    private Vector2 _pendingTrackActionOverlaySize;
    private bool _pendingTrackActionOverlayAnyEditing;

    private int _pendingContextMenuTrackIndex = -1;
    private int _activeContextMenuTrackIndex = -1;

    private void DrawPendingTrackActionOverlay()
    {
        if (_pendingTrackActionOverlayIndex < 0)
            return;

        var index = _pendingTrackActionOverlayIndex;
        _pendingTrackActionOverlayIndex = -1;

        if (_file == null
            || index >= _file.Tracks.Count
            || _previewTracks == null
            || index >= _previewTracks.Length)
        {
            ClearTrackActionOverlay(index);
            return;
        }

        var track = _file.Tracks[index];
        var displayState = _previewTracks[index];
        if (track.IsConductorTrack || displayState == null)
        {
            ClearTrackActionOverlay(index);
            return;
        }

        var overlayPos = ClampTrackPopupPosition(_pendingTrackActionOverlayPos, _pendingTrackActionOverlaySize);
        _trackActionOverlayMin = overlayPos;
        _trackActionOverlayMax = overlayPos + _pendingTrackActionOverlaySize;

        ImGui.OpenPopup(TrackActionOverlayPopupId);
        DrawTrackActionOverlay(
            track,
            index,
            displayState,
            overlayPos,
            _pendingTrackActionOverlaySize,
            _pendingTrackActionOverlayAnyEditing);
    }

    private void ClearTrackActionOverlay(int index)
    {
        if (_trackActionOverlayIndex != index)
            return;

        _trackActionOverlayIndex = -1;
        _trackActionOverlayMin = Vector2.Zero;
        _trackActionOverlayMax = Vector2.Zero;
    }

    private void DrawTrackActionOverlay(
        EditableTrack track,
        int index,
        TrackDisplayState displayState,
        Vector2 screenPos,
        Vector2 size,
        bool anyEditing)
    {
        ImGui.SetNextWindowPos(screenPos);
        ImGui.SetNextWindowSize(size);
        using var windowPadding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, ImGuiHelpers.ScaledVector2(4f, 2f))
            .Push(ImGuiStyleVar.WindowRounding, 4f * ImGuiHelpers.GlobalScale)
            .Push(ImGuiStyleVar.FramePadding, new Vector2(2f * ImGuiHelpers.GlobalScale, 0f));
        using var windowBg = ImRaii.PushColor(ImGuiCol.WindowBg, new Vector4(0f, 0f, 0f, 0.94f))
            .Push(ImGuiCol.Border, new Vector4(0f, 0f, 0f, 0f))
            .Push(ImGuiCol.Button, new Vector4(0f, 0f, 0f, 0f))
            .Push(ImGuiCol.ButtonHovered, Style.Components.FrameBgHovered with { W = 0.65f })
            .Push(ImGuiCol.ButtonActive, Style.Components.FrameBgActive with { W = 0.7f });

        var flags = ImGuiWindowFlags.NoDecoration
            | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse
            | ImGuiWindowFlags.NoFocusOnAppearing;
        if (ImGui.BeginPopup(TrackActionOverlayPopupId, flags))
        {
            var secondaryActionColor = Vector4.Lerp(
                Style.Components.TextDisabled,
                Style.Components.Text,
                0.75f);
            using var textColor = ImRaii.PushColor(ImGuiCol.Text, secondaryActionColor);

            if (displayState.IsLocked)
            {
                using (ImRaii.PushColor(ImGuiCol.Text, TrackLockBadgeIconColor()))
                {
                    if (ImGuiUtil.IconButton(FontAwesomeIcon.Lock, "##lockTrack", "Track locked (click to unlock)"))
                    {
                        displayState.IsLocked = false;
                        ClearTrackActionOverlay(index);
                        ImGui.CloseCurrentPopup();
                    }
                }
            }
            else if (ImGuiUtil.IconButton(FontAwesomeIcon.LockOpen, "##lockTrack", "Lock track (prevents note selection)"))
            {
                displayState.IsLocked = true;
                ClearTrackActionOverlay(index);
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();

            bool isVisible = displayState.Visible;
            var visibleIcon = isVisible ? FontAwesomeIcon.Eye : FontAwesomeIcon.EyeSlash;
            string visTooltip = isVisible
                ? MidiEditorOperationHelp.TrackVisibleInPianoRoll
                : MidiEditorOperationHelp.TrackHiddenInPianoRoll;
            if (ImGuiUtil.IconButton(visibleIcon, "##ShwHideTrack", visTooltip))
            {
                displayState.Visible = !displayState.Visible;
                RefreshPreviewVoiceLimits();
                ClearTrackActionOverlay(index);
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();

            using (ImRaii.Disabled(anyEditing))
            {
                if (ImGuiUtil.IconButton(FontAwesomeIcon.Edit, "##editTrack", MidiEditorOperationHelp.TrackEditName))
                {
                    _editingTrack = track;
                    _editTrackName = track.Name;
                    _editTrackFocusNext = true;
                    ClearTrackActionOverlay(index);
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();

                if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, "##delTrack", MidiEditorOperationHelp.TrackDelete))
                {
                    if (ImGui.GetIO().KeyCtrl)
                    {
                        var result = _editorCommandExecutor.Execute(
                            new DeleteTracksCommand(),
                            CreateEditorCommandContext(),
                            new DeleteTracksOptions(new[] { index }));
                        if (result.Succeeded)
                            ApplyEditorCommandRefreshHints();
                        ClearTrackActionOverlay(index);
                        ImGui.CloseCurrentPopup();
                        ImGui.EndPopup();
                        return;
                    }
                }
            }
            ImGui.EndPopup();
        }
    }

    private static Vector2 GetTrackActionOverlaySize()
    {
        var iconWidth = ImGuiUtil.GetIconButtonSize(FontAwesomeIcon.Lock).X + ImGui.GetStyle().FramePadding.X * 2f;
        var width = iconWidth * 4f + ImGui.GetStyle().ItemSpacing.X * 3f + 8f * ImGuiHelpers.GlobalScale;
        var height = ImGui.GetFrameHeight() + 4f * ImGuiHelpers.GlobalScale;
        return new Vector2(width, height);
    }

    private static Vector2 ClampTrackPopupPosition(Vector2 position, Vector2 size)
    {
        var viewport = ImGui.GetMainViewport();
        var padding = ImGuiHelpers.ScaledVector2(4f);
        var min = viewport.WorkPos + padding;
        var max = viewport.WorkPos + viewport.WorkSize - size - padding;

        if (max.X < min.X)
            max.X = min.X;
        if (max.Y < min.Y)
            max.Y = min.Y;

        return new Vector2(
            Math.Clamp(position.X, min.X, max.X),
            Math.Clamp(position.Y, min.Y, max.Y));
    }

    internal void DrawPendingTrackContextMenu()
    {
        if (_file == null)
        {
            _pendingContextMenuTrackIndex = -1;
            _activeContextMenuTrackIndex = -1;
            return;
        }

        if (_pendingContextMenuTrackIndex >= 0)
        {
            _activeContextMenuTrackIndex = _pendingContextMenuTrackIndex;
            _pendingContextMenuTrackIndex = -1;

            ImGui.SetNextWindowPos(ImGui.GetMousePos(), ImGuiCond.Appearing);
            ImGui.OpenPopup(TrackContextMenuPopupId);
        }

        if (_activeContextMenuTrackIndex < 0)
            return;

        if (_activeContextMenuTrackIndex >= _file.Tracks.Count)
        {
            _activeContextMenuTrackIndex = -1;
            return;
        }

        var track = _file.Tracks[_activeContextMenuTrackIndex];
        if (!DrawTrackContextMenu(track, _activeContextMenuTrackIndex))
            _activeContextMenuTrackIndex = -1;
    }

    private bool DrawTrackContextMenu(EditableTrack track, int index)
    {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f);
        using var popup = ImRaii.Popup(TrackContextMenuPopupId);
        if (!popup)
            return false;

        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonInfoNormal)
            .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonInfoNormal)
            .Push(ImGuiCol.ButtonActive, Style.Components.ButtonInfoNormal))
        {
            ImGui.Button(track.DisplayName, new Vector2(-1, 0));
        }

        ImGui.Separator();

        if (ImGui.MenuItem("Add Blank Track After", default, false, !track.IsConductorTrack))
            AddBlankTrackAfter(index);

        if (ImGui.MenuItem("Clone Track", default, false, !track.IsConductorTrack))
        {
            var result = _editorCommandExecutor.Execute(
                new CloneTracksCommand(),
                CreateEditorCommandContext(),
                new CloneTracksOptions(new[] { index }));
            if (result.Succeeded)
                ApplyEditorCommandRefreshHints();
        }

        if (ImGui.MenuItem("Split by Channel", default, false, track.HasMultipleChannels))
        {
            var result = _editorCommandExecutor.Execute(
                new SplitTrackByChannelCommand(),
                CreateEditorCommandContext(),
                new SplitTrackByChannelOptions(index));
            if (result.Succeeded)
                ApplyEditorCommandRefreshHints();
        }

        if (ImGui.MenuItem("Transpose Up 1 Octave", default, false, !track.IsConductorTrack))
            TransposeTrackFromContextMenu(index, 12);

        if (ImGui.MenuItem("Transpose Down 1 Octave", default, false, !track.IsConductorTrack))
            TransposeTrackFromContextMenu(index, -12);

        var displayState = (_previewTracks != null && index < _previewTracks.Length) ? _previewTracks[index] : null;

        if (displayState == null || track.IsConductorTrack)
            return true;

        bool isLocked = displayState.IsLocked;
        var lockText = isLocked ? "Unlock Track" : "Lock Track";
        if (ImGui.MenuItem(lockText))
            displayState.IsLocked = !isLocked;

        bool useTrackNameTranspose = displayState.UseTrackNameTranspose;
        if (ImGui.Checkbox($"Track Name Transpose##TrackNameTranspose_{index}", ref useTrackNameTranspose))
        {
            displayState.UseTrackNameTranspose = useTrackNameTranspose;
        }

        bool useAutoAdapt = displayState.UseAutoAdapt;
        if (ImGui.Checkbox($"Auto Adapt to C3-C6##AutoAdapt_{index}", ref useAutoAdapt))
        {
            displayState.UseAutoAdapt = useAutoAdapt;
        }

        return true;
    }

    private void TransposeTrackFromContextMenu(int trackIndex, int semitones)
    {
        var result = _editorCommandExecutor.Execute(
            new TransposeTracksCommand(),
            CreateEditorCommandContext(),
            new TransposeTracksOptions(new[] { trackIndex }, semitones));

        if (result.Succeeded)
            ApplyEditorCommandRefreshHints();
    }

    internal static void DrawTrackInstrumentIcon(
        uint iconId,
        string tooltip,
        TrackDisplayState? displayState,
        string? popupId = null)
    {
        var iconSize = ImGuiHelpers.ScaledVector2(ImGui.GetFrameHeight() * TrackInstrumentIconScale);
        DalamudApi.TextureProvider.DrawIcon(iconId, iconSize);
        if (ImGui.IsItemHovered())
            ImGuiUtil.ToolTip(tooltip);

        if (!string.IsNullOrEmpty(popupId))
            ImGui.OpenPopupOnItemClick(popupId, ImGuiPopupFlags.MouseButtonLeft);

        if (displayState != null)
            DrawTrackStatusOverlays(displayState);
    }

    private static void DrawTrackStatusOverlays(TrackDisplayState displayState)
    {
        if (!displayState.IsLocked && displayState.Visible)
            return;

        var iconMin = ImGui.GetItemRectMin();
        var iconMax = ImGui.GetItemRectMax();
        var scale = ImGuiHelpers.GlobalScale;
        var badgeSize = TrackStatusBadgeSize * scale;
        var padding = 1f * scale;
        var badgeGap = TrackStatusBadgeGap * scale;
        var cursor = new Vector2(iconMax.X - badgeSize - padding, iconMin.Y + padding);

        if (displayState.IsLocked)
        {
            DrawTrackStatusBadge(cursor, badgeSize, FontAwesomeIcon.Lock, TrackLockBadgeIconColor());
            cursor.Y += badgeSize + badgeGap;
        }

        if (!displayState.Visible)
            DrawTrackStatusBadge(cursor, badgeSize, FontAwesomeIcon.EyeSlash, TrackHiddenBadgeIconColor());
    }

    private static void DrawTrackStatusBadge(
        Vector2 min,
        float size,
        FontAwesomeIcon icon,
        Vector4 iconColor)
    {
        var drawList = ImGui.GetWindowDrawList();
        var max = min + new Vector2(size, size);
        drawList.AddRectFilled(
            min,
            max,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.62f)),
            size * 0.25f);

        using var font = ImRaii.PushFont(UiBuilder.IconFont);
        var text = icon.ToIconString();
        var textSize = ImGui.CalcTextSize(text);
        var textPos = min + (new Vector2(size, size) - textSize) * 0.5f;
        drawList.AddText(textPos, ImGui.ColorConvertFloat4ToU32(iconColor), text);
    }

    private static Vector4 TrackLockBadgeIconColor()
        => Vector4.Lerp(Style.Components.TextDisabled, Style.Colors.Red, 0.72f) with { W = 0.9f };

    private static Vector4 TrackHiddenBadgeIconColor()
        => Style.Components.ButtonBlueActive with { W = 0.9f };
}
