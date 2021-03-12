using Microsoft.AspNetCore.Mvc;

namespace ThriveDevCenter.Server.Controllers
{
    using Hubs;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;
    using Shared;

    [ApiController]
    [Route("api/v1/[controller]")]
    public class RegistrationController : Controller
    {
        private readonly ILogger<RegistrationController> logger;
        private readonly IHubContext<NotificationsHub, INotifications> notifications;
        private readonly RegistrationStatus configuration;

        public RegistrationController(ILogger<RegistrationController> logger,
            IHubContext<NotificationsHub, INotifications> notifications, RegistrationStatus configuration)
        {
            this.logger = logger;
            this.notifications = notifications;
            this.configuration = configuration;
        }

        /// <summary>
        ///   Returns true if registration is enabled
        /// </summary>
        [HttpGet]
        public bool Get()
        {
            return configuration.RegistrationEnabled;
        }

        [HttpPost]
        public IActionResult Post(RegistrationFormData request)
        {
            if (request.RegistrationCode != configuration.RegistrationCode)
                return BadRequest("Invalid registration code");

            return BadRequest("Not implemented");
        }
    }
}