namespace ThriveDevCenter.Shared.Models
{
    using System;

    public enum FileAccess
    {
        Public = 0,
        User,
        Developer,
        OwnerOrAdmin,

        /// <summary>
        ///   Only system access
        /// </summary>
        Nobody
    }

    public static class FileAccessHelpers
    {
        public static bool IsAccessibleTo(this FileAccess access, UserAccessLevel? userAccess, long? userId,
            long? objectOwnerId)
        {
            // Everyone has access to public
            if (access == FileAccess.Public)
                return true;

            // This is done to make it easier to call this method
            userAccess ??= UserAccessLevel.NotLoggedIn;

            // Unauthenticated users can only view public items
            // False is returned here as public access was checked above
            if (userId == null || userAccess == UserAccessLevel.NotLoggedIn)
                return false;

            // Admins can access anything not system-only
            if (userAccess == UserAccessLevel.Admin)
                return access != FileAccess.Nobody;

            // Object owner access
            if (objectOwnerId != null && userId == objectOwnerId)
                return access != FileAccess.Nobody;

            if (userAccess == UserAccessLevel.Developer)
            {
                return access == FileAccess.User || access == FileAccess.Developer;
            }
            else
            {
                return access == FileAccess.User;
            }
        }

        public static string ToUserReadableString(this FileAccess access)
        {
            switch (access)
            {
                case FileAccess.Public:
                    return "public";
                case FileAccess.User:
                    return "users";
                case FileAccess.Developer:
                    return "developers";
                case FileAccess.OwnerOrAdmin:
                    return "owner";
                case FileAccess.Nobody:
                    return "system";
                default:
                    throw new ArgumentOutOfRangeException(nameof(access), access, null);
            }
        }

        public static FileAccess AccessFromUserReadableString(string access)
        {
            switch (access.ToLowerInvariant())
            {
                case "public":
                    return FileAccess.Public;
                case "users":
                case "user":
                    return FileAccess.User;
                case "developers":
                case "developer":
                    return FileAccess.Developer;
                case "owner":
                case "owners":
                case "owner + admins":
                case "admins":
                case "admin":
                    return FileAccess.OwnerOrAdmin;
                case "system":
                case "nobody":
                    return FileAccess.Nobody;
                default:
                    throw new ArgumentException("Unknown name for FileAccess");
            }
        }
    }
}
