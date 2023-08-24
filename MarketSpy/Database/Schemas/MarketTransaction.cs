using SQLite;

namespace MarketSpy.Database.Schemas;

public class MarketTransaction : DbTable
{
    public string Retainer { get; set; } = null;
    public long RetainerId { get; set; } = 0;

    /// <summary>
    ///     The name of the city where the item was sold or bought from.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    ///     Whether or not this transaction entry was from a retainer sale, or purchase.
    /// </summary>
    public bool IsSale { get; set; }

    /// <summary>
    ///     Item ID according to idk.
    /// </summary>
    [Indexed]
    public uint ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public float ValuePerItem { get; set; }
    public int TotalValue { get; set; }

    public int TotalValueAfterTax { get; set; }

    public float ValuePerItemAfterTax { get; set; }

    public float TaxPercent { get; set; }

    public int TaxPaid { get; set; }
    public bool IsHq { get; set; }
    public string Category { get; set; }
}
