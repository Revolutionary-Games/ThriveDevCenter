namespace ThriveDevCenter.Server.Utilities
{
    using System.Threading;
    using System.Threading.Tasks;
    using Models;
    using Services;

    public static class LFSProjectTreeBuilder
    {
        public static async Task BuildFileTree(ILocalTempFileLocks tempFiles, LfsProject project,
            CancellationToken cancellationToken)
        {
            var semaphore = tempFiles.GetTempFilePath($"gitFileTrees/{project.Name}", out string tempPath);

            await semaphore.WaitAsync(cancellationToken);

            try
            {
                await GitRunHelpers.EnsureRepoIsCloned(project.CloneUrl, tempPath, cancellationToken);

                // TODO: rest of the file tree building logic
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}