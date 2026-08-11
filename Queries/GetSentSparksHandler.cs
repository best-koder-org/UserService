using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UserService.Data;
using UserService.DTOs;

namespace UserService.Queries;

public class GetSentSparksHandler : IRequestHandler<GetSentSparksQuery, GetSentSparksResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<GetSentSparksHandler> _logger;

    public GetSentSparksHandler(ApplicationDbContext db, ILogger<GetSentSparksHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<GetSentSparksResponse> Handle(GetSentSparksQuery request, CancellationToken ct)
    {
        var query = _db.Sparks
            .AsNoTracking()
            .Where(s => s.SenderUserId == request.UserId)
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
                null, null, null, null
            ))
            .ToListAsync(ct);

        return new GetSentSparksResponse(sparks, totalCount);
    }
}
