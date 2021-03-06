namespace ThriveDevCenter.Shared
{
    using System;

    /// <summary>
    ///   Holds App-wide constant values.
    ///   Contains the current version of the app, to detect mismatch between the client and the server.
    ///   Increment these numbers when the signalr definitions change or the APIs change.
    /// </summary>
    public static class AppInfo
    {
        public const bool UsePrerendering = false;

        public const string SessionCookieName = "ThriveDevSession";

        public const string SecondPrecisionDurationFormat = @"hh\:mm\:ss";

        public const string GitLfsContentType = "application/vnd.git-lfs+json";
        public const string GithubApiContentType = "application/vnd.github.v3+json";

        public const string CIConfigurationFile = "CIConfiguration.yml";

        public const string CSRFNeededName = "CSRFRequired";
        public const string CSRFStatusName = "CSRF";
        public const string CurrentUserMiddlewareKey = "AuthenticatedUser";
        public const string AccessKeyMiddlewareKey = "UsedAccessKey";
        public const string LauncherLinkMiddlewareKey = "UsedLauncherLink";

        public const string LocalStorageUserInfo = "LastPageLoadUser";

        public const string ItemTypeFolder = "folder";
        public const string ItemTypeFile = "file";

        public const string SoftDeleteAttribute = "Deleted";

        public const int APITokenByteCount = 34;

        public const int MaxDehydratedObjectsPerOffer = 100;
        public const int MaxDehydratedObjectsInDevBuild = 5000;
        public const int MaxPageSizeForBuildSearch = 100;
        public const int MaxDehydratedDownloadBatch = 100;

        public const int MaxDevBuildDescriptionLength = 4000;
        public const int MinimumDevBuildDescriptionLength = 20;
        public const int MaxDevBuildDescriptionNiceLineLength = 70;

        public const int KIBIBYTE = 1024;
        public const int MEBIBYTE = KIBIBYTE * KIBIBYTE;

        /// <summary>
        ///   Maximum size of a file to upload through LFS
        /// </summary>
        public const long MaxLfsUploadSize = 75 * MEBIBYTE;

        /// <summary>
        ///   Maximum size of a dehydrated file
        /// </summary>
        public const long MaxDehydratedUploadSize = 200 * MEBIBYTE;

        /// <summary>
        ///   Maximum size of a devbuild file
        /// </summary>
        public const long MaxDevBuildUploadSize = 50 * MEBIBYTE;

        /// <summary>
        ///   Maximum size of a file uploaded to the general file storage by a client
        /// </summary>
        public const long MaxGeneralFileStoreSize = 4024L * MEBIBYTE;

        public const int MaxInBrowserPreviewTextFileSize = MEBIBYTE * 20;
        public const int MaxSingleBuildOutputMessageLength = MEBIBYTE * 20;

        public const int MaxBuildOutputLineLength = 4000;

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

        public const long SingleResourceTableRowId = 1;

        public const int Major = 1;
        public const int Minor = 9;

        public const int DefaultMaxLauncherLinks = 5;

        public const int MinimumRedeemableCodeLength = 8;

        /// <summary>
        ///   The interval in seconds that a session use is updated to the database
        /// </summary>
        public static readonly TimeSpan LastUsedSessionAccuracy = TimeSpan.FromSeconds(60);

        public static readonly TimeSpan LastUsedAccessKeyAccuracy = TimeSpan.FromSeconds(60);

        /// <summary>
        ///   How long the token is valid to upload to the general remote storage
        /// </summary>
        public static readonly TimeSpan RemoteStorageUploadExpireTime = TimeSpan.FromMinutes(60);

        public static readonly TimeSpan RemoteStorageDownloadExpireTime = TimeSpan.FromMinutes(15);

        public static readonly TimeSpan LauncherLinkCodeExpireTime = TimeSpan.FromMinutes(15);
    }
}
