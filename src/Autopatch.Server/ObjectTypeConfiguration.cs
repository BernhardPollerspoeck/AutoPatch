namespace Autopatch.Server;

public class ObjectTypeConfiguration<T> where T : class
{
    public string[] ExcludedProperties { get; set; } = [];//TODO:
    public ClientChangePolicy ClientChangePolicy { get; set; } = ClientChangePolicy.AutoAccept;
    public TimeSpan? ThrottleInterval { get; set; }//TODO:
}

