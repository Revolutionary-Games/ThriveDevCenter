using Microsoft.AspNetCore.Mvc;

namespace ThriveDevCenter.Server.Controllers
{
    using System.Threading.Tasks;
    using Authorization;
    using Hubs;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Models;
    using Services;
    using Shared.Forms;
    using Shared.Models;
    using Shared.Notifications;

    [ApiController]
    [Route("api/v1/[controller]")]
    public class RegistrationController : Controller
    {
        private readonly ILogger<RegistrationController> logger;
        private readonly IRegistrationStatus configuration;
        private readonly ITokenVerifier csrfVerifier;
        private readonly NotificationsEnabledDb database;

        public RegistrationController(ILogger<RegistrationController> logger,IRegistrationStatus configuration,
            ITokenVerifier csrfVerifier, NotificationsEnabledDb database)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.csrfVerifier = csrfVerifier;
            this.database = database;
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
        public async Task<IActionResult> Post(RegistrationFormData request)
        {
            if (!csrfVerifier.IsValidCSRFToken(request.CSRF, null, false))
                return BadRequest("Invalid CSRF");

            if (!SecurityHelpers.SlowEquals(request.RegistrationCode, configuration.RegistrationCode))
                return BadRequest("Invalid registration code");

            if (request.Name == null || request.Name.Length < 3)
                return BadRequest("Name is too short");

            if (request.Email == null || request.Email.Length < 3 || !request.Email.Contains('@'))
                return BadRequest("Email is invalid");

            if (request.Password == null || request.Password.Length < 6)
                return BadRequest("Password is too short");

            // Check for conflicting username or email
            if (await database.Users.AsQueryable().FirstOrDefaultAsync(u => u.UserName == request.Name) != null ||
                await database.Users.AsQueryable().FirstOrDefaultAsync(u => u.Email == request.Email) != null)
                return BadRequest("There is already an account associated with the given email or name");

            var password = Passwords.CreateSaltedPasswordHash(request.Password);

            var user = new User()
            {
                Email = request.Email,
                UserName = request.Name,
                PasswordHash = password,
                Local = true
            };

            await database.Users.AddAsync(user);
            await database.SaveChangesAsync();

            return Created($"/users/{user.Id}", user.GetInfo(RecordAccessLevel.Private));
        }
    }
}
