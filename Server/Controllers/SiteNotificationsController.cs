using Microsoft.AspNetCore.Mvc;

namespace ThriveDevCenter.Server.Controllers
{
    using System.ComponentModel.DataAnnotations;
    using System.Threading.Tasks;
    using Authorization;
    using Hubs;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;
    using Models;
    using Shared;
    using Shared.Forms;
    using Shared.Models;

    [ApiController]
    [Route("api/v1/[controller]")]
    public class SiteNotificationsController : Controller
    {
        private readonly ILogger<SiteNotificationsController> logger;
        private readonly ApplicationDbContext database;
        private readonly IHubContext<NotificationsHub, INotifications> notifications;

        public SiteNotificationsController(ILogger<SiteNotificationsController> logger, ApplicationDbContext database,
            IHubContext<NotificationsHub, INotifications> notifications)
        {
            this.logger = logger;
            this.database = database;
            this.notifications = notifications;
        }

        [AuthorizeRoleFilter(RequiredAccess = UserAccessLevel.Admin)]
        [HttpPost("ephemeralNotice")]
        public async Task<IActionResult> SendEphemeralNotice([Required] SiteNoticeFormData data)
        {
            var user = HttpContext.AuthenticatedUser();

            logger.LogInformation("New site notice (ephemeral) sent by: {Email}, text: {Message}, type: {Type}",
                user.Email, data.Message, data.Type);

            // As a site message is not a critical thing, only a normal log entry is created and not an admin action
            var log = new LogEntry()
            {
                Message = $"Ephemeral site message sent by \"{user.Name}\": {data.Message}"
            };

            await database.LogEntries.AddAsync(log);
            await database.SaveChangesAsync();

            await notifications.Clients.All.ReceiveSiteNotice(data.Type, data.Message);

            return Ok();
        }
    }
}
