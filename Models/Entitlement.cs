namespace UserService.Models;

/// User's premium entitlement tier.
public enum EntitlementTier { Free, Premium }

/// Premium subscription / entitlement record for a user.
[System.ComponentModel.DataAnnotations.Schema.Table("Entitlements")]
public class Entitlement
{
    public int Id { get; set; }

    /// Keycloak user ID.
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string UserId { get; set; } = string.Empty;

    public EntitlementTier Tier { get; set; } = EntitlementTier.Free;

    /// Null for Free tier (no expiry); for Premium, when the subscription ends.
    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// A single entry in the Sparks virtual-currency ledger.
[System.ComponentModel.DataAnnotations.Schema.Table("SparksLedger")]
public class SparksLedgerEntry
{
    public long Id { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string UserId { get; set; } = string.Empty;

    /// Positive = credit (earn/buy), negative = debit (spend).
    public int Delta { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string Reason { get; set; } = string.Empty;

    /// Running balance after this entry was applied. Never negative.
    public int BalanceAfter { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// A Spark sent from one user to another (Feeld-style ping / Hinge-style rose).
[System.ComponentModel.DataAnnotations.Schema.Table("SparkRecords")]
public class SparkRecord
{
    public long Id { get; set; }

    /// Keycloak user ID of the sender.
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string SenderUserId { get; set; } = string.Empty;

    /// Keycloak user ID of the recipient.
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string RecipientUserId { get; set; } = string.Empty;

    /// Optional message attached to the spark (max 200 chars).
    [System.ComponentModel.DataAnnotations.StringLength(200)]
    public string? Message { get; set; }

    /// Whether the recipient has viewed this spark.
    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
