namespace ThriveDevCenter.Server.Models
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    ///   Build configuration read from a yaml file before starting build jobs
    /// </summary>
    public class CiBuildConfiguration
    {
        [Required]
        [Range(1, 1)]
        public int Version { get; set; }

        [MaxLength(20)]
        [MinLength(1)]
        [Required]
        public Dictionary<string, CiJobConfiguration> Jobs { get; set; }
    }

    public class CiJobConfiguration
    {
        [Required]
        [StringLength(500, MinimumLength = 4)]
        public string Image { get; set; }

        public CiJobCacheConfiguration Cache { get; set; }

        [Required]
        [MinLength(1)]
        [MaxLength(50)]
        public List<CiJobBuildStep> Steps { get; set; }
    }

    public class CiJobCacheConfiguration
    {
        [Required]
        [MinLength(1)]
        [MaxLength(10)]
        public List<string> LoadFrom { get; set; }

        public string WriteTo { get; set; }
    }

    public class CiJobBuildStep
    {
        public CiJobBuildStepRun Run { get; set; }
    }

    public class CiJobBuildStepRun
    {
        [Required]
        [StringLength(90, MinimumLength = 2)]
        public string Name { get; set; }

        [Required]
        [StringLength(4000, MinimumLength = 1)]
        public string Command { get; set; }
    }
}
