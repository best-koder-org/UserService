using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UserService.DTOs;

namespace UserService.Models
{
    /// <summary>
    /// A feedback / support ticket submitted by a user (T091).
    /// </summary>
    [Table("SupportTickets")]
    public class SupportTicket
    {
        public int Id { get; set; }

        /// <summary>Public-facing ticket reference, e.g. "TKT-3F9A2B7C".</summary>
        [Required]
        [StringLength(20)]
        public string TicketId { get; set; } = string.Empty;

        /// <summary>Keycloak user id of the submitter.</summary>
        public Guid UserId { get; set; }

        public SupportTicketCategory Category { get; set; }

        public TicketStatus Status { get; set; } = TicketStatus.Open;

        [Required]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [StringLength(4000)]
        public string Description { get; set; } = string.Empty;

        [StringLength(255)]
        public string? ContactEmail { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ResolvedAt { get; set; }
    }
}
