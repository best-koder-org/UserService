using UserService.DTOs;

namespace UserService.Services
{
    public interface IAccountDeletionService
    {
        Task<AccountDeletionResult> DeleteAccountAsync(int userProfileId, bool hardDelete = false, string? reason = null);
    }
}
