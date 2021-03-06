namespace ThriveDevCenter.Client.Utilities
{
    using System.Collections.Generic;

    public class ClientSideResourceStatus<T>
        where T : class, IDeletedResourceStatus, new()
    {
        private readonly Dictionary<long, T> statuses = new();

        public T GetStatus(long resourceId)
        {
            if (!statuses.ContainsKey(resourceId))
                statuses[resourceId] = new T();

            return statuses[resourceId];
        }

        public void SetDeletedStatus(long resourceId)
        {
            GetStatus(resourceId).Deleted = true;
        }

        public bool IsDeleted(long resourceId)
        {
            if (!statuses.ContainsKey(resourceId))
                return false;

            return statuses[resourceId].Deleted;
        }
    }

    public interface IDeletedResourceStatus
    {
        /// <summary>
        ///   This is used to pretend that an item is deleted before we get the server re-fetch of data done
        /// </summary>
        public bool Deleted { get; set; }
    }

    public class DeletedResourceStatus : IDeletedResourceStatus
    {
        public bool Deleted { get; set; }
    }
}
