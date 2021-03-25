using Microsoft.AspNetCore.Mvc;

namespace ThriveDevCenter.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Authorization;
    using Filters;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Models;
    using Services;
    using Shared;
    using Shared.Models;
    using Utilities;

    [ApiController]
    [Route("LoginController")]
    public class LoginController : Controller
    {
        private const string DiscourseSsoEndpoint = "/session/sso_provider";
        private const int SsoNonceLength = 32;
        private const string SsoTypeDevForum = "devforum";
        private const string SsoTypeCommunityForum = "communityforum";
        private const string SsoTypePatreon = "patreon";

        private static readonly TimeSpan SsoTimeout = TimeSpan.FromMinutes(20);

        private readonly ILogger<LoginController> logger;
        private readonly ApplicationDbContext database;
        private readonly IConfiguration configuration;
        private readonly JwtTokens csrfVerifier;
        private readonly RedirectVerifier redirectVerifier;

        private readonly bool localLoginEnabled;

        public LoginController(ILogger<LoginController> logger, ApplicationDbContext database,
            IConfiguration configuration, JwtTokens csrfVerifier,
            RedirectVerifier redirectVerifier)
        {
            this.logger = logger;
            this.database = database;
            this.configuration = configuration;
            this.csrfVerifier = csrfVerifier;
            this.redirectVerifier = redirectVerifier;

            localLoginEnabled = Convert.ToBoolean(configuration["Login:Local:Enabled"]);
        }

        private bool DevForumConfigured => !string.IsNullOrEmpty(configuration["Login:DevForum:SsoSecret"]);
        private bool CommunityForumConfigured => !string.IsNullOrEmpty(configuration["Login:CommunityForum:SsoSecret"]);

        private bool PatreonConfigured => !string.IsNullOrEmpty(configuration["Login:Patreon:ClientId"]) &&
            !string.IsNullOrEmpty(configuration["Login:Patreon:ClientSecret"]);

        [HttpGet]
        public LoginOptions Get()
        {
            return new LoginOptions()
            {
                Categories = new List<LoginCategory>()
                {
                    new()
                    {
                        Name = "Developer login",
                        Options = new List<LoginOption>()
                        {
                            new()
                            {
                                ReadableName = "Login Using a Development Forum Account",
                                InternalName = SsoTypeDevForum,
                                Active = DevForumConfigured
                            }
                        }
                    },
                    new()
                    {
                        Name = "Supporter (patron) login",
                        Options = new List<LoginOption>()
                        {
                            new()
                            {
                                ReadableName = "Login Using a Community Forum Account",
                                InternalName = SsoTypeCommunityForum,
                                Active = CommunityForumConfigured
                            },
                            new()
                            {
                                ReadableName = "Login Using Patreon",
                                InternalName = SsoTypePatreon,
                                Active = PatreonConfigured
                            }
                        }
                    },
                    new()
                    {
                        Name = "Local Account",
                        Options = new List<LoginOption>()
                        {
                            new()
                            {
                                ReadableName = "Login using a local account",
                                InternalName = "local",
                                Active = localLoginEnabled,
                                Local = true
                            }
                        }
                    },
                }
            };
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartSsoLogin([Required] string ssoType, [FromBody] [Required] string csrf)
        {
            await PerformPreLoginChecks(csrf);

            switch (ssoType)
            {
                case SsoTypeDevForum:
                {
                    if (!DevForumConfigured)
                        return CreateResponseForDisabledOption();

                    return await DoDiscourseLoginRedirect(SsoTypeDevForum, configuration["Login:DevForum:SsoSecret"],
                        configuration["Login:DevForum:BaseUrl"]);
                }
                case SsoTypeCommunityForum:
                {
                    if (!CommunityForumConfigured)
                        return CreateResponseForDisabledOption();

                    return await DoDiscourseLoginRedirect(SsoTypeCommunityForum,
                        configuration["Login:CommunityForum:SsoSecret"],
                        configuration["Login:CommunityForum:BaseUrl"]);
                }
                case SsoTypePatreon:
                {
                    if (!PatreonConfigured)
                        return CreateResponseForDisabledOption();

                    var returnUrl = new Uri(configuration.GetBaseUrl(), $"/LoginController/return/{ssoType}")
                        .ToString();

                    var session = await BeginSsoLogin(ssoType);

                    var scopes = "identity identity[email]";

                    return Redirect(QueryHelpers.AddQueryString(
                        configuration["Login:Patreon:BaseUrl"],
                        new Dictionary<string, string>()
                        {
                            { "redirect_uri", returnUrl },
                            { "scope", scopes },
                            { "state", session.SsoNonce }
                        }));
                }
            }

            return Redirect(QueryHelpers.AddQueryString("/login", "error", "Invalid SsoType"));
        }

        [HttpGet("return/" + SsoTypeDevForum)]
        public async Task<IActionResult> SsoReturnDev([Required] string sso, [Required] string sig)
        {
            if (!DevForumConfigured)
                return CreateResponseForDisabledOption();

            return await HandleDiscourseSsoReturn(sso, sig, SsoTypeDevForum);
        }

        [HttpGet("return/" + SsoTypeCommunityForum)]
        public async Task<IActionResult> SsoReturnCommunity([Required] string sso, [Required] string sig)
        {
            if (!CommunityForumConfigured)
                return CreateResponseForDisabledOption();

            return await HandleDiscourseSsoReturn(sso, sig, SsoTypeCommunityForum);
        }

        [HttpGet("return/" + SsoTypePatreon)]
        public async Task<IActionResult> SsoReturnPatreon([Required] string state, [Required] string code, string error)
        {
            if (!PatreonConfigured)
                return CreateResponseForDisabledOption();

            if (!string.IsNullOrEmpty(error))
            {
                // TODO: is it safe to show this to the user?
                return Redirect(QueryHelpers.AddQueryString("/login", "error", $"Error from patreon: {error}"));
            }

            return await HandlePatreonSsoReturn(state, code);
        }

        [HttpPost("login")]
        public async Task<IActionResult> PerformLocalLogin([FromForm] LoginFormData login)
        {
            if (!localLoginEnabled)
                return CreateResponseForDisabledOption();

            await PerformPreLoginChecks(login.CSRF);

            var user = await database.Users.FirstOrDefaultAsync(u => u.Email == login.Email && u.Local);

            if (user == null || string.IsNullOrEmpty(user.PasswordHash) ||
                !Passwords.CheckPassword(user.PasswordHash, login.Password))
                return Redirect(QueryHelpers.AddQueryString("/login", "error", "Invalid username or password"));

            // Login is successful
            await BeginNewSession(user);

            if (string.IsNullOrEmpty(login.ReturnUrl) ||
                !redirectVerifier.SanitizeRedirectUrl(login.ReturnUrl, out string redirect))
            {
                return Redirect("/");
            }
            else
            {
                return Redirect(redirect);
            }
        }

        [NonAction]
        private async Task BeginNewSession(User user)
        {
            var remoteAddress = Request.HttpContext.Connection.RemoteIpAddress;

            var session = new Session
            {
                User = user, SessionVersion = user.SessionVersion, LastUsedFrom = remoteAddress
            };

            await database.Sessions.AddAsync(session);
            await database.SaveChangesAsync();

            logger.LogInformation("Successful login for user {Email} from {RemoteAddress}, session: {Id}", user.Email,
                remoteAddress, session.Id);

            SetSessionCookie(session);
        }

        private void SetSessionCookie(Session session)
        {
            var options = new CookieOptions
            {
                Expires = DateTime.UtcNow.AddSeconds(AppInfo.SessionExpirySeconds),
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,

                // TODO: do we need to set the domain explicitly?
                // options.Domain;

                // This might cause issues when locally testing with Chrome
                Secure = true,

                // Sessions are used for logins, they are essential. This might need to be re-thought out if
                // non-essential info is attached to sessions later
                IsEssential = true
            };

            Response.Cookies.Append(AppInfo.SessionCookieName, session.Id.ToString(), options);
        }

        [NonAction]
        private IActionResult CreateResponseForDisabledOption()
        {
            return Redirect(QueryHelpers.AddQueryString("/login", "error", "This login option is not enabled"));
        }

        [NonAction]
        private async Task PerformPreLoginChecks(string csrf)
        {
            var existingSession = await HttpContext.Request.Cookies.GetSession(database);

            // TODO: verify that the client making the request had up to date token
            if (!csrfVerifier.IsValidCSRFToken(csrf, existingSession?.User))
            {
                throw new HttpResponseException()
                    { Value = "Invalid CSRF token. Please refresh and try logging in again" };
            }

            // If there is an existing session, end it
            if (existingSession != null)
            {
                logger.LogInformation("Destroying an existing session before starting login");
                await LogoutController.PerformSessionDestroy(existingSession, database);
            }
        }

        [NonAction]
        private async Task<Session> BeginSsoLogin(string ssoSource)
        {
            var remoteAddress = Request.HttpContext.Connection.RemoteIpAddress;

            var session = new Session
            {
                LastUsedFrom = remoteAddress,
                SsoNonce = NonceGenerator.GenerateNonce(SsoNonceLength),
                StartedSsoLogin = ssoSource,
                SsoStartTime = DateTime.UtcNow
            };

            await database.Sessions.AddAsync(session);
            await database.SaveChangesAsync();

            SetSessionCookie(session);

            return session;
        }

        [NonAction]
        private async Task<IActionResult> DoDiscourseLoginRedirect(string ssoType, string secret, string redirectBase)
        {
            var returnUrl = new Uri(configuration.GetBaseUrl(), $"/LoginController/return/{ssoType}").ToString();

            var session = await BeginSsoLogin(ssoType);

            var payload = PrepareDiscoursePayload(session.SsoNonce, returnUrl);

            var signature = CalculateDiscourseSsoParamSignature(payload, secret);

            return Redirect(QueryHelpers.AddQueryString(
                new Uri(new Uri(redirectBase), DiscourseSsoEndpoint).ToString(),
                new Dictionary<string, string>()
                {
                    { "sso", payload },
                    { "sig", signature }
                }));
        }

        [NonAction]
        private string CalculateDiscourseSsoParamSignature(string payload, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));

            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }

        [NonAction]
        private string PrepareDiscoursePayload(string nonce, string returnUrl)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes($"nonce={nonce}&return_sso_url={returnUrl}"));
        }

        private async Task<(Session session, IActionResult result)> FetchAndCheckSessionForSsoReturn(string nonce)
        {
            var session = await HttpContext.Request.Cookies.GetSession(database);

            if (session == null || session.StartedSsoLogin != SsoTypeDevForum)
            {
                return (session, Redirect(QueryHelpers.AddQueryString("/login", "error",
                    "Your session was invalid. Please try again.")));
            }

            if (IsSsoTimedOut(session, out IActionResult timedOut))
                return (session, timedOut);

            var remoteAddress = Request.HttpContext.Connection.RemoteIpAddress;

            // Maybe this offers some extra security
            if (!session.LastUsedFrom.Equals(remoteAddress))
            {
                return (session, Redirect(QueryHelpers.AddQueryString("/login", "error",
                    "Your IP address changed during the login attempt.")));
            }

            if (session.SsoNonce != nonce || string.IsNullOrEmpty(session.SsoNonce))
                return (session, Redirect(QueryHelpers.AddQueryString("/login", "error",
                    "Invalid request nonce. Please try again.")));

            // Clear nonce after checking to disallow duplicate requests (needs to make sure to save)
            session.SsoNonce = null;
            return (session, null);
        }

        [NonAction]
        private bool IsSsoTimedOut(Session session, out IActionResult result)
        {
            if (session.SsoStartTime == null || DateTime.UtcNow - session.SsoStartTime > SsoTimeout)
            {
                result = Redirect(QueryHelpers.AddQueryString("/login", "error",
                    "The login attempt has expired. Please try again."));
                return true;
            }

            result = null;
            return false;
        }

        [NonAction]
        private IActionResult GetInvalidSsoParametersResult()
        {
            return Redirect(QueryHelpers.AddQueryString("/login", "error",
                "Invalid SSO parameters received"));
        }

        [NonAction]
        private async Task<IActionResult> HandleDiscourseSsoReturn(string ssoPayload, string signature, string ssoType)
        {
            string secret;
            bool developer;

            switch (ssoType)
            {
                case SsoTypeDevForum:
                    secret = configuration["Login:DevForum:SsoSecret"];
                    developer = true;
                    break;
                case SsoTypeCommunityForum:
                    secret = configuration["Login:CommunityForum:SsoSecret"];
                    developer = false;
                    break;
                default:
                    throw new ArgumentException("invalid discourse ssoType");
            }

            // Make sure the signature is right first
            var actualRequestSignature = CalculateDiscourseSsoParamSignature(ssoPayload, secret);

            if (actualRequestSignature != signature)
                return GetInvalidSsoParametersResult();

            // TODO: exception catching here?
            var payload = QueryHelpers.ParseQuery(Encoding.UTF8.GetString(Convert.FromBase64String(ssoPayload)));

            if (!payload.TryGetValue("nonce", out StringValues payloadNonce) || payloadNonce.Count != 1)
                return GetInvalidSsoParametersResult();

            var (session, result) = await FetchAndCheckSessionForSsoReturn(payloadNonce[0]);

            // Return in case of failure
            if (result != null)
                return result;

            bool requireSave = true;

            try
            {
                if (!payload.TryGetValue("email", out StringValues emailRaw) || emailRaw.Count != 1)
                    return GetInvalidSsoParametersResult();

                var email = emailRaw[0];

                if (ssoType == SsoTypeCommunityForum)
                {
                    // Check membership in required groups

                    if (!payload.TryGetValue("groups", out StringValues groups))
                        return GetInvalidSsoParametersResult();

                    if (!groups.Contains(DiscourseApiHelpers.CommunityDevBuildGroup) &&
                        !groups.Contains(DiscourseApiHelpers.CommunityVIPGroup))
                    {
                        logger.LogInformation("Not allowing login due to missing group membership for: {Email}", email);
                        return Redirect(QueryHelpers.AddQueryString("/login", "error",
                            "You must be either in the Supporter or VIP supporter group to login. " +
                            "These are granted to our Patrons. If you just signed up, please wait up to an " +
                            "hour for groups to sync."));
                    }
                }

                var username = email;

                if (payload.TryGetValue("email", out StringValues usernameRaw) && usernameRaw.Count != 1)
                {
                    username = usernameRaw[0];
                }

                var tuple = await HandleSsoLoginToAccount(session, email, username, ssoType, developer);
                requireSave = !tuple.saved;
                return tuple.result;
            }
            finally
            {
                if (requireSave)
                    await database.SaveChangesAsync();
            }
        }

        [NonAction]
        private async Task<IActionResult> HandlePatreonSsoReturn(string state, string code)
        {
            if (string.IsNullOrEmpty(code))
                return GetInvalidSsoParametersResult();

            string secret;

            var (session, result) = await FetchAndCheckSessionForSsoReturn(state);

            // Return in case of failure
            if (result != null)
                return result;

            return Redirect(QueryHelpers.AddQueryString("/login", "error",
                "Not implemented yet."));

            bool requireSave = true;

            try
            {
                // var tuple = await HandleSsoLoginToAccount(session, email, username, ssoType, developer);
                // requireSave = !tuple.saved;
                // return tuple.result;
            }
            finally
            {
                if (requireSave)
                    await database.SaveChangesAsync();
            }
        }

        [NonAction]
        private async Task<(IActionResult result, bool saved)> HandleSsoLoginToAccount(Session session, string email,
            string username,
            string ssoType,
            bool developerLogin)
        {
            if (string.IsNullOrEmpty(email))
                return (GetInvalidSsoParametersResult(), false);

            logger.LogInformation("Logging in SSO login user with email: {Email}", email);

            var user = await database.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                // New account needed
                logger.LogInformation("Creating new account for SSO login: {Email} developer: {DeveloperLogin}",
                    email, developerLogin);

                user = new User()
                {
                    Email = email,
                    UserName = username,
                    Local = false,
                    SsoSource = ssoType,
                    Developer = developerLogin,
                    Admin = false
                };

                await database.Users.AddAsync(user);
            }
            else if (user.Local == true)
            {
                return (Redirect(QueryHelpers.AddQueryString("/login", "error",
                    "Can't login to local account using SSO")), false);
            }
            else if (user.SsoSource != ssoType)
            {
                logger.LogInformation(
                    "User logged in with different SSO source than before, new: {SsoType}, old: {SsoSource}", ssoType,
                    user.SsoSource);

                if (user.SsoSource == SsoTypeDevForum || user.Developer == true)
                {
                    return (Redirect(QueryHelpers.AddQueryString("/login", "error",
                            "Your account is a developer account. You need to login through the Development Forums.")),
                        false);
                }

                if (ssoType == SsoTypeDevForum)
                {
                    // Conversion to a developer account
                    await database.LogEntries.AddAsync(new LogEntry()
                    {
                        Message = "User is now a developer due to different SSO login type",
                        TargetUser = user
                    });

                    user.Developer = true;
                    user.SsoSource = SsoTypeDevForum;
                }
                else if (user.SsoSource == SsoTypePatreon && ssoType == SsoTypeCommunityForum)
                {
                    logger.LogInformation("Patron logged in using a community forum account");
                }
                else if (user.SsoSource == SsoTypeCommunityForum && ssoType == SsoTypePatreon)
                {
                    logger.LogInformation("Community forum user logged in using patreon");
                }
                else
                {
                    throw new Exception("Unknown sso type (old, new) combination to move an user to");
                }
            }

            var result = await FinishSsoLoginToAccount(user, session);
            return (result, true);
        }

        [NonAction]
        private async Task<IActionResult> FinishSsoLoginToAccount(User user,
            Session session)
        {
            logger.LogInformation("Sso login succeeded to account: {Id}", user.Id);

            session.User = user;
            session.LastUsed = DateTime.UtcNow;
            session.StartedSsoLogin = null;
            session.SessionVersion = user.SessionVersion;

            await database.SaveChangesAsync();

            string returnUrl = null;

            if (string.IsNullOrEmpty(returnUrl) ||
                !redirectVerifier.SanitizeRedirectUrl(returnUrl, out string redirect))
            {
                return Redirect("/");
            }
            else
            {
                return Redirect(redirect);
            }
        }
    }

    public class LoginFormData
    {
        [Required]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        [Required]
        public string CSRF { get; set; }

        public string ReturnUrl { get; set; }
    }
}
