namespace ThriveDevCenter.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Hangfire;
    using Jobs;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Models;
    using Shared.Models;

    public class RemoteServerHandler
    {
        private readonly ILogger<RemoteServerHandler> logger;
        private readonly NotificationsEnabledDb database;
        private readonly IEC2Controller ec2Controller;
        private readonly IBackgroundJobClient jobClient;
        private readonly Lazy<Task<List<ControlledServer>>> servers;

        private readonly int shutdownIdleDelay;
        private readonly int maximumRunningServers;
        private readonly bool useHibernate;

        public RemoteServerHandler(ILogger<RemoteServerHandler> logger, IConfiguration configuration,
            NotificationsEnabledDb database, IEC2Controller ec2Controller, IBackgroundJobClient jobClient)
        {
            this.logger = logger;
            this.database = database;
            this.ec2Controller = ec2Controller;
            this.jobClient = jobClient;
            shutdownIdleDelay = Convert.ToInt32(configuration["CI:ServerIdleTimeBeforeStop"]);
            maximumRunningServers = Convert.ToInt32(configuration["CI:MaximumConcurrentServers"]);
            useHibernate = Convert.ToBoolean(configuration["CI:UseHibernate"]);

            servers =
                new Lazy<Task<List<ControlledServer>>>(() =>
                    database.ControlledServers.AsQueryable().OrderBy(s => s.Id).ToListAsync());
        }

        public bool NewServersAdded { get; private set; }

        public Task<List<ControlledServer>> GetServers()
        {
            return servers.Value;
        }

        public async Task CheckServerStatuses(CancellationToken cancellationToken)
        {
            var toCheck = new List<ControlledServer>();

            var now = DateTime.UtcNow;

            foreach (var server in await GetServers())
            {
                switch (server.Status)
                {
                    case ServerStatus.WaitingForStartup:
                    case ServerStatus.Stopping:
                    {
                        // Don't check servers too often
                        if (now - server.StatusLastChecked > TimeSpan.FromSeconds(5))
                            toCheck.Add(server);

                        break;
                    }
                }
            }

            if (toCheck.Count > 0)
            {
                foreach (var status in await ec2Controller.GetInstanceStatuses(
                    toCheck.AsEnumerable().Select(i => i.InstanceId).ToList(), cancellationToken))
                {
                    var actualStatus = EC2Controller.InstanceStateToStatus(status);

                    var match = toCheck.First(i => i.InstanceId == status.InstanceId);
                    match.StatusLastChecked = now;

                    if (match.Status != actualStatus)
                    {
                        logger.LogInformation("Server {Id} is now in status: {ActualStatus}", match.Id, actualStatus);
                        match.Status = actualStatus;
                        match.ReservationType = ServerReservationType.None;
                        match.BumpUpdatedAt();

                        if (actualStatus == ServerStatus.Running)
                        {
                            match.PublicAddress = EC2Controller.InstanceIP(status);
                            match.RunningSince = now;
                        }
                        else if (match.RunningSince != null)
                        {
                            match.TotalRuntime += (now - match.RunningSince.Value).TotalSeconds;
                            match.RunningSince = null;
                        }
                    }
                }
            }
        }

        public async Task<bool> HandleCIJobs(List<CiJob> ciJobsNeedingActions)
        {
            int missingServer = 0;

            var potentialServers = await GetServers();

            foreach (var job in ciJobsNeedingActions)
            {
                if (job.State == CIJobState.Starting)
                {
                    // Need to find a server to run this job on
                    bool found = false;

                    foreach (var server in potentialServers)
                    {
                        if (server.Status == ServerStatus.Running && server.ProvisionedFully &&
                            !server.WantsMaintenance && server.ReservationType == ServerReservationType.None)
                        {
                            server.ReservationType = ServerReservationType.CIJob;
                            server.ReservedFor = job.CiJobId;
                            job.State = CIJobState.WaitingForServer;
                            server.BumpUpdatedAt();

                            await database.SaveChangesAsync();

                            // Can run this job here
                            jobClient.Enqueue<RunJobOnServerJob>(x =>
                                x.Execute(job.CiProjectId, job.CiBuildId, job.CiJobId, server.Id,
                                    RunJobOnServerJob.DefaultJobConnectRetries,
                                    CancellationToken.None));
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        ++missingServer;
                }
            }

            // Exit if all jobs have a server to run on
            if (missingServer < 1)
                return true;

            var provisioning = potentialServers.Count(s => s.Status == ServerStatus.Provisioning);
            var starting = potentialServers.Count(s => s.Status == ServerStatus.WaitingForStartup);
            var stopping = potentialServers.Count(s => s.Status == ServerStatus.Stopping);
            var running = potentialServers.Count(s => s.Status == ServerStatus.Running);

            logger.LogInformation(
                "Not enough server to run jobs, missing: {MissingServer} currently starting: {Starting} " +
                "provisioning: {Provisioning} stopping: {Stopping}", missingServer, starting, provisioning, stopping);

            // Starting and provisioning servers reduce the count of missing servers
            missingServer -= provisioning;
            missingServer -= starting;

            if (missingServer < 1)
                return false;

            // Don't start new servers if we are above the configured limit
            if (provisioning + starting + stopping + running >= maximumRunningServers)
            {
                logger.LogInformation("Maximum number of concurrent servers is reached, can't start more");
                return false;
            }

            // TODO: implement a server sweetspot, under which new servers can start as fast as wanted, but above
            // only a single server is allowed to be provisioning or starting at once

            // Start some existing server
            while (missingServer > 0)
            {
                bool foundServer = false;

                foreach (var server in potentialServers)
                {
                    if (server.WantsMaintenance)
                        continue;

                    if (server.Status == ServerStatus.Stopped)
                    {
                        --missingServer;
                        logger.LogInformation(
                            "Starting a stopped server to meet demand, still missing: {MissingServer}", missingServer);

                        await ec2Controller.ResumeInstance(server.InstanceId);

                        server.Status = ServerStatus.WaitingForStartup;
                        server.StatusLastChecked = DateTime.UtcNow;
                        server.BumpUpdatedAt();

                        await database.SaveChangesAsync();

                        foundServer = true;
                        break;
                    }

                    if (server.Status == ServerStatus.Terminated)
                    {
                        --missingServer;
                        logger.LogInformation(
                            "Re-provisioning a terminated server to meet demand, still missing: {MissingServer}",
                            missingServer);

                        // This shouldn't create multiple at once, but the API returns a list
                        var awsServers = await ec2Controller.LaunchNewInstance();
                        NewServersAdded = true;
                        bool first = true;

                        foreach (var awsServer in awsServers)
                        {
                            if (!first)
                            {
                                logger.LogError(
                                    "AWS API created more servers than we wanted, attempting to terminate the extra");
                                await ec2Controller.TerminateInstance(awsServer);
                                throw new Exception("AWS API created more servers than we wanted");
                            }

                            first = false;

                            server.SetProvisioningStatus(awsServer);

                            await database.SaveChangesAsync();

                            logger.LogInformation("Starting re-provisioning on {Id}", server.Id);

                            jobClient.Enqueue<ProvisionControlledServerJob>(x =>
                                x.Execute(server.Id, CancellationToken.None));
                        }

                        return false;
                    }
                }

                if (!foundServer)
                    break;
            }

            // If still not enough, create new servers if allowed
            if (missingServer > 0 && potentialServers.Count < maximumRunningServers)
            {
                logger.LogInformation("Creating a new server to meet demand");

                // This shouldn't create multiple at once, but the API returns a list
                List<string> awsServers;

                try
                {
                    awsServers = await ec2Controller.LaunchNewInstance();
                }
                catch (Exception e)
                {
                    logger.LogError("Failed to start new EC2 server instance: {@E}", e);
                    return false;
                }

                NewServersAdded = true;

                foreach (var awsServer in awsServers)
                {
                    var server = new ControlledServer()
                    {
                        InstanceId = awsServer
                    };

                    --missingServer;

                    await database.ControlledServers.AddAsync(server);
                    await database.SaveChangesAsync();

                    logger.LogInformation("New server {Id} created", server.Id);

                    jobClient.Enqueue<ProvisionControlledServerJob>(x => x.Execute(server.Id, CancellationToken.None));
                }
            }

            return false;
        }

        public async Task ShutdownIdleServers()
        {
            var now = DateTime.UtcNow;

            foreach (var server in await GetServers())
            {
                if (server.ProvisionedFully && server.Status == ServerStatus.Running &&
                    server.ReservationType == ServerReservationType.None)
                {
                    // Can potentially time out if last modified a while ago
                    var idleTime = now - server.UpdatedAt;
                    if (idleTime > TimeSpan.FromSeconds(shutdownIdleDelay))
                    {
                        var action = useHibernate ? "Hibernating" : "Stopping";

                        logger.LogInformation("{Action} server {Id} because it's been idle for: {IdleTime}", action,
                            server.Id, idleTime);

                        await ec2Controller.StopInstance(server.InstanceId, useHibernate);

                        server.Status = ServerStatus.Stopping;
                        server.BumpUpdatedAt();
                    }
                }
            }

            // TODO: check server termination for servers that have been stopped for a while, or ones that
            // need maintenance
        }
    }
}
