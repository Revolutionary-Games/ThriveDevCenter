namespace ThriveDevCenter.Shared
{
    using System;

    /// <summary>
    ///   Holds the current version of the app, to detect mismatch between the client and the server.
    ///   Increment these numbers when the signalr definitions change or the APIs change
    /// </summary>
    public static class AppInfo
    {
        public const bool UsePrerendering = false;

        public const string SessionCookieName = "ThriveDevSession";

        public const string GitLfsContentType = "application/vnd.git-lfs+json";

        public const string CSRFNeededName = "CSRFRequired";
        public const string CSRFStatusName = "CSRF";
        public const string CurrentUserMiddlewareKey = "AuthenticatedUser";

        public const string LocalStorageUserInfo = "LastPageLoadUser";

        public const string ItemTypeFolder = "folder";
        public const string ItemTypeFile = "file";

        public const string SoftDeleteAttribute = "Deleted";

        public const int APITokenByteCount = 34;

        /// <summary>
        ///   Sessions (and cookies) expire after 30 days of inactivity
        /// </summary>
        public const int SessionExpirySeconds = 60 * 60 * 24 * 30;

        /// <summary>
        ///   Cookies expire 60 days after creation as there is no refresh mechanism this is set higher
        ///   than the session expiry
        /// </summary>
        public const int ClientCookieExpirySeconds = 60 * 60 * 24 * 60;

        public const int DefaultTableNotificationFetchTimer = 1000;
        public const int LongerTableNotificationFetchTimer = 5000;
        public const int LongestTableNotificationFetchTimer = 30000;
        public const int LongerTableRefreshIntervalCutoff = 4;
        public const int LongestTableRefreshIntervalCutoff = 11;

        /// <summary>
        ///   The interval in seconds that a session use is updated to the database
        /// </summary>
        public static readonly TimeSpan LastUsedSessionAccuracy = TimeSpan.FromSeconds(60);

        public const int Major = 1;
        public const int Minor = 6;

        public const int DefaultMaxLauncherLinks = 5;

        public const int MinimumRedeemableCodeLength = 8;
    }
}
