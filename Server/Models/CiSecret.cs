namespace ThriveDevCenter.Server.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using Microsoft.EntityFrameworkCore;
    using Shared;
    using Shared.Models;
    using Shared.Models.Enums;
    using Shared.Notifications;
    using Utilities;

    [Index(new[] { nameof(CiProjectId), nameof(SecretName) }, IsUnique = true)]
    public class CiSecret : IUpdateNotifications
    {
        public long CiProjectId { get; set; }

        public long CiSecretId { get; set; }

        [Required]
        public CISecretType UsedForBuildTypes { get; set; }

        [Required]
        [AllowSortingBy]
        public string SecretName { get; set; }

        [Required]
        public string SecretContent { get; set; }

        [AllowSortingBy]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public CiProject CiProject { get; set; }

        public CISecretDTO GetDTO()
        {
            return new()
            {
                CiProjectId = CiProjectId,
                CiSecretId = CiSecretId,
                UsedForBuildTypes = UsedForBuildTypes,
                SecretName = SecretName,
                CreatedAt = CreatedAt,
            };
        }

        public IEnumerable<Tuple<SerializedNotification, string>> GetNotifications(EntityState entityState)
        {
            var dto = GetDTO();

            yield return new Tuple<SerializedNotification, string>(new CIProjectSecretsUpdated()
            {
                Type = entityState.ToChangeType(),
                Item = dto,
            }, NotificationGroups.CIProjectSecretsUpdatedPrefix + CiProjectId);
        }
    }
}