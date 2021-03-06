namespace ThriveDevCenter.Server.Tests.Fixtures
{
    using System;
    using Server.Models;

    public class SimpleFewUsersDatabase : BaseSharedDatabaseFixture
    {
        private static readonly object Lock = new object();
        private static bool databaseInitialized;

        private static readonly Guid StaticSessionId1 = Guid.NewGuid();
        private static readonly Guid StaticSessionId2 = Guid.NewGuid();
        private static readonly Guid StaticSessionId3 = Guid.NewGuid();

        public SimpleFewUsersDatabase() : base("SimpleFewUsersDatabase")
        {
            lock (Lock)
            {
                if (!databaseInitialized)
                {
                    Seed();
                    databaseInitialized = true;
                }
            }
        }

        public Guid SessionId1 => StaticSessionId1;
        public Guid SessionId2 => StaticSessionId2;
        public Guid SessionId3 => StaticSessionId3;

        protected sealed override void Seed()
        {
            var user1 = new User
            {
                Id = 1,
                Email = "test@example.com",
                Name = "test",
                Local = true
            };

            Database.Users.Add(user1);

            Database.Sessions.Add(new Session
            {
                Id = SessionId1,
                User = user1
            });

            var user2 = new User
            {
                Id = 2,
                Email = "test2@example.com",
                Name = "test2",
                Local = true,
                Developer = true
            };

            Database.Users.Add(user2);

            Database.Sessions.Add(new Session
            {
                Id = SessionId2,
                User = user2
            });

            var user3 = new User
            {
                Id = 3,
                Email = "test3@example.com",
                Name = "test3",
                Local = true,
                Admin = true
            };

            Database.Users.Add(user3);

            Database.Sessions.Add(new Session
            {
                Id = SessionId3,
                User = user3
            });

            Database.SaveChanges();
        }
    }
}
