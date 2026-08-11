namespace UserService.Services;

/// <summary>
/// T577 — Interface for vector embedding and similarity computation.
/// </summary>
public interface IVectorEmbeddingService
{
    Task<float[]> UpdateVectorAsync(string keycloakId, CancellationToken ct = default);
    Task<double?> CosineSimilarityAsync(string userA, string userB, CancellationToken ct = default);
}
