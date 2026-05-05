using System;
using System.Linq;
using Xunit;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models;

namespace UserService.Tests
{
    public class PsykologSessionDbTests
    {
        private static ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public void CanInsertAndRetrievePsykologSession()
        {
            using var context = CreateContext("TestPsykologSessionDb");

            var session = new PsykologSession
            {
                KeycloakId = "user-keycloak-id-1",
                StartedAt = DateTime.UtcNow,
                ThemeCount = 3,
                Status = PsykologSessionStatus.Active,
                SessionNumber = 1
            };

            context.PsykologSessions.Add(session);
            context.SaveChanges();

            var retrieved = context.PsykologSessions.FirstOrDefault(s => s.KeycloakId == "user-keycloak-id-1");
            Assert.NotNull(retrieved);
            Assert.Equal(PsykologSessionStatus.Active, retrieved.Status);
            Assert.Equal(3, retrieved.ThemeCount);
            Assert.Equal(1, retrieved.SessionNumber);
        }

        [Fact]
        public void PsykologSessionStatus_StoredAsInt()
        {
            var session = new PsykologSession
            {
                KeycloakId = "user-keycloak-id-2",
                Status = PsykologSessionStatus.Completed,
                SessionNumber = 2
            };

            Assert.Equal(PsykologSessionStatus.Completed, session.Status);
            Assert.Equal(1, (int)session.Status);
        }

        [Fact]
        public void CanInsertPsykologMessageLinkedToSession()
        {
            using var context = CreateContext("TestPsykologMessageDb");

            var session = new PsykologSession
            {
                KeycloakId = "user-keycloak-id-3",
                StartedAt = DateTime.UtcNow,
                Status = PsykologSessionStatus.Active,
                SessionNumber = 1
            };
            context.PsykologSessions.Add(session);
            context.SaveChanges();

            var message = new PsykologMessage
            {
                SessionId = session.Id,
                Role = PsykologRole.User,
                Content = "Hello, I need help.",
                CreatedAt = DateTime.UtcNow
            };
            context.PsykologMessages.Add(message);
            context.SaveChanges();

            var retrieved = context.PsykologMessages.FirstOrDefault(m => m.SessionId == session.Id);
            Assert.NotNull(retrieved);
            Assert.Equal(PsykologRole.User, retrieved.Role);
            Assert.Equal("Hello, I need help.", retrieved.Content);
        }

        [Fact]
        public void PsykologRole_StoredAsInt()
        {
            var message = new PsykologMessage
            {
                Role = PsykologRole.Assistant,
                Content = "How can I help you?"
            };

            Assert.Equal(PsykologRole.Assistant, message.Role);
            Assert.Equal(1, (int)message.Role);
        }

        [Fact]
        public void CascadeDelete_RemovesMessagesWhenSessionDeleted()
        {
            using var context = CreateContext("TestPsykologCascadeDb");

            var session = new PsykologSession
            {
                KeycloakId = "user-keycloak-id-4",
                StartedAt = DateTime.UtcNow,
                Status = PsykologSessionStatus.Active,
                SessionNumber = 1
            };
            context.PsykologSessions.Add(session);
            context.SaveChanges();

            context.PsykologMessages.Add(new PsykologMessage
            {
                SessionId = session.Id,
                Role = PsykologRole.User,
                Content = "Message 1",
                CreatedAt = DateTime.UtcNow
            });
            context.PsykologMessages.Add(new PsykologMessage
            {
                SessionId = session.Id,
                Role = PsykologRole.Assistant,
                Content = "Reply 1",
                CreatedAt = DateTime.UtcNow
            });
            context.SaveChanges();

            Assert.Equal(2, context.PsykologMessages.Count(m => m.SessionId == session.Id));

            context.PsykologSessions.Remove(session);
            context.SaveChanges();

            Assert.Equal(0, context.PsykologMessages.Count(m => m.SessionId == session.Id));
        }

        [Fact]
        public void PsykologSession_DefaultValues_AreCorrect()
        {
            var session = new PsykologSession();
            Assert.Equal(0, session.ThemeCount);
            Assert.Equal(PsykologSessionStatus.Active, session.Status);
            Assert.Null(session.EndedAt);
        }

        [Fact]
        public void PsykologSessionStatus_AllValues_Defined()
        {
            Assert.Equal(0, (int)PsykologSessionStatus.Active);
            Assert.Equal(1, (int)PsykologSessionStatus.Completed);
            Assert.Equal(2, (int)PsykologSessionStatus.Expired);
        }

        [Fact]
        public void PsykologRole_AllValues_Defined()
        {
            Assert.Equal(0, (int)PsykologRole.User);
            Assert.Equal(1, (int)PsykologRole.Assistant);
        }
    }
}
