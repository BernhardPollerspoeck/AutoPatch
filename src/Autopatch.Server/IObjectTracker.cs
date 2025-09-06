using System.Collections.Specialized;

namespace Autopatch.Server;

public interface IObjectTracker
{
    string TypeName { get; }
    INotifyCollectionChanged TrackedCollection { get; }

    void StartTracking();
    void StopTracking();

    void SendFullData(string connectionId);
}

public interface IObjectTracker<TCollection, TItem> : IObjectTracker
    where TCollection : INotifyCollectionChanged, IList<TItem>
{
    new TCollection TrackedCollection { get; }
}

