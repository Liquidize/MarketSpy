using System;
using Dalamud.Game.Network.Structures;

namespace MarketSpy.Internal;

/// <summary>
/// A marketbaord listing that is stored in the internal cache.
/// </summary>
public class CachedMarketListing
{
    public CachedMarketListing(DateTime cachedAt, MarketBoardCurrentOfferings.MarketBoardItemListing listing)
    {
        CachedAt = cachedAt;
        Listing = listing;
    }

    /// <summary>
    /// Time this marketboard instance was cached at.
    /// </summary>
    public DateTime CachedAt { get; set; }
    
    /// <summary>
    /// The actual listing data.
    /// </summary>
    public MarketBoardCurrentOfferings.MarketBoardItemListing Listing { get; set; }

    /// <summary>
    /// Checks if this cached instance is expired and needs deletion.
    /// </summary>
    /// <returns></returns>
    public bool IsExpired()
    {
        return CachedAt.AddMinutes(5) < DateTime.UtcNow;
    }
}
