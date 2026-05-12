using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Common;

namespace MidiBard.Control.MidiControl.Preview;

internal sealed unsafe partial class MidiEditorPlaybackPreview
{
    private void LogDuplicateInstrumentPreviewIfNeeded(int trackIndex, HeldNote note)
    {
        if (duplicateInstrumentDiagnosticsLogged.Contains(note.InstrumentId))
            return;

        var activeTrackLabels = new List<string>();
        for (var i = 0; i < trackPlaybackStates.Length; i++)
        {
            if (i == trackIndex || !IsTrackVisible(i))
                continue;

            var state = trackPlaybackStates[i];
            if (state.CurrentNote?.InstrumentId == note.InstrumentId ||
                state.HeldNotes.Any(held => held.InstrumentId == note.InstrumentId))
            {
                activeTrackLabels.Add(GetDiagnosticTrackLabel(i));
            }
        }

        if (activeTrackLabels.Count == 0)
            return;

        duplicateInstrumentDiagnosticsLogged.Add(note.InstrumentId);
        activeTrackLabels.Add(GetDiagnosticTrackLabel(trackIndex));
        DalamudApi.PluginLog.Verbose(
            $"[MidiEditorPreview] Multiple visible preview tracks are active with instrument {note.InstrumentId}: {string.Join(", ", activeTrackLabels)}. " +
            "If one part is inaudible, direct SCD playback may be voice-limited by the game sound manager for this sample.");
    }

    private string GetDiagnosticTrackLabel(int trackIndex)
    {
        if ((uint)trackIndex >= (uint)trackStates.Length)
            return $"#{trackIndex + 1}";

        var name = trackStates[trackIndex].TrackName;
        return string.IsNullOrWhiteSpace(name)
            ? $"#{trackIndex + 1}"
            : $"#{trackIndex + 1} {name}";
    }

    private uint? ResolveInstrumentForEvent(int trackIndex, TrackPreviewState trackState, int channel)
    {
        var baseInstrumentId = trackState.BaseInstrumentId;
        var hasBaseInstrument = baseInstrumentId is > 0;

        if (!hasBaseInstrument)
            return ResolveFallbackProgramInstrument(trackState, channel);

        if (!instrumentCatalog.IsGuitar(baseInstrumentId!.Value))
            return baseInstrumentId;

        if (TryResolveOverrideByTrackInstrument(trackIndex, baseInstrumentId.Value, out var overrideInstrumentId))
            return overrideInstrumentId;

        if ((uint)channel < 16 && trackState.GuitarToneChannelPrograms[channel] is { } program &&
            TryResolveGuitarProgramInstrument(program, out var guitarProgramInstrumentId))
            return guitarProgramInstrumentId;

        return baseInstrumentId;
    }

    private void ProcessProgramChange(int trackIndex, int channel, SevenBitNumber program)
    {
        if ((uint)trackIndex >= (uint)trackStates.Length || (uint)channel >= 16)
            return;

        var trackState = trackStates[trackIndex];
        trackState.FallbackChannelPrograms[channel] = program;

        switch (settings.GuitarToneMode)
        {
            case GuitarToneMode.Off:
                break;
            case GuitarToneMode.Standard:
                trackState.GuitarToneChannelPrograms[channel] = program;
                break;
            case GuitarToneMode.Simple:
                SetAllGuitarTonePrograms(trackState, program);
                break;
            case GuitarToneMode.OverrideByTrack:
                break;
            case GuitarToneMode.ProgramElectricGuitarMode:
                if (trackState.IsProgramElectricGuitar)
                    trackState.GuitarToneChannelPrograms[channel] = program;
                break;
            default:
                break;
        }
    }

    private static void SetAllGuitarTonePrograms(TrackPreviewState trackState, SevenBitNumber program)
    {
        for (var i = 0; i < trackState.GuitarToneChannelPrograms.Length; i++)
            trackState.GuitarToneChannelPrograms[i] = program;
    }

    private uint? ResolveFallbackProgramInstrument(TrackPreviewState trackState, int channel)
    {
        if ((uint)channel >= 16 || trackState.FallbackChannelPrograms[channel] is not { } program)
            return null;

        return instrumentCatalog.TryResolveProgramInstrument(program, out var instrumentId) ? instrumentId : null;
    }

    private bool TryResolveOverrideByTrackInstrument(int trackIndex, uint baseInstrumentId, out uint instrumentId)
    {
        instrumentId = 0;
        if (settings.GuitarToneMode != GuitarToneMode.OverrideByTrack || !instrumentCatalog.IsGuitar(baseInstrumentId))
            return false;

        if ((uint)trackIndex >= (uint)settings.TrackStatus.Length)
            return false;

        var tone = Math.Clamp(settings.TrackStatus[trackIndex].Tone, 0, 4);
        instrumentId = (uint)(24 + tone);
        return true;
    }

    private bool TryResolveGuitarProgramInstrument(SevenBitNumber program, out uint instrumentId)
    {
        if (instrumentCatalog.TryResolveProgramInstrument(program, out instrumentId) &&
            instrumentCatalog.IsGuitar(instrumentId))
            return true;

        instrumentId = 0;
        return false;
    }

    private void ResetProgramStates()
    {
        lock (playbackLock)
            ResetProgramStatesLocked();
    }

    private void ResetProgramStatesLocked()
    {
        foreach (var trackState in trackStates)
        {
            Array.Clear(trackState.FallbackChannelPrograms);
            Array.Clear(trackState.GuitarToneChannelPrograms);
        }
    }

    private void ApplyProgramStateAtLocked(double seconds)
    {
        foreach (var programEvent in programEvents)
        {
            if (programEvent.TimeSeconds > seconds)
                break;

            ProcessProgramChange(programEvent.TrackIndex, programEvent.Channel, programEvent.Program);
        }
    }
}
