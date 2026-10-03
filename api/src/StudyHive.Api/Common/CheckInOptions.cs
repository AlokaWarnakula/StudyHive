namespace StudyHive.Api.Common;

/// <summary>When a student may check in to a room booking (AUDIT C-04): from
/// <see cref="OpensMinutesBefore"/> minutes before it starts until it ends.</summary>
public sealed class CheckInOptions
{
    public const string SectionName = "CheckIn";

    public int OpensMinutesBefore { get; set; } = 15;
}
