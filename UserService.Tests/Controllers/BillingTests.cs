using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using UserService.Commands;
using UserService.Common;
using UserService.Controllers;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;
using UserService.Queries;
using Xunit;

namespace UserService.Tests.Billing;

public class BillingTests : IDisposable
{
    private readonly ApplicationDbContext _db;

    public BillingTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private const string TestUserId = "billing-test-user-1";

    // ── Query handler tests ──

    [Fact]
    public async Task GetEntitlement_NoRecord_ReturnsFree()
    {
        var handler = new GetEntitlementHandler(_db);
        var result = await handler.Handle(new GetEntitlementQuery(TestUserId), default);
        Assert.Equal(EntitlementTier.Free, result.Tier);
        Assert.False(result.IsPremium);
    }

    [Fact]
    public async Task GetEntitlement_ExpiredPremium_ReturnsFree()
    {
        _db.Entitlements.Add(new Entitlement
        {
            UserId = TestUserId,
            Tier = EntitlementTier.Premium,
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
        });
        await _db.SaveChangesAsync();

        var handler = new GetEntitlementHandler(_db);
        var result = await handler.Handle(new GetEntitlementQuery(TestUserId), default);
        Assert.Equal(EntitlementTier.Free, result.Tier);
        Assert.False(result.IsPremium);
    }

    [Fact]
    public async Task GetEntitlement_ActivePremium_ReturnsPremium()
    {
        _db.Entitlements.Add(new Entitlement
        {
            UserId = TestUserId,
            Tier = EntitlementTier.Premium,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        });
        await _db.SaveChangesAsync();

        var handler = new GetEntitlementHandler(_db);
        var result = await handler.Handle(new GetEntitlementQuery(TestUserId), default);
        Assert.Equal(EntitlementTier.Premium, result.Tier);
        Assert.True(result.IsPremium);
    }

    [Fact]
    public async Task GetSparksBalance_NoEntries_ReturnsZero()
    {
        var handler = new GetSparksBalanceHandler(_db);
        var result = await handler.Handle(new GetSparksBalanceQuery(TestUserId), default);
        Assert.Equal(0, result.Balance);
    }

    [Fact]
    public async Task GetSparksBalance_AfterCredit_ReturnsCorrect()
    {
        _db.SparksLedger.Add(new SparksLedgerEntry
        {
            UserId = TestUserId,
            Delta = 500, Reason = "purchase",
            BalanceAfter = 500,
        });
        await _db.SaveChangesAsync();

        var handler = new GetSparksBalanceHandler(_db);
        var result = await handler.Handle(new GetSparksBalanceQuery(TestUserId), default);
        Assert.Equal(500, result.Balance);
    }

    // ── Command handler tests ──

    [Fact]
    public async Task GrantPremium_CreatesNewEntitlement()
    {
        var handler = new GrantPremiumHandler(_db);
        var result = await handler.Handle(new GrantPremiumCommand(TestUserId, 30), default);
        Assert.Equal(EntitlementTier.Premium, result.Tier);
        Assert.NotNull(result.ExpiresAt);
        Assert.True(result.ExpiresAt > DateTime.UtcNow.AddDays(29));
        Assert.True(result.ExpiresAt < DateTime.UtcNow.AddDays(31));
    }

    [Fact]
    public async Task GrantPremium_ExtendsExisting()
    {
        _db.Entitlements.Add(new Entitlement
        {
            UserId = TestUserId,
            Tier = EntitlementTier.Premium,
            ExpiresAt = DateTime.UtcNow.AddDays(10),
        });
        await _db.SaveChangesAsync();

        var handler = new GrantPremiumHandler(_db);
        var result = await handler.Handle(new GrantPremiumCommand(TestUserId, 30), default);
        Assert.True(result.ExpiresAt > DateTime.UtcNow.AddDays(39));
    }

    [Fact]
    public async Task CreditSparks_AddsToLedger()
    {
        var handler = new CreditSparksHandler(_db);
        var result = await handler.Handle(new CreditSparksCommand(TestUserId, 200, "test"), default);
        Assert.Equal(200, result.NewBalance);
        Assert.Single(_db.SparksLedger);
    }

    [Fact]
    public async Task CreditSparks_Accumulates()
    {
        var h = new CreditSparksHandler(_db);
        await h.Handle(new CreditSparksCommand(TestUserId, 100, "first"), default);
        var result = await h.Handle(new CreditSparksCommand(TestUserId, 50, "second"), default);
        Assert.Equal(150, result.NewBalance);
    }

    [Fact]
    public async Task DebitSparks_Insufficient_ReturnsError()
    {
        var handler = new DebitSparksHandler(_db);
        var result = await handler.Handle(new DebitSparksCommand(TestUserId, 100, "spend"), default);
        Assert.False(result.Success);
        Assert.Equal("Insufficient Sparks", result.Error);
        Assert.Equal(0, result.NewBalance);
    }

    [Fact]
    public async Task DebitSparks_Sufficient_Deducts()
    {
        var h = new CreditSparksHandler(_db);
        await h.Handle(new CreditSparksCommand(TestUserId, 200, "earn"), default);
        var handler = new DebitSparksHandler(_db);
        var result = await handler.Handle(new DebitSparksCommand(TestUserId, 75, "spend"), default);
        Assert.True(result.Success);
        Assert.Equal(125, result.NewBalance);
        Assert.Null(result.Error);
    }

    // ── Controller tests ──

    private BillingController CreateController(Mock<IMediator>? mediatorMock = null)
    {
        mediatorMock ??= new Mock<IMediator>();
        var logger = new Mock<ILogger<BillingController>>();
        var config = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        config.Setup(c => c["InternalAuth:ApiKey"]).Returns("test-key");
        var controller = new BillingController(mediatorMock.Object, logger.Object, config.Object, _db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, TestUserId) }, "Test"))
            }
        };
        return controller;
    }

    [Fact]
    public async Task GetStatus_NoAuth_ReturnsUnauthorized()
    {
        var logger = new Mock<ILogger<BillingController>>();
        var config = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        var controller = new BillingController(Mock.Of<IMediator>(), logger.Object, config.Object, _db);
        var result = await controller.GetStatus();
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Purchase_ValidSku_SendsCommand()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GrantPremiumCommand>(), default))
            .ReturnsAsync(new GrantPremiumResponse(TestUserId, EntitlementTier.Premium, DateTime.UtcNow.AddDays(30)));

        var controller = CreateController(mediator);
        var result = await controller.Purchase(new SandboxPurchaseRequest("premium_month")) as OkObjectResult;

        Assert.NotNull(result);
        var response = Assert.IsType<ApiResponse<object>>(result!.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task Purchase_InvalidSku_ReturnsBadRequest()
    {
        var controller = CreateController();
        var result = await controller.Purchase(new SandboxPurchaseRequest("invalid"));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetCatalog_ReturnsPlansAndBundles()
    {
        var controller = CreateController();
        var result = controller.GetCatalog() as OkObjectResult;
        Assert.NotNull(result);
        var catalog = Assert.IsType<PremiumCatalogResponse>(result!.Value);
        Assert.NotEmpty(catalog.Plans);
        Assert.NotEmpty(catalog.Bundles);
    }

    // ── Sparks daily allocation + spend tests (Feeld-inspired) ──

    [Fact]
    public async Task GetSparksStatus_FreeUser_ZeroDailyMax()
    {
        // Free user with no entitlement → dailyMax=0, no auto-allocation
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetEntitlementQuery>(), default))
            .ReturnsAsync(new GetEntitlementResponse(TestUserId, EntitlementTier.Free, null, false));

        var handler = new GetSparksStatusHandler(_db, mediator.Object);
        var result = await handler.Handle(new GetSparksStatusQuery(TestUserId), default);

        Assert.Equal(0, result.DailyMax);
        Assert.Equal(0, result.DailyUsed);
        Assert.Equal(0, result.DailyRemaining);
        Assert.Equal(0, result.TotalBalance);
        // Should NOT have auto-allocated anything (free user)
        mediator.Verify(m => m.Send(It.IsAny<CreditSparksCommand>(), default), Times.Never);
    }

    [Fact]
    public async Task GetSparksStatus_PremiumUser_AutoAllocatesTwo()
    {
        _db.Entitlements.Add(new Entitlement
        {
            UserId = TestUserId,
            Tier = EntitlementTier.Premium,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        });
        await _db.SaveChangesAsync();

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetEntitlementQuery>(), default))
            .ReturnsAsync(new GetEntitlementResponse(TestUserId, EntitlementTier.Premium, DateTime.UtcNow.AddDays(30), true));
        // The handler sends CreditSparksCommand internally — capture it
        mediator.Setup(m => m.Send(It.IsAny<CreditSparksCommand>(), default))
            .ReturnsAsync(new CreditSparksResponse(TestUserId, 2));

        var handler = new GetSparksStatusHandler(_db, mediator.Object);
        var result = await handler.Handle(new GetSparksStatusQuery(TestUserId), default);

        Assert.Equal(2, result.DailyMax);
        Assert.Equal(0, result.DailyUsed);
        Assert.Equal(2, result.DailyRemaining);
        // Should have triggered auto-allocation
        mediator.Verify(m => m.Send(It.Is<CreditSparksCommand>(c => c.Amount == 2 && c.Reason == "daily_allocation"), default), Times.Once);
    }

    [Fact]
    public async Task GetSparksStatus_Premium_AlreadyUsed_TracksCorrectly()
    {
        _db.Entitlements.Add(new Entitlement
        {
            UserId = TestUserId,
            Tier = EntitlementTier.Premium,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        });
        // Already credited + spent 1 today
        _db.SparksLedger.Add(new SparksLedgerEntry
        {
            UserId = TestUserId, Delta = 2, Reason = "daily_allocation", BalanceAfter = 2,
            CreatedAt = DateTime.UtcNow,
        });
        _db.SparksLedger.Add(new SparksLedgerEntry
        {
            UserId = TestUserId, Delta = -1, Reason = "spark_ping", BalanceAfter = 1,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetEntitlementQuery>(), default))
            .ReturnsAsync(new GetEntitlementResponse(TestUserId, EntitlementTier.Premium, DateTime.UtcNow.AddDays(30), true));

        var handler = new GetSparksStatusHandler(_db, mediator.Object);
        var result = await handler.Handle(new GetSparksStatusQuery(TestUserId), default);

        Assert.Equal(2, result.DailyMax);
        Assert.Equal(1, result.DailyUsed);
        Assert.Equal(1, result.DailyRemaining);
        Assert.Equal(1, result.TotalBalance);
    }

    [Fact]
    public async Task SpendSpark_FreeUser_NoSparks_ReturnsError()
    {
        var mediator = new Mock<IMediator>();
        // Free entitlement → no allocation
        mediator.Setup(m => m.Send(It.IsAny<GetEntitlementQuery>(), default))
            .ReturnsAsync(new GetEntitlementResponse(TestUserId, EntitlementTier.Free, null, false));

        var handler = new SpendSparkHandler(_db, mediator.Object);
        var result = await handler.Handle(new SpendSparkCommand(TestUserId, "spark_ping"), default);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("No Sparks available", result.Error);
    }

    [Fact]
    public async Task SpendSpark_PremiumUser_DailyAllocation_Succeeds()
    {
        _db.Entitlements.Add(new Entitlement
        {
            UserId = TestUserId,
            Tier = EntitlementTier.Premium,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        });
        await _db.SaveChangesAsync();

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetEntitlementQuery>(), default))
            .ReturnsAsync(new GetEntitlementResponse(TestUserId, EntitlementTier.Premium, DateTime.UtcNow.AddDays(30), true));
        // Capture auto-allocation
        mediator.Setup(m => m.Send(It.Is<CreditSparksCommand>(c => c.Reason == "daily_allocation"), default))
            .ReturnsAsync(new CreditSparksResponse(TestUserId, 2));
        // Capture debit
        mediator.Setup(m => m.Send(It.Is<DebitSparksCommand>(c => c.Reason == "spark_ping"), default))
            .ReturnsAsync(new DebitSparksResponse(TestUserId, 1, true, null));

        var handler = new SpendSparkHandler(_db, mediator.Object);
        var result = await handler.Handle(new SpendSparkCommand(TestUserId, "spark_ping"), default);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task SpendSpark_InvalidAction_ReturnsError()
    {
        var mediator = new Mock<IMediator>();
        var handler = new SpendSparkHandler(_db, mediator.Object);
        var result = await handler.Handle(new SpendSparkCommand(TestUserId, "invalid_action"), default);

        Assert.False(result.Success);
        Assert.Contains("Invalid action", result.Error);
    }

    [Fact]
    public async Task SpendSpark_NoAction_ReturnsError()
    {
        var mediator = new Mock<IMediator>();
        var handler = new SpendSparkHandler(_db, mediator.Object);
        var result = await handler.Handle(new SpendSparkCommand(TestUserId, ""), default);

        Assert.False(result.Success);
        Assert.Contains("Action is required", result.Error);
    }

    [Fact]
    public async Task SpendSpark_PurchasedBalance_WhenDailyExhausted()
    {
        _db.Entitlements.Add(new Entitlement
        {
            UserId = TestUserId,
            Tier = EntitlementTier.Premium,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        });
        // Already used both daily Sparks; purchased 5 more
        _db.SparksLedger.Add(new SparksLedgerEntry
        {
            UserId = TestUserId, Delta = 2, Reason = "daily_allocation", BalanceAfter = 2,
            CreatedAt = DateTime.UtcNow,
        });
        _db.SparksLedger.Add(new SparksLedgerEntry
        {
            UserId = TestUserId, Delta = -2, Reason = "spark_ping", BalanceAfter = 0,
            CreatedAt = DateTime.UtcNow,
        });
        _db.SparksLedger.Add(new SparksLedgerEntry
        {
            UserId = TestUserId, Delta = 5, Reason = "purchase", BalanceAfter = 5,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetEntitlementQuery>(), default))
            .ReturnsAsync(new GetEntitlementResponse(TestUserId, EntitlementTier.Premium, DateTime.UtcNow.AddDays(30), true));
        // Daily allocation was already granted, no new grant
        // But daily used = 2, daily max = 2 → no daily remaining
        // Should fall back to purchased Sparks
        mediator.Setup(m => m.Send(It.Is<DebitSparksCommand>(c => c.Reason == "spark_ping"), default))
            .ReturnsAsync(new DebitSparksResponse(TestUserId, 4, true, null));

        var handler = new SpendSparkHandler(_db, mediator.Object);
        var result = await handler.Handle(new SpendSparkCommand(TestUserId, "spark_ping"), default);

        Assert.True(result.Success);
        Assert.Equal(4, result.NewBalance);
    }

    // ── Spark Record tests ──

    private const string SenderId = "spark-sender-1";
    private const string RecipientId = "spark-recipient-1";

    [Fact]
    public async Task SendSparkHandler_DeductsSparkAndCreatesRecord()
    {
        var h = new CreditSparksHandler(_db);
        await h.Handle(new CreditSparksCommand(SenderId, 200, "purchase"), default);

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetEntitlementQuery>(), default))
            .ReturnsAsync(new GetEntitlementResponse(SenderId, EntitlementTier.Free, null, false));
        // SendSparkHandler internally sends SpendSparkCommand, not DebitSparksCommand directly
        mediator.Setup(m => m.Send(It.Is<SpendSparkCommand>(c => c.Action == "spark_ping"), default))
            .ReturnsAsync(new SpendSparkResponse(true, 199, 0, null));

        var handler = new SendSparkHandler(_db, mediator.Object, new Mock<ILogger<SendSparkHandler>>().Object);

        var result = await handler.Handle(new SendSparkCommand(SenderId, RecipientId, "Hello!"), default);

        Assert.True(result.Success);
        Assert.Equal(199, result.NewBalance);
        Assert.NotNull(result.SparkRecordId);
        var records = _db.Sparks.ToList();
        Assert.Single(records);
        Assert.Equal(SenderId, records[0].SenderUserId);
        Assert.Equal(RecipientId, records[0].RecipientUserId);
        Assert.Equal("Hello!", records[0].Message);
        Assert.False(records[0].IsRead);
    }

    [Fact]
    public async Task SendSparkHandler_InsufficientSparks_ReturnsError()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetEntitlementQuery>(), default))
            .ReturnsAsync(new GetEntitlementResponse(SenderId, EntitlementTier.Free, null, false));
        // SpendSparkCommand returns failure (insufficient sparks)
        mediator.Setup(m => m.Send(It.Is<SpendSparkCommand>(c => c.Action == "spark_ping"), default))
            .ReturnsAsync(new SpendSparkResponse(false, 0, 0, "No Sparks available. Purchase a bundle or upgrade to Majestic."));

        var handler = new SendSparkHandler(_db, mediator.Object, new Mock<ILogger<SendSparkHandler>>().Object);

        var result = await handler.Handle(new SendSparkCommand(SenderId, RecipientId, null), default);

        Assert.False(result.Success);
        Assert.Equal("No Sparks available. Purchase a bundle or upgrade to Majestic.", result.Error);
        Assert.Null(result.SparkRecordId);
        Assert.Empty(_db.Sparks);
    }

    [Fact]
    public async Task SendSparkHandler_TruncatesLongMessage()
    {
        var h = new CreditSparksHandler(_db);
        await h.Handle(new CreditSparksCommand(SenderId, 100, "purchase"), default);

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetEntitlementQuery>(), default))
            .ReturnsAsync(new GetEntitlementResponse(SenderId, EntitlementTier.Free, null, false));
        mediator.Setup(m => m.Send(It.Is<SpendSparkCommand>(c => c.Action == "spark_ping"), default))
            .ReturnsAsync(new SpendSparkResponse(true, 99, 0, null));

        var handler = new SendSparkHandler(_db, mediator.Object, new Mock<ILogger<SendSparkHandler>>().Object);

        var longMsg = new string('x', 500);
        var result = await handler.Handle(new SendSparkCommand(SenderId, RecipientId, longMsg), default);

        Assert.True(result.Success);
        var record = _db.Sparks.First();
        Assert.Equal(200, record.Message?.Length);
    }

    [Fact]
    public async Task GetReceivedSparksHandler_ReturnsOnlyRecipientSparks()
    {
        var h = new CreditSparksHandler(_db);
        await h.Handle(new CreditSparksCommand("sender-a", 100, "purchase"), default);
        await h.Handle(new CreditSparksCommand("sender-b", 100, "purchase"), default);

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetEntitlementQuery>(), default))
            .ReturnsAsync(new GetEntitlementResponse("sender-a", EntitlementTier.Free, null, false));
        mediator.Setup(m => m.Send(It.Is<SpendSparkCommand>(c => c.Action == "spark_ping"), default))
            .ReturnsAsync(new SpendSparkResponse(true, 99, 0, null));

        var sendHandler = new SendSparkHandler(_db, mediator.Object, new Mock<ILogger<SendSparkHandler>>().Object);

        await sendHandler.Handle(new SendSparkCommand("sender-a", "recipient-1", "Spark from A"), default);
        await sendHandler.Handle(new SendSparkCommand("sender-b", "recipient-1", "Spark from B"), default);
        await sendHandler.Handle(new SendSparkCommand("sender-a", "other-recipient", "Not for you"), default);

        var queryHandler = new GetReceivedSparksHandler(_db, new Mock<ILogger<GetReceivedSparksHandler>>().Object);

        var result = await queryHandler.Handle(new GetReceivedSparksQuery("recipient-1"), default);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Sparks, s => Assert.Equal("recipient-1", s.RecipientUserId));
    }

    [Fact]
    public async Task GetReceivedSparksHandler_RespectsPagination()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetEntitlementQuery>(), default))
            .ReturnsAsync(new GetEntitlementResponse("sender", EntitlementTier.Free, null, false));
        mediator.Setup(m => m.Send(It.Is<SpendSparkCommand>(c => c.Action == "spark_ping"), default))
            .ReturnsAsync(new SpendSparkResponse(true, 0, 0, null));

        var sendHandler = new SendSparkHandler(_db, mediator.Object, new Mock<ILogger<SendSparkHandler>>().Object);

        for (int i = 0; i < 5; i++)
            await sendHandler.Handle(new SendSparkCommand("sender", "recipient-p", $"Spark {i}"), default);

        var queryHandler = new GetReceivedSparksHandler(_db, new Mock<ILogger<GetReceivedSparksHandler>>().Object);

        var page1 = await queryHandler.Handle(new GetReceivedSparksQuery("recipient-p", 1, 2), default);
        Assert.Equal(2, page1.Sparks.Count);
        Assert.Equal(5, page1.TotalCount);

        var page3 = await queryHandler.Handle(new GetReceivedSparksQuery("recipient-p", 3, 2), default);
        Assert.Single(page3.Sparks);
    }

    [Fact]
    public async Task GetSentSparksHandler_ReturnsOnlySenderSparks()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetEntitlementQuery>(), default))
            .ReturnsAsync(new GetEntitlementResponse("sender-x", EntitlementTier.Free, null, false));
        mediator.Setup(m => m.Send(It.Is<SpendSparkCommand>(c => c.Action == "spark_ping"), default))
            .ReturnsAsync(new SpendSparkResponse(true, 0, 0, null));

        var sendHandler = new SendSparkHandler(_db, mediator.Object, new Mock<ILogger<SendSparkHandler>>().Object);

        await sendHandler.Handle(new SendSparkCommand("sender-x", "alice", "Hi Alice"), default);
        await sendHandler.Handle(new SendSparkCommand("sender-x", "bob", "Hi Bob"), default);
        await sendHandler.Handle(new SendSparkCommand("other-sender", "alice", "From other"), default);

        var queryHandler = new GetSentSparksHandler(_db, new Mock<ILogger<GetSentSparksHandler>>().Object);

        var result = await queryHandler.Handle(new GetSentSparksQuery("sender-x"), default);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Sparks, s => Assert.Equal("sender-x", s.SenderUserId));
    }

    [Fact]
    public async Task GetSentSparksHandler_EmptyForNoSparks()
    {
        var handler = new GetSentSparksHandler(_db, new Mock<ILogger<GetSentSparksHandler>>().Object);
        var result = await handler.Handle(new GetSentSparksQuery("never-sent"), default);
        Assert.Empty(result.Sparks);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetStatus_Controller_ReturnsTierAsInt()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetEntitlementQuery>(), default))
            .ReturnsAsync(new GetEntitlementResponse(TestUserId, EntitlementTier.Premium, DateTime.UtcNow.AddDays(30), true));
        mediator.Setup(m => m.Send(It.IsAny<GetSparksStatusQuery>(), default))
            .ReturnsAsync(new GetSparksStatusResponse(TestUserId, 500, 1, 2, 1));

        var logger = new Mock<ILogger<BillingController>>();
        var config = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        var controller = new BillingController(mediator.Object, logger.Object, config.Object, _db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, TestUserId) }, "Test"))
            }
        };

        var rawResult = await controller.GetStatus();
        var okResult = Assert.IsType<OkObjectResult>(rawResult);
        // Use camelCase serializer options to match ASP.NET Core default behavior
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value, options);

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var hasTier = doc.RootElement.TryGetProperty("tier", out var tierElem);
        Assert.True(hasTier, "Response must contain 'tier' field");
        Assert.Equal(System.Text.Json.JsonValueKind.Number, tierElem.ValueKind);
        Assert.Equal(1, tierElem.GetInt32());
    }

    [Fact]
    public async Task SendSpark_Controller_ValidatesRecipient()
    {
        var mediator = new Mock<IMediator>();
        var logger = new Mock<ILogger<BillingController>>();
        var config = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        var controller = new BillingController(mediator.Object, logger.Object, config.Object, _db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, TestUserId) }, "Test"))
            }
        };

        var result = await controller.SendSpark(new SendSparkRequest("", null));
        Assert.IsType<BadRequestObjectResult>(result);

        var selfResult = await controller.SendSpark(new SendSparkRequest(TestUserId, "to self"));
        Assert.IsType<BadRequestObjectResult>(selfResult);

    }
}