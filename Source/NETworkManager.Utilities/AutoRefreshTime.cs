using System;
using System.Collections.Generic;

namespace NETworkManager.Utilities;

/// <summary>
///     Class provides static methods to manage auto refresh time.
/// </summary>
public static class AutoRefreshTime
{
    /// <summary>
    ///     Method returns a list with default <see cref="AutoRefreshTimeInfo" />s.
    /// </summary>
    public static IEnumerable<AutoRefreshTimeInfo> GetDefaults =>
    [
        new(5, TimeUnit.Second),
        new(15, TimeUnit.Second),
        new(30, TimeUnit.Second),
        new(1, TimeUnit.Minute),
        new(5, TimeUnit.Minute)
    ];

    /// <summary>
    ///     Method to calculate a <see cref="TimeSpan" /> based on <see cref="AutoRefreshTimeInfo" />.
    /// </summary>
    /// <param name="info"><see cref="AutoRefreshTimeInfo" /> to calculate <see cref="TimeSpan" /></param>
    /// <returns>Returns the calculated <see cref="TimeSpan" />.</returns>
    public static TimeSpan CalculateTimeSpan(AutoRefreshTimeInfo info)
    {
        return info.TimeUnit switch
        {
            // Calculate the seconds
            TimeUnit.Second => new TimeSpan(0, 0, info.Value),
            // Calculate the minutes
            TimeUnit.Minute => new TimeSpan(0, info.Value * 60, 0),
            // Calculate the hours
            TimeUnit.Hour => new TimeSpan(info.Value * 60, 0, 0),
            _ => throw new Exception("Wrong time unit."),
        };
    }
}