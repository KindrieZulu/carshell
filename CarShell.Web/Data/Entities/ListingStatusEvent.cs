namespace CarShell.Web.Data.Entities;

// Append-only history of listing status changes — this is what the "cars
// sold / dispatched" report reads, rather than just the listing's current
// status, so past data survives even after a listing is later archived.
public class ListingStatusEvent
{
    public Guid Id { get; set; }

    public Guid ListingId { get; set; }
    public Listing Listing { get; set; } = default!;

    public ListingStatus FromStatus { get; set; }
    public ListingStatus ToStatus { get; set; }

    // Recorded when ToStatus is Sold — kept separate from Listing.Price so a
    // later price edit never rewrites sales history.
    public decimal? SalePrice { get; set; }

    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid ChangedBy { get; set; }
    public User ChangedByUser { get; set; } = default!;
}
