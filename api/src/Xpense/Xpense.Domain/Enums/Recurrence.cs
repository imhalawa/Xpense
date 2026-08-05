namespace Xpense.Domain.Enums;

/// <summary>
/// How a budget repeats. <see cref="None"/> means it covers one window and never comes back.
/// <para>
/// The repeating values name calendar periods rather than lengths of time, so every period a budget
/// measures has a name -- 2026-W32, 2026-08, 2026 -- that a client, a report and a notification can
/// all mean the same thing by. "Every 30 days" and "from the 25th" are deliberately not expressible:
/// they would make a period something you compute rather than something you can refer to.
/// </para>
/// </summary>
public enum Recurrence
{
    None,
    Weekly,
    Monthly,
    Yearly
}
