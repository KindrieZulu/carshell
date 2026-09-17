namespace CarShell.Web.Data.Entities;

public enum SubscriptionStatus { Active, PastDue, Canceled }

// Phase 2 only — the schema carries it from day one so opening self-serve
// seller registration later is a new table plus an authorization check, not
// a migration on the core listings/users tables.
public class SellerSubscription
{
    public Guid Id { get; set; }

    public Guid SellerId { get; set; }
    public User Seller { get; set; } = default!;

    public string StripeSubscriptionId { get; set; } = default!;
    public SubscriptionStatus Status { get; set; }
    public DateTimeOffset CurrentPeriodEnd { get; set; }
}
