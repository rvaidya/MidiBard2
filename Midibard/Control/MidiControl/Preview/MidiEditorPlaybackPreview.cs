using System;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Multimedia;

namespace MidiBard.Control.MidiControl.Preview;

internal sealed unsafe partial class MidiEditorPlaybackPreview : IDisposable
{
    private const int SameOnsetRollStepMs = 35;
    private static readonly TimeSpan SameOnsetRollStep = TimeSpan.FromMilliseconds(SameOnsetRollStepMs);

    private readonly IMidiEditorPreviewSettings settings;
    private readonly IMidiEditorPreviewInstrumentCatalog instrumentCatalog;
    private readonly IMidiEditorPreviewSoundPlayer soundPlayer;
    private readonly IMidiEditorPreviewScheduler scheduler;
    private readonly MidiEditorPreviewReleasePolicy releasePolicy = new();
    private readonly MidiEditorPreviewCompensationPolicy compensationPolicy;
    // This is deliberately live rather than snapshotted: users can hide/show piano-roll
    // tracks during playback and preview should mute/resume those tracks immediately.
    private readonly Func<int, bool> trackVisibilityProvider;
    private readonly object playbackLock = new();
    private readonly List<PreviewProgramEvent> programEvents = new();
    private readonly List<EventSnapshot> eventSnapshots = new();
    private readonly List<PendingPreviewSchedule> pendingCompensatedEventSchedules = new();
    private readonly HashSet<uint> duplicateInstrumentDiagnosticsLogged = new();
    private Playback playback;
    private TrackPreviewState[] trackStates = Array.Empty<TrackPreviewState>();
    private TrackPlaybackState[] trackPlaybackStates = Array.Empty<TrackPlaybackState>();
    private long nextNoteSequence;
    private long compensatedEventScheduleVersion;
    private double durationSeconds;
    private bool hasEvents;

    public MidiEditorPlaybackPreview(Plugin plugin, Func<int, bool> trackVisibilityProvider = null)
        : this(
            new PluginMidiEditorPreviewSettings(plugin),
            new DefaultMidiEditorPreviewInstrumentCatalog(),
            new DalamudMidiEditorPreviewSoundPlayer(),
            trackVisibilityProvider,
            compensationProvider: new PluginMidiEditorPreviewCompensationProvider(plugin))
    {
    }

    internal MidiEditorPlaybackPreview(
        IMidiEditorPreviewSettings settings,
        IMidiEditorPreviewInstrumentCatalog instrumentCatalog,
        IMidiEditorPreviewSoundPlayer soundPlayer,
        Func<int, bool> trackVisibilityProvider = null,
        IMidiEditorPreviewScheduler? scheduler = null,
        IMidiEditorPreviewCompensationProvider? compensationProvider = null)
    {
        this.settings = settings;
        this.instrumentCatalog = instrumentCatalog;
        this.soundPlayer = soundPlayer;
        this.scheduler = scheduler ?? new TimerMidiEditorPreviewScheduler();
        compensationPolicy = new MidiEditorPreviewCompensationPolicy(
            compensationProvider ?? NoOpMidiEditorPreviewCompensationProvider.Instance);
        this.trackVisibilityProvider = trackVisibilityProvider ?? (_ => true);
    }

    public bool IsPlaying => playback?.IsRunning == true;
    public double PositionSeconds => GetPlaybackPositionSeconds();
    public double DurationSeconds => durationSeconds;
    public bool HasEvents => hasEvents;
    public string? StatusMessage { get; private set; }
    internal IReadOnlyList<EventSnapshot> EventSnapshots => eventSnapshots;
}
