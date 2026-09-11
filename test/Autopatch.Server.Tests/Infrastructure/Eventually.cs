using System.Diagnostics;

namespace Autopatch.Server.Tests.Infrastructure;

public static class Eventually
{
    /// <summary>
    /// Polls <paramref name="condition"/> until it holds or <paramref name="timeout"/> elapses.
    /// Exceptions thrown by the condition count as "not yet".
    /// </summary>
    public static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                if (condition())
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // The observed state is still changing; try again.
            }
            await Task.Delay(20);
        }
        return false;
    }
}
