using System;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;

namespace MidiBard.Control.MidiControl.Preview;

internal sealed unsafe partial class MidiEditorPlaybackPreview
{
    public void Load(EditableMidiFile? file, bool preservePosition)
    {
        var oldPosition = preservePosition ? PositionSeconds : 0.0;
        StopAllSounds();
        DisposePlayback();
        programEvents.Clear();
        eventSnapshots.Clear();
        duplicateInstrumentDiagnosticsLogged.Clear();
        trackStates = Array.Empty<TrackPreviewState>();
        trackPlaybackStates = Array.Empty<TrackPlaybackState>();
        nextNoteSequence = 0;
        durationSeconds = 0.0;
        hasEvents = false;
        StatusMessage = null;

        if (file == null)
            return;

        BuildTrackStates(file);
        var playbackEvents = BuildPlaybackEvents(file, out var tempoMap);
        hasEvents = playbackEvents.Any(ev => ev.Event is NoteOnEvent noteOn && (byte)noteOn.Velocity > 0);
        if (!hasEvents)
            return;

        playback = CreatePlayback(playbackEvents, tempoMap);
        durationSeconds = GetDurationSeconds(playback);

        if (preservePosition)
            Seek(oldPosition);
    }

    public void Restart()
    {
        Seek(0.0);
        Play();
    }

    public void Play()
    {
        if (!HasEvents)
            return;

        if (playback == null)
            return;

        if (durationSeconds > 0 && PositionSeconds >= durationSeconds)
            Seek(0.0);

        playback.Speed = Math.Max(0.1, settings.PlaySpeed);
        playback.Start();
    }

    public void Pause()
    {
        if (playback?.IsRunning != true)
            return;

        playback.Stop();
        StopAllSounds();
    }

    public void Stop()
    {
        if (playback != null)
        {
            playback.Stop();
            playback.MoveToStart();
        }

        StopAllSounds();
        ResetProgramStates();
    }

    public void Seek(double seconds)
    {
        if (playback == null)
            return;

        var clampedSeconds = Math.Clamp(seconds, 0.0, Math.Max(durationSeconds, 0.0));
        var wasPlaying = playback.IsRunning;
        if (wasPlaying)
            playback.Stop();

        lock (playbackLock)
        {
            StopAllSoundsLocked(MidiEditorPreviewReleasePolicy.CleanupFadeMs);
            ResetProgramStatesLocked();
            ApplyProgramStateAtLocked(clampedSeconds);
        }

        playback.MoveToTime(ToMetricTimeSpan(clampedSeconds));

        if (wasPlaying)
            playback.Start();
    }

    public void Update()
    {
        if (playback == null || !playback.IsRunning)
            return;

        playback.Speed = Math.Max(0.1, settings.PlaySpeed);

        // Visibility changes are UI-only and do not generate MIDI events, so poll while playing.
        lock (playbackLock)
            RefreshAllTrackPlayback(MidiEditorPreviewReleasePolicy.MaximumDynamicReleaseFadeMs);
    }

    private double GetPlaybackPositionSeconds()
    {
        if (playback == null)
            return 0.0;

        try
        {
            return Math.Clamp(ToSeconds(playback.GetCurrentTime<MetricTimeSpan>()), 0.0, Math.Max(durationSeconds, 0.0));
        }
        catch (ObjectDisposedException)
        {
            return 0.0;
        }
    }

    private static double GetDurationSeconds(Playback playback)
        => Math.Max(0.0, ToSeconds(playback.GetDuration<MetricTimeSpan>()));

    private static double ToSeconds(MetricTimeSpan timeSpan)
        => timeSpan.TotalMicroseconds / 1_000_000.0;

    private static MetricTimeSpan ToMetricTimeSpan(double seconds)
        => new((long)(Math.Max(0.0, seconds) * 1_000_000.0));

    private void DisposePlayback()
    {
        if (playback == null)
            return;

        try
        {
            playback.Finished -= PlaybackFinished;
            playback.Stop();
            playback.Dispose();
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Verbose(e, "[MidiEditorPreview] Failed to dispose preview playback.");
        }
        finally
        {
            playback = null;
        }
    }

    public void Dispose()
    {
        StopAllSounds();
        DisposePlayback();
    }
}
