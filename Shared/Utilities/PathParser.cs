namespace ThriveDevCenter.Shared.Converters
{
    using System.Linq;

    public static class PathParser
    {
        public static string GetParentPath(string path)
        {
            // TODO: there's probably a more elegant algorithm possible here
            var pathParts = path.Split('/');
            return string.Join('/', pathParts.Take(pathParts.Length - 1));
        }
    }
}
