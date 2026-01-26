namespace UserService.DTOs
{
    public class AccountDeletionRequest
    {
        public bool HardDelete { get; set; } = false; // Default to soft delete
        public string? Reason { get; set; } // Optional reason for analytics
        public string? ConfirmationToken { get; set; } // For confirmation workflow
    }

    public class AccountDeletionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public AccountDeletionSummary Summary { get; set; } = new();
    }

    public class AccountDeletionSummary
    {
        public bool ProfileDeleted { get; set; }
        public int PhotosDeleted { get; set; }
        public int MatchesDeleted { get; set; }
        public int MessagesDeleted { get; set; }
        public int SwipesDeleted { get; set; }
        public int SafetyReportsDeleted { get; set; }
        public int BlocksDeleted { get; set; }
        public List<string> Errors { get; set; } = new();
        public DateTime DeletedAt { get; set; }
    }
}
