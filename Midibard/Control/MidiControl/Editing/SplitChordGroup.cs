using System.Collections.Generic;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

internal sealed record SplitChordGroup(
    string TrackName,
    int GroupSize,
    int Order,
    bool IsChord,
    List<Note> Notes);
