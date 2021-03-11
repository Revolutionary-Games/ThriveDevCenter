namespace ThriveDevCenter.Shared.Models
{
    public class UserInfo : ClientSideTimedModel
    {
        public string Name { get; set; }
        public string Email { get; set; }

        public bool Developer { get; set; }
        public bool Admin { get; set; }

        public bool HasApiToken { get; set; }
        public bool HasLfsToken { get; set; }
        public int TotalLauncherLinks { get; set; }
    }
}
