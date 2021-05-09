namespace ThriveDevCenter.Server.Jobs
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Hangfire;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Models;
    using Services;
    using Shared.Models;

    public class RunJobOnServerJob
    {
        private readonly ILogger<RunJobOnServerJob> logger;
        private readonly NotificationsEnabledDb database;
        private readonly IBackgroundJobClient jobClient;
        private readonly GithubCommitStatusReporter statusReporter;

        public RunJobOnServerJob(ILogger<RunJobOnServerJob> logger, NotificationsEnabledDb database,
            IBackgroundJobClient jobClient, GithubCommitStatusReporter statusReporter)
        {
            this.logger = logger;
            this.database = database;
            this.jobClient = jobClient;
            this.statusReporter = statusReporter;
        }

        public async Task Execute(long ciProjectId, long ciBuildId, long ciJobId, long serverId,
            CancellationToken cancellationToken)
        {
            // Includes are needed here to provide fully populated data for update notifications
            var job = await database.CiJobs.Include(j => j.Build).ThenInclude(b => b.CiProject)
                .FirstOrDefaultAsync(
                    j => j.CiProjectId == ciProjectId && j.CiBuildId == ciBuildId && j.CiJobId == ciJobId,
                    cancellationToken);
            var server = await database.ControlledServers.FindAsync(new object[] { serverId }, cancellationToken);

            if (server == null)
                throw new ArgumentException("Could not find server to run build on");

            if (job == null)
            {
                ReleaseServerReservation(server);
                logger.LogWarning("Skipping CI job as it doesn't exist");
                return;
            }

            // TODO: check if ciJobId matches the reservation on the server?

            logger.LogInformation("Pretending that CI job {CIProjectId}-{CIBuildId}-{CIJobId} has run correctly",
                ciProjectId, ciBuildId, ciJobId);

            job.State = CIJobState.Finished;
            job.FinishedAt = DateTime.UtcNow;
            job.Succeeded = true;

            ReleaseServerReservation(server);

            // After running the job, the changes saving should now be skipped
            // ReSharper disable once MethodSupportsCancellation
            await database.SaveChangesAsync();

            // Send status to github
            var status = GithubAPI.CommitStatus.Success;
            string statusDescription = "Checks succeeded";

            if (!job.Succeeded)
            {
                status = GithubAPI.CommitStatus.Failure;
                statusDescription = "Some checks failed";
            }

            if (!await statusReporter.SetCommitStatus(job.Build.CiProject.RepositoryFullName, job.Build.CommitHash,
                status, statusReporter.CreateStatusUrlForJob(job), statusDescription,
                job.JobName))
            {
                logger.LogError("Failed to set commit status for build's job: {JobName}", job.JobName);
            }

            jobClient.Enqueue<CheckOverallBuildStatusJob>(x => x.Execute(ciProjectId, ciBuildId, CancellationToken.None));
        }

        private void ReleaseServerReservation(ControlledServer server)
        {
            server.ReservationType = ServerReservationType.None;
            server.ReservedFor = null;
            server.BumpUpdatedAt();
        }
    }
}