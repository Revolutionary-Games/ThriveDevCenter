namespace ThriveDevCenter.Shared.Models
{
    using System;
    using System.Net;
    using System.Text.Json.Serialization;
    using Converters;

    public class AccessKeyDTO : ClientSideTimedModel
    {
        public string Description { get; set; }
        public DateTime? LastUsed { get; set; }
        public AccessKeyType KeyType { get; set; }

        [JsonConverter(typeof(IPAddressConverter))]
        public IPAddress LastUsedFrom { get; set; }
    }
}
