using Microsoft.AspNetCore.Mvc;

namespace ThriveDevCenter.Server.Controllers
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Threading.Tasks;
    using Authorization;
    using BlazorPagination;
    using Filters;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Models;
    using Shared;
    using Shared.Models;
    using Utilities;

    [ApiController]
    [Route("api/v1/[controller]")]
    public class LauncherLinksController : Controller
    {
        private readonly ILogger<LauncherLinksController> logger;
        private readonly NotificationsEnabledDb database;

        public LauncherLinksController(ILogger<LauncherLinksController> logger,
            NotificationsEnabledDb database)
        {
            this.logger = logger;
            this.database = database;
        }

        [HttpGet("{userId:long}")]
        [AuthorizeRoleFilter]
        public async Task<ActionResult<PagedResult<LauncherLinkDTO>>> GetLinks([Required] long userId,
            [Required] string sortColumn,
            [Required] SortDirection sortDirection, [Required] [Range(1, int.MaxValue)] int page,
            [Required] [Range(1, 50)] int pageSize)
        {
            // Only admins can view other user's info
            if (userId != HttpContext.AuthenticatedUser().Id &&
                !HttpContext.HasAuthenticatedUserWithAccess(UserAccessLevel.Admin, AuthenticationScopeRestriction.None))
            {
                return Forbid();
            }

            IQueryable<LauncherLink> query;

            try
            {
                query = database.LauncherLinks.AsQueryable().Where(l => l.UserId == userId)
                    .OrderBy(sortColumn, sortDirection);
            }
            catch (ArgumentException e)
            {
                logger.LogWarning("Invalid requested order: {@E}", e);
                throw new HttpResponseException() { Value = "Invalid data selection or sort" };
            }

            var objects = await query.ToPagedResultAsync(page, pageSize);

            return objects.ConvertResult(i => i.GetDTO());
        }

        [HttpDelete("{userId:long}")]
        [AuthorizeRoleFilter]
        public async Task<IActionResult> DeleteAllLinks([Required] long userId)
        {
            var performingUser = HttpContext.AuthenticatedUser();

            // Only admins can delete other user's links
            if (userId != performingUser.Id &&
                !HttpContext.HasAuthenticatedUserWithAccess(UserAccessLevel.Admin, AuthenticationScopeRestriction.None))
            {
                return Forbid();
            }

            var linksToDelete = await database.LauncherLinks.AsQueryable().Where(l => l.UserId == userId).ToListAsync();

            // Skip doing anything if there's nothing to delete
            if (linksToDelete.Count < 1)
                return Ok();

            if (userId == performingUser.Id)
            {
                await database.LogEntries.AddAsync(new LogEntry()
                {
                    Message = "All launcher links deleted by self",
                    TargetUserId = userId
                });
            }
            else
            {
                await database.AdminActions.AddAsync(new AdminAction()
                {
                    Message = "All launcher links deleted by self",
                    TargetUserId = userId,
                    PerformedById = performingUser.Id
                });
            }

            database.LauncherLinks.RemoveRange(linksToDelete);

            await database.SaveChangesAsync();

            return Ok();
        }

        [HttpPost()]
        [AuthorizeRoleFilter]
        public async Task<IActionResult> CreateLinkCode()
        {
            var user = HttpContext.AuthenticatedUser();

            // Fail if too many links
            if (await database.LauncherLinks.AsQueryable().CountAsync(l => l.UserId == user.Id) >=
                AppInfo.DefaultMaxLauncherLinks)
            {
                return BadRequest("You already have the maximum number of launchers linked");
            }

            var modifiableUser = await database.Users.FindAsync(user.Id);

            if (modifiableUser == null)
            {
                throw new HttpResponseException()
                    { Status = StatusCodes.Status500InternalServerError, Value = "Failed to find target user" };
            }

            modifiableUser.LauncherLinkCode = Guid.NewGuid().ToString();
            modifiableUser.LauncherCodeExpires = DateTime.UtcNow + AppInfo.LauncherLinkCodeExpireTime;

            await database.SaveChangesAsync();

            return Ok(modifiableUser.LauncherLinkCode);
        }
    }
}
