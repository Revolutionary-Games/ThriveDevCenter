namespace ThriveDevCenter.Client.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components;
    using Services;
    using ThriveDevCenter.Shared.Models;
    using ThriveDevCenter.Shared.Notifications;

    /// <summary>
    ///   Base class for blazor pages that show a single resource
    /// </summary>
    public abstract class SingleResourcePage<T, TNotification> : SingleResourcePage<T>,
        INotificationHandler<TNotification>
        where T : class, IIdentifiable
        where TNotification : ModelUpdated<T>
    {
        public Task Handle(TNotification notification, CancellationToken cancellationToken)
        {
            // TODO: could buffer the update if we are currently fetching data
            if (Data == null)
                return Task.CompletedTask;

            if (Data.Id == notification.Item.Id)
            {
                // Received an update for our data
                Data = notification.Item;
                return InvokeAsync(StateHasChanged);
            }

            return Task.CompletedTask;
        }

        public abstract void GetWantedListenedGroups(UserAccessLevel currentAccessLevel, ISet<string> groups);
    }

    /// <summary>
    ///   Base class for blazor pages that show a single resource without update notifications
    /// </summary>
    public abstract class SingleResourcePage<T> : SimpleResourceFetcher<T>
        where T : class
    {
        /// <summary>
        ///   Id of the resource to show
        /// </summary>
        [Parameter]
        public long Id { get; set; }

        protected override async Task FetchData()
        {
            var query = StartQuery();

            try
            {
                Data = await query;
            }
            catch (HttpRequestException e)
            {
                // Error write is not used here as we don't want to cause the blazor standard uncaught error popup
                Console.WriteLine($"Error getting single item data: {e}");

                if (e.StatusCode != HttpStatusCode.NotFound)
                {
                    Error = $"Error fetching data: {e.Message}";
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error getting single item data: {e}");
                Error = $"Error fetching data: {e.Message}";
            }

            Loading = false;

            if (Data != null)
            {
                if (!DataReceived)
                {
                    await OnFirstDataReceived();
                    DataReceived = true;
                }
            }

            await InvokeAsync(StateHasChanged);
        }
    }
}
