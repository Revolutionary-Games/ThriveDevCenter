namespace ThriveDevCenter.Server.Utilities
{
    using System.Security.Cryptography;
    using System.Text.Json;
    using Microsoft.AspNetCore.DataProtection;

    public class StorageUploadVerifyToken
    {
        private readonly IDataProtector dataProtector;

        public StorageUploadVerifyToken(IDataProtector dataProtector, string fileUploadPath, string fileStoragePath,
            int fileSize, long fileId, long? parentId, string unGzippedHash, string plainFileHash)
        {
            this.dataProtector = dataProtector;
            FileUploadPath = fileUploadPath;
            FileStoragePath = fileStoragePath;
            FileSize = fileSize;
            FileId = fileId;
            ParentId = parentId;
            UnGzippedHash = unGzippedHash;
            PlainFileHash = plainFileHash;
        }

        public string FileUploadPath { get; set; }
        public string FileStoragePath { get; set; }
        public int FileSize { get; set; }
        public long FileId { get; set; }
        public long? ParentId { get; set; }

        /// <summary>
        ///   This hash is done so that the remote file is ungzipped (in memory) when calculating the hash
        /// </summary>
        public string UnGzippedHash { get; set; }

        /// <summary>
        ///   This hash is directly just the sha3 of the remote file
        /// </summary>
        public string PlainFileHash { get; set; }

        public static StorageUploadVerifyToken TryToLoadFromString(IDataProtector dataProtector, string tokenStr)
        {
            try
            {
                var unprotected = dataProtector.Unprotect(tokenStr);
                return JsonSerializer.Deserialize<StorageUploadVerifyToken>(unprotected);
            }
            catch (CryptographicException)
            {
                return null;
            }
            catch (JsonException)
            {
                // TODO: logging for this? Quite unlikely that this can happen, but might be nice to try to log this
                return null;
            }
        }

        public override string ToString()
        {
            if (dataProtector == null)
                return base.ToString();

            return dataProtector.Protect(JsonSerializer.Serialize(this, new JsonSerializerOptions()
            {
                IgnoreNullValues = true
            }));
        }
    }
}