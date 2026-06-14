using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UserService.Data;
using UserService.DTOs;

namespace UserService.Queries;

public class GetReceivedSparksHandler : IRequestHandler<GetReceivedSparksQuery, GetReceivedSparksResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<GetReceivedSparksHandler> _logger;

    public GetReceivedSparksHandler(ApplicationDbContext db, ILogger<GetReceivedSparksHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<GetReceivedSparksResponse> Handle(GetReceivedSparksQuery request, CancellationToken ct)
    {
        var query = _db.Sparks
            .AsNoTracking()
            .Where(s => s.RecipientUserId == request.UserId)
            .OrderByDescending(s => s.CreatedAt);

        var totalCount = await query.CountAsync(ct);

        var sparks = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new SparkRecordDto(
                s.Id,
                s.SenderUserId,
                s.RecipientUserId,
                s.Message,
                s.IsRead,
                s.CreatedAt,
                null, // SenderDisplayName — resolved by client via UserService
                null, // SenderPhotoUrl
                null, // RecipientDisplayName
                null  // RecipientPhotoUrl
            ))
            .ToListAsync(ct);

        return new GetReceivedSparksResponse(sparks, totalCount);
    }
}
