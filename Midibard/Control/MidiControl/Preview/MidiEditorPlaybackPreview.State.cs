using System;
using System.Collections.Generic;

using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;

namespace MidiBard.Control.MidiControl.Preview;

internal sealed unsafe partial class MidiEditorPlaybackPreview
{
    private readonly record struct PreviewPlaybackMetadata(
        int TrackIndex,
        long Time,
        double TimeSeconds,
        int EventValue,
        int? ResolvedGameNote = null,
        uint? ResolvedInstrumentId = null);

    private readonly record struct PreviewProgramEvent(double TimeSeconds, int TrackIndex, int Channel, SevenBitNumber Program);

    private readonly record struct HeldNote(int Channel, int MidiNote, int GameNote, uint InstrumentId, long OnsetTick, double OnsetSeconds, long Sequence);

    internal readonly record struct EventSnapshot(
        int TrackIndex,
        long Time,
        string EventType,
        int Channel,
        int EventValue,
        int? ProgramNumber = null);

    internal readonly record struct TrackSnapshot(
        int TrackIndex,
        int HeldNoteCount,
        int? CurrentMidiNote,
        int? CurrentGameNote,
        uint? CurrentInstrumentId,
        nint CurrentSound);

    private sealed class PreviewTimedEvent : TimedEvent, IMetadata
    {
        public PreviewTimedEvent(MidiEvent midiEvent, long time, PreviewPlaybackMetadata metadata)
            : base(midiEvent, time)
        {
            Metadata = metadata;
        }

        public object Metadata { get; set; }
    }

    private sealed class InternalPlayback : Playback
    {
        private readonly Func<MidiEvent, object, bool> tryPlayCallback;

        public InternalPlayback(
            IEnumerable<PreviewTimedEvent> timedObjects,
            TempoMap tempoMap,
            PlaybackSettings settings,
            Func<MidiEvent, object, bool> tryPlayCallback)
            : base(timedObjects, tempoMap, settings)
        {
            this.tryPlayCallback = tryPlayCallback;
        }

        protected override bool TryPlayEvent(MidiEvent midiEvent, object metadata)
            => tryPlayCallback(midiEvent, metadata);
    }

    private sealed class TrackPreviewState
    {
        public string TrackName { get; init; } = string.Empty;
        public int Transpose { get; init; }
        public uint? BaseInstrumentId { get; init; }
        public bool IsProgramElectricGuitar { get; init; }
        public SevenBitNumber?[] FallbackChannelPrograms { get; } = new SevenBitNumber?[16];
        public SevenBitNumber?[] GuitarToneChannelPrograms { get; } = new SevenBitNumber?[16];
    }

    private sealed class TrackPlaybackState
    {
        public List<HeldNote> HeldNotes { get; } = new();
        public List<HeldNote> SameOnsetRollQueue { get; } = new();
        public HeldNote? CurrentNote { get; set; }
        public nint CurrentSound { get; set; }
        public List<RetainedPreviewSound> SoundsForCleanup { get; } = new();
        public long? SameOnsetRollTick { get; set; }
        public double SameOnsetRollElapsedSeconds { get; set; }
        public IDisposable? SameOnsetRollSchedule { get; set; }
        public long SameOnsetRollVersion { get; set; }
    }

    private sealed class RetainedPreviewSound(nint sound)
    {
        public nint Sound { get; } = sound;
        public IDisposable? CleanupSchedule { get; set; }
        public bool Stopped { get; set; }
    }

    private sealed class PendingPreviewSchedule : IDisposable
    {
        public IDisposable? Schedule { get; set; }
        public bool Cancelled { get; private set; }

        public void Dispose()
        {
            Cancelled = true;
            Schedule?.Dispose();
        }
    }
}
