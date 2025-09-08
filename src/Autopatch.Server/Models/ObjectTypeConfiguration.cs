namespace Autopatch.Server.Models;

public class ObjectTypeConfiguration<T>
{
    public string[]? ExcludedProperties { get; set; }
    public ClientChangePolicy ClientChangePolicy { get; set; } = ClientChangePolicy.AutoAccept;
    public TimeSpan? ThrottleInterval { get; set; }
    public int? MaxBatchSize { get; set; }
}

