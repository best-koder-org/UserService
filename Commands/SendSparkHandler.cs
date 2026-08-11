using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UserService.Common;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;

namespace UserService.Commands;

/// <summary>
/// Handles sending a Spark from one user to another.
/// Deducts a Spark from the sender, creates a SparkRecord, and (optionally) notifies the recipient.
/// </summary>
public class SendSparkHandler : IRequestHandler<SendSparkCommand, SendSparkResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<SendSparkHandler> _logger;

    public SendSparkHandler(ApplicationDbContext db, IMediator mediator, ILogger<SendSparkHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<SendSparkResponse> Handle(SendSparkCommand request, CancellationToken ct)
    {
        // 1. Deduct a Spark from the sender
        var spendResult = await _mediator.Send(
            new SpendSparkCommand(request.SenderUserId, "spark_ping"), ct);

        if (!spendResult.Success)
            return new SendSparkResponse(false, spendResult.NewBalance, spendResult.DailyRemaining,
                spendResult.Error ?? "No Sparks available", null);

        // 2. Create SparkRecord
        var record = new SparkRecord
        {
            SenderUserId = request.SenderUserId,
            RecipientUserId = request.RecipientUserId,
            Message = request.Message?.Length > 200 ? request.Message[..200] : request.Message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Sparks.Add(record);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Spark sent: Sender={Sender} Recipient={Recipient} RecordId={RecordId}",
            request.SenderUserId, request.RecipientUserId, record.Id);

        return new SendSparkResponse(true, spendResult.NewBalance, spendResult.DailyRemaining, null, record.Id);
    }
}
