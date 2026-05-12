using System.Collections.Generic;

namespace MidiBard.Control.MidiControl.Editing;

public interface IMidiEditorTransform<in TOptions>
{
    MidiEditorTransformDescriptor Descriptor { get; }
    MidiEditorTransformValidation Validate(MidiEditorTransformContext context, TOptions options);
    MidiEditorTransformResult Execute(MidiEditorTransformContext context, TOptions options);
}

public sealed record MidiEditorTransformDescriptor(string Id, string DisplayName);

public sealed record MidiEditorTransformContext(
    EditableMidiFile File,
    IReadOnlyList<int> SelectedTrackIndices,
    int SelectedTrackIndex = -1,
    IReadOnlyList<int>? SelectedEventIndices = null);

public sealed record MidiEditorTransformValidation(bool IsValid, string? Message = null)
{
    public static MidiEditorTransformValidation Success { get; } = new(true);
    public static MidiEditorTransformValidation Failure(string message) => new(false, message);
}

public sealed record MidiEditorTransformResult(
    bool Changed,
    string? Summary = null,
    bool ClearTrackSelection = false,
    bool ClearEventSelection = false,
    bool ReloadSelectedTrack = false,
    bool ClearSelectedTrack = false)
{
    public static MidiEditorTransformResult NoChange(string? summary = null) => new(false, summary);
}

public sealed record MidiEditorTransformExecutionResult(
    bool Succeeded,
    bool Changed,
    MidiEditorTransformResult Result,
    string? Message)
{
    public static MidiEditorTransformExecutionResult ValidationFailed(string? message)
        => new(false, false, MidiEditorTransformResult.NoChange(message), message);
}

public sealed class MidiEditorTransformExecutor
{
    private readonly MidiForgeHistory history;

    public MidiEditorTransformExecutor(MidiForgeHistory history)
    {
        this.history = history;
    }

    public MidiEditorTransformExecutionResult Execute<TOptions>(
        MidiEditorTransformContext context,
        IMidiEditorTransform<TOptions> transform,
        TOptions options)
    {
        var validation = transform.Validate(context, options);
        if (!validation.IsValid)
            return MidiEditorTransformExecutionResult.ValidationFailed(validation.Message);

        var pendingHistory = history.BeginPendingCapture(context.File);
        var result = transform.Execute(context, options);

        if (result.Changed && context.File.Version == pendingHistory.Version)
            context.File.MarkChanged();

        var committed = history.CommitPendingCapture(context.File, pendingHistory);
        if (!committed)
            return new MidiEditorTransformExecutionResult(true, false, result with { Changed = false }, result.Summary);

        return new MidiEditorTransformExecutionResult(true, true, result with { Changed = true }, result.Summary);
    }
}
