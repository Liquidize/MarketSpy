namespace MarketSpy.Database.Schemas;

public class Trade : DbTable
{
    /// <summary>
    ///     Who we are trading with
    /// </summary>
    public string TradePartner { get; set; }


    /// <summary>
    ///     Net amount of gil received from the trade. This is calculated from the difference from before-trade gil and
    ///     after trade gil. We can't determine how much was given or taken from the trade individually.
    ///     Or at least, I am too lazy to do it :).
    /// </summary>
    public int NetReceived { get; set; }
}
