using System.Globalization;

namespace Autopatch.Core;

/// <summary>
/// Conventions shared by the AutoPatch server and its clients.
/// </summary>
/// <remarks>
/// For every collection the server invokes the client method <c>AutoPatch/{subscriptionKey}</c> with the arguments
/// <c>(string target, PatchOperation[] operations, bool isInitialSet, long sequence)</c>:
/// <list type="bullet">
/// <item>An initial set replaces the complete client collection. Its operations are one <c>add /-</c> per item.</item>
/// <item>Every other batch has the sequence number of the previous batch plus one. A client that sees a smaller or equal number
/// ignores the batch; a client that sees a gap requests the full data again.</item>
/// </list>
/// </remarks>
public static class AutoPatchProtocol
{
    /// <summary>The default path the AutoPatch hub is mapped to.</summary>
    public const string DefaultHubPath = "/autopatch";

    /// <summary>The hub method a client calls to subscribe.</summary>
    public const string SubscribeMethod = "SubscribeToType";

    /// <summary>The hub method a client calls to unsubscribe.</summary>
    public const string UnsubscribeMethod = "UnsubscribeFromType";

    private const string MethodPrefix = "AutoPatch/";

    /// <summary>
    /// Gets the subscription key for a type name and an optional collection key.
    /// </summary>
    /// <returns><c>TypeName</c> or <c>TypeName/Key</c>.</returns>
    public static string GetSubscriptionKey(string typeName, string? key)
        => string.IsNullOrEmpty(key) ? typeName : $"{typeName}/{key}";

    /// <summary>
    /// Gets the client method (and SignalR group) name for a subscription key.
    /// </summary>
    public static string GetMethodName(string subscriptionKey) => MethodPrefix + subscriptionKey;

    /// <summary>
    /// Parses a patch path of the form <c>/index</c>, <c>/-</c> or <c>/index/Property</c>.
    /// </summary>
    /// <param name="path">The path to parse.</param>
    /// <param name="index">The item index, or <c>-1</c> for <c>/-</c>.</param>
    /// <param name="property">The property name, or <see langword="null"/> if the path addresses a whole item.</param>
    /// <returns><see langword="true"/> if the path has one of the supported forms.</returns>
    public static bool TryParsePath(string? path, out int index, out string? property)
    {
        index = -1;
        property = null;
        if (string.IsNullOrEmpty(path) || path[0] != '/')
        {
            return false;
        }

        var segments = path[1..].Split('/');
        if (segments.Length is < 1 or > 2)
        {
            return false;
        }

        if (segments.Length == 2)
        {
            if (segments[1].Length == 0)
            {
                return false;
            }
            property = segments[1].Replace("~1", "/").Replace("~0", "~");
        }

        if (segments[0] == "-")
        {
            return property is null;
        }

        return int.TryParse(segments[0], NumberStyles.None, CultureInfo.InvariantCulture, out index);
    }
}
