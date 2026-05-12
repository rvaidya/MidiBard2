using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace MidiBard.Control.MidiControl.Editing;

public sealed class SetProgramsTransform : IMidiEditorTransform<MidiForgeSetTrackProgramOptions>
{
    public MidiEditorTransformDescriptor Descriptor { get; } = new("track.set-programs", "Set Track Programs");

    public MidiEditorTransformValidation Validate(
        MidiEditorTransformContext context,
        MidiForgeSetTrackProgramOptions options)
        => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

    public MidiEditorTransformResult Execute(
        MidiEditorTransformContext context,
        MidiForgeSetTrackProgramOptions options)
    {
        var result = Apply(context.File, context.SelectedTrackIndices, options);
        var changed = result.ChangedTracks > 0;
        var changedSelectedTrack = MidiEditorTransformValidationHelpers.IncludesSelectedTrack(context);

        return new MidiEditorTransformResult(
            Changed: changed,
            Summary: $"updated {result.ChangedTracks} track program(s)",
            ClearTrackSelection: changed,
            ClearEventSelection: changed && changedSelectedTrack,
            ReloadSelectedTrack: changed && changedSelectedTrack);
    }

    public static MidiForgeSetTrackProgramResult Apply(
        EditableMidiFile file,
        IEnumerable<int> trackIndices,
        MidiForgeSetTrackProgramOptions options)
    {
        var validTrackIndices = trackIndices
            .Where(index => index >= 0 && index < file.Tracks.Count && !file.Tracks[index].IsConductorTrack)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();

        var programNumber = (SevenBitNumber)(byte)Math.Clamp(options.ProgramNumber, 0, 127);
        var changedTracks = 0;
        var addedProgramChanges = 0;
        var updatedProgramChanges = 0;
        var renamedTracks = 0;

        foreach (var (trackIndex, fallbackIndex) in validTrackIndices.Select((index, order) => (index, order + 1)))
        {
            var track = file.Tracks[trackIndex];
            var sourceChunk = track.CloneCurrentChunk();
            var trackChanged = false;

            using (var manager = sourceChunk.ManageTimedEvents())
            {
                var timedProgramChanges = manager.Objects
                    .Where(timedEvent => timedEvent.Event is ProgramChangeEvent)
                    .OrderBy(timedEvent => timedEvent.Time)
                    .ToArray();

                if (timedProgramChanges.Length == 0)
                {
                    manager.Objects.Add(new TimedEvent(
                        new ProgramChangeEvent(programNumber)
                        {
                            Channel = (FourBitNumber)(byte)Math.Clamp(track.Channel, 0, 15),
                        },
                        0));
                    addedProgramChanges++;
                    trackChanged = true;
                }
                else
                {
                    var changesToUpdate = options.ReplaceAllProgramChanges
                        ? timedProgramChanges
                        : timedProgramChanges.Take(1);

                    foreach (var timedProgramChange in changesToUpdate)
                    {
                        var programChange = (ProgramChangeEvent)timedProgramChange.Event;
                        if (programChange.ProgramNumber == programNumber)
                            continue;

                        programChange.ProgramNumber = programNumber;
                        updatedProgramChanges++;
                        trackChanged = true;
                    }
                }
            }

            var replacementTrack = trackChanged
                ? new EditableTrack(sourceChunk, trackIndex)
                : track;

            if (options.RenameTracks)
            {
                var trackName = MidiForgeTrackNaming.GetTrackNameForProgram(
                    programNumber,
                    options.RenameMode,
                    fallbackIndex);
                if (MidiForgeTrackNameEditor.SetEditableTrackName(replacementTrack, trackName))
                {
                    renamedTracks++;
                    trackChanged = true;
                }
            }

            if (!trackChanged)
                continue;

            if (!ReferenceEquals(replacementTrack, track))
            {
                track.Dispose();
                file.Tracks[trackIndex] = replacementTrack;
            }

            changedTracks++;
        }

        if (changedTracks > 0)
            file.MarkChanged();

        return new MidiForgeSetTrackProgramResult(
            validTrackIndices.Length,
            changedTracks,
            addedProgramChanges,
            updatedProgramChanges,
            renamedTracks);
    }
}
