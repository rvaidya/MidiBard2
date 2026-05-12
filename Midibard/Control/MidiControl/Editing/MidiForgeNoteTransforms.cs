using System.Linq;

namespace MidiBard.Control.MidiControl.Editing;

public static class MidiForgeNoteTransforms
{
    public static IMidiEditorTransform<MidiForgeSplitToneRangeOptions> SplitByToneRange { get; } =
        new SplitByToneRangeTransform();

    public static IMidiEditorTransform<MidiForgeSplitLengthRangeOptions> SplitByLengthRange { get; } =
        new SplitByLengthRangeTransform();

    public static IMidiEditorTransform<MidiForgeSplitOverlappedNotesOptions> SplitOverlappedNotes { get; } =
        new SplitOverlappedNotesTransform();

    public static IMidiEditorTransform<MidiForgeTrimOverlappedNotesOptions> TrimOverlappedSustainedNotes { get; } =
        new TrimOverlappedSustainedNotesTransform();

    public static IMidiEditorTransform<MidiForgeExtendNotesDurationOptions> ExtendNotesDuration { get; } =
        new ExtendNotesDurationTransform();

    public static IMidiEditorTransform<MidiForgeComparisonTrackOptions> SplitEqualNotes { get; } =
        new SplitEqualNotesTransform();

    public static IMidiEditorTransform<MidiForgeComparisonTrackOptions> DifferenceTracks { get; } =
        new DifferenceTracksTransform();

    public static IMidiEditorTransform<MidiForgeSplitNotesIntoTracksOptions> SplitNotesIntoTracks { get; } =
        new SplitNotesIntoTracksTransform();

    public static IMidiEditorTransform<MidiForgeGeneratePitchBendNotesOptions> GeneratePitchBendNotes { get; } =
        new GeneratePitchBendNotesTransform();

    private sealed class SplitByToneRangeTransform : IMidiEditorTransform<MidiForgeSplitToneRangeOptions>
    {
        public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.split-by-tone-range", "Split Notes by Tone Range");

        public MidiEditorTransformValidation Validate(
            MidiEditorTransformContext context,
            MidiForgeSplitToneRangeOptions options)
            => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

        public MidiEditorTransformResult Execute(
            MidiEditorTransformContext context,
            MidiForgeSplitToneRangeOptions options)
        {
            var result = MidiForgeOperations.SplitTracksByToneRange(
                context.File,
                context.SelectedTrackIndices,
                options);

            return CreatedTrackResult(
                result.CreatedTracks,
                $"created {result.CreatedTracks} tone-range track(s)");
        }
    }

    private sealed class SplitByLengthRangeTransform : IMidiEditorTransform<MidiForgeSplitLengthRangeOptions>
    {
        public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.split-by-length-range", "Split Notes by Length Range");

        public MidiEditorTransformValidation Validate(
            MidiEditorTransformContext context,
            MidiForgeSplitLengthRangeOptions options)
            => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

        public MidiEditorTransformResult Execute(
            MidiEditorTransformContext context,
            MidiForgeSplitLengthRangeOptions options)
        {
            var result = MidiForgeOperations.SplitTracksByLengthRange(
                context.File,
                context.SelectedTrackIndices,
                options);

            return CreatedTrackResult(
                result.CreatedTracks,
                $"created {result.CreatedTracks} length-range track(s)");
        }
    }

    private sealed class SplitOverlappedNotesTransform : IMidiEditorTransform<MidiForgeSplitOverlappedNotesOptions>
    {
        public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.split-overlapped-notes", "Split Overlapped Notes");

        public MidiEditorTransformValidation Validate(
            MidiEditorTransformContext context,
            MidiForgeSplitOverlappedNotesOptions options)
            => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

        public MidiEditorTransformResult Execute(
            MidiEditorTransformContext context,
            MidiForgeSplitOverlappedNotesOptions options)
        {
            var result = MidiForgeOperations.SplitTracksOverlappedNotes(context.File, context.SelectedTrackIndices);

            return CreatedTrackResult(
                result.CreatedTracks,
                $"created {result.CreatedTracks} overlap split track(s)");
        }
    }

    private sealed class TrimOverlappedSustainedNotesTransform : IMidiEditorTransform<MidiForgeTrimOverlappedNotesOptions>
    {
        public MidiEditorTransformDescriptor Descriptor { get; } =
            new("forge.trim-overlapped-sustained-notes", "Trim Overlapped Sustained Notes");

        public MidiEditorTransformValidation Validate(
            MidiEditorTransformContext context,
            MidiForgeTrimOverlappedNotesOptions options)
            => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

        public MidiEditorTransformResult Execute(
            MidiEditorTransformContext context,
            MidiForgeTrimOverlappedNotesOptions options)
        {
            var result = MidiForgeOperations.TrimOverlappedSustainedNotes(context.File, context.SelectedTrackIndices);

            return CreatedTrackResult(
                result.CreatedTracks,
                $"created {result.CreatedTracks} trimmed track(s)");
        }
    }

    private sealed class ExtendNotesDurationTransform : IMidiEditorTransform<MidiForgeExtendNotesDurationOptions>
    {
        public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.extend-note-duration", "Extend Notes Duration");

        public MidiEditorTransformValidation Validate(
            MidiEditorTransformContext context,
            MidiForgeExtendNotesDurationOptions options)
            => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

        public MidiEditorTransformResult Execute(
            MidiEditorTransformContext context,
            MidiForgeExtendNotesDurationOptions options)
        {
            var result = MidiForgeOperations.ExtendNotesDuration(
                context.File,
                context.SelectedTrackIndices,
                options);

            return CreatedTrackResult(
                result.CreatedTracks,
                $"created {result.CreatedTracks} extended track(s)");
        }
    }

    private sealed class SplitEqualNotesTransform : IMidiEditorTransform<MidiForgeComparisonTrackOptions>
    {
        public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.split-equal-notes", "Split Equal Notes");

        public MidiEditorTransformValidation Validate(
            MidiEditorTransformContext context,
            MidiForgeComparisonTrackOptions options)
            => ValidateComparison(context, options);

        public MidiEditorTransformResult Execute(
            MidiEditorTransformContext context,
            MidiForgeComparisonTrackOptions options)
        {
            var result = MidiForgeOperations.SplitTracksEqualNotes(
                context.File,
                context.SelectedTrackIndices,
                options.TargetTrackIndex);

            return CreatedTrackResult(
                result.CreatedTracks,
                $"created {result.CreatedTracks} equal-note comparison track(s)");
        }
    }

    private sealed class DifferenceTracksTransform : IMidiEditorTransform<MidiForgeComparisonTrackOptions>
    {
        public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.difference-tracks", "Difference Tracks");

        public MidiEditorTransformValidation Validate(
            MidiEditorTransformContext context,
            MidiForgeComparisonTrackOptions options)
            => ValidateComparison(context, options);

        public MidiEditorTransformResult Execute(
            MidiEditorTransformContext context,
            MidiForgeComparisonTrackOptions options)
        {
            var result = MidiForgeOperations.DifferenceTracks(
                context.File,
                context.SelectedTrackIndices,
                options.TargetTrackIndex);

            return CreatedTrackResult(
                result.CreatedTracks,
                $"created {result.CreatedTracks} difference track(s)");
        }
    }

    private sealed class SplitNotesIntoTracksTransform : IMidiEditorTransform<MidiForgeSplitNotesIntoTracksOptions>
    {
        public MidiEditorTransformDescriptor Descriptor { get; } = new("forge.split-notes-into-tracks", "Split Notes Into Tracks");

        public MidiEditorTransformValidation Validate(
            MidiEditorTransformContext context,
            MidiForgeSplitNotesIntoTracksOptions options)
        {
            if (options.NumberOfTracks <= 0)
                return MidiEditorTransformValidation.Failure("Number of tracks must be greater than zero.");
            if (options.EveryNotesAmount <= 0)
                return MidiEditorTransformValidation.Failure("Every N notes must be greater than zero.");

            return MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);
        }

        public MidiEditorTransformResult Execute(
            MidiEditorTransformContext context,
            MidiForgeSplitNotesIntoTracksOptions options)
        {
            var result = MidiForgeOperations.SplitNotesIntoTracks(
                context.File,
                context.SelectedTrackIndices,
                options);

            return CreatedTrackResult(
                result.CreatedTracks,
                $"created {result.CreatedTracks} distributed-note track(s)");
        }
    }

    private sealed class GeneratePitchBendNotesTransform : IMidiEditorTransform<MidiForgeGeneratePitchBendNotesOptions>
    {
        public MidiEditorTransformDescriptor Descriptor { get; } =
            new("forge.generate-pitch-bend-notes", "Generate Pitch-Bend Notes");

        public MidiEditorTransformValidation Validate(
            MidiEditorTransformContext context,
            MidiForgeGeneratePitchBendNotesOptions options)
            => MidiEditorTransformValidationHelpers.RequireSelectedTracks(context);

        public MidiEditorTransformResult Execute(
            MidiEditorTransformContext context,
            MidiForgeGeneratePitchBendNotesOptions options)
        {
            var result = MidiForgeOperations.GeneratePitchBendNotes(
                context.File,
                context.SelectedTrackIndices,
                options);
            var changed = result.CreatedTracks > 0 || result.ReplacedTracks > 0;
            var replacedSelectedTrack = options.DeleteOriginalTracks
                && MidiEditorTransformValidationHelpers.IncludesSelectedTrack(context);

            return new MidiEditorTransformResult(
                Changed: changed,
                Summary: $"generated {result.GeneratedNotes} pitch-bend note segment(s)",
                ClearTrackSelection: changed,
                ClearEventSelection: changed && replacedSelectedTrack,
                ReloadSelectedTrack: changed && replacedSelectedTrack);
        }
    }

    private static MidiEditorTransformValidation ValidateComparison(
        MidiEditorTransformContext context,
        MidiForgeComparisonTrackOptions options)
    {
        if (context.SelectedTrackIndices.Count < 2)
            return MidiEditorTransformValidation.Failure("At least two performance tracks must be selected.");
        if (!context.SelectedTrackIndices.Contains(options.TargetTrackIndex))
            return MidiEditorTransformValidation.Failure("The target track must be selected.");

        return MidiEditorTransformValidation.Success;
    }

    private static MidiEditorTransformResult CreatedTrackResult(int createdTracks, string summary)
        => new(
            Changed: createdTracks > 0,
            Summary: summary,
            ClearTrackSelection: createdTracks > 0);
}
