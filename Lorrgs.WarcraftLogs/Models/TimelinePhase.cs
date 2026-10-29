namespace Lorrgs.WarcraftLogs.Models;

/// <summary>An encounter phase or intermission; NotePhaseId preserves the observed addon timer anchor.</summary>
public sealed record TimelinePhase(double Id, double Timestamp, bool IsIntermission = false,
    int? NotePhaseId = null);
