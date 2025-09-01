namespace Autopatch.Server;

public class AutopatchOptions
{
    public TimeSpan DefaultThrottleInterval { get; set; }
    public int MaxBatchSize { get; set; }
}

