namespace ThriveDevCenter.Shared.Notifications
{
    using Models;

    /// <summary>
    ///   Notification about a list being updated
    /// </summary>
    public abstract class ListUpdated<T> : SerializedNotification
        where T : class
    {
        public ListItemChangeType Type { get; init; } = ListItemChangeType.ItemUpdated;
        public T Item { get; init; }
    }

    public class LFSListUpdated : ListUpdated<LFSProjectInfo>
    {
    }
}