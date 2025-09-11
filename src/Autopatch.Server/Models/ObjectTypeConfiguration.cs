namespace Autopatch.Server.Models;

/// <summary>
/// Represents configuration settings for a specific object type in the autopatch system.
/// </summary>
/// <typeparam name="T">The type of object that this configuration applies to.</typeparam>
/// <remarks>
/// This class provides settings to control how objects of type T are handled during 
/// patching operations, including property exclusions, client change policies, 
/// throttling, and batching configurations.
/// </remarks>
public class ObjectTypeConfiguration<T>
{
    /// <summary>
    /// Gets or sets an array of property names that should be excluded from patching operations.
    /// </summary>
    /// <value>
    /// An array of property names to exclude, or null if no properties should be excluded.
    /// </value>
    public string[]? ExcludedProperties { get; set; }

    /// <summary>
    /// Gets or sets the policy for handling client-initiated changes for this object type.
    /// </summary>
    /// <value>
    /// The client change policy. Defaults to <see cref="ClientChangePolicy.AutoAccept"/>.
    /// </value>
    public ClientChangePolicy ClientChangePolicy { get; set; } = ClientChangePolicy.AutoAccept;

    /// <summary>
    /// Gets or sets the minimum time interval between patch operations for this object type.
    /// </summary>
    /// <value>
    /// The throttle interval, or null if no throttling should be applied.
    /// </value>
    /// <remarks>
    /// When set, this property prevents patch operations from occurring more frequently 
    /// than the specified interval, helping to reduce system load.
    /// </remarks>
    public TimeSpan? ThrottleInterval { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of objects that can be processed in a single batch operation.
    /// </summary>
    /// <value>
    /// The maximum batch size, or null if no batch size limit should be enforced.
    /// </value>
    /// <remarks>
    /// This setting helps control memory usage and processing time by limiting the number 
    /// of objects processed simultaneously.
    /// </remarks>
    public int? MaxBatchSize { get; set; }
}

