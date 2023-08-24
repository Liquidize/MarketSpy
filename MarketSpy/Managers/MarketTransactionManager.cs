using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.Game.Network;
using Dalamud.Game.Network.Structures;
using Lumina.Excel.GeneratedSheets;
using MarketSpy.Database;
using MarketSpy.Internal;

namespace MarketSpy.Managers;

public class MarketTransactionManager
{
    private readonly ChatGui _chatGui;
    private readonly ClientState _clientState;
    private readonly Configuration _config;
    private readonly DataManager _dataManager;
    private readonly Framework _framework;
    private readonly List<CachedMarketListing> _listingCache = new();
    private readonly MarketDatabase _marketDb;
    private readonly GameNetwork _network;
    private DateTime _lastCheckedTime = DateTime.MinValue;

    private MarketBoardPurchaseHandler? _marketBoardPurchaseHandler;

    public MarketTransactionManager(Plugin plugin)
    {
        _clientState = plugin.ClientState;
        _config = plugin.Configuration;
        _framework = plugin.Framework;
        _chatGui = plugin.ChatGui;
        _marketDb = plugin.MarketDb;
        _network = plugin.Network;
        _dataManager = plugin.DataManager;

        _network.NetworkMessage += OnNetworkMessage;
        _framework.Update += OnFrameworkUpdate;
        _clientState.Login += OnLogin;
        _clientState.Logout += OnLogout;
    }

    private string _currentLocation { get; set; }

    private void OnLogout(object? sender, EventArgs e)
    {
        // We dont need to be active if we aren't logged in.
        _network.NetworkMessage -= OnNetworkMessage;
        _framework.Update -= OnFrameworkUpdate;
        _listingCache.Clear();
    }

    private void OnLogin(object? sender, EventArgs e)
    {
        _network.NetworkMessage += OnNetworkMessage;
        _framework.Update += OnFrameworkUpdate;
    }

    private void CleanupListingCache()
    {
        foreach (var listing in _listingCache)
            if (listing.IsExpired())
                _listingCache.Remove(listing);
    }

    private void OnFrameworkUpdate(Framework framework)
    {
        var now = DateTime.UtcNow;
        if (_lastCheckedTime > now) return;
        CleanupListingCache();
        _lastCheckedTime = now.AddMinutes(5);
    }


    private void OnNetworkMessage(
        IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction)
    {
        if (_dataManager.IsDataReady != true) return; // We sadge bois

        if (direction == NetworkMessageDirection.ZoneUp)
        {
            if (opCode == _dataManager.ClientOpCodes["MarketBoardPurchaseHandler"])
            {
                _marketBoardPurchaseHandler = MarketBoardPurchaseHandler.Read(dataPtr);
                return;
            }

            return;
        }

        if (opCode == _dataManager.ServerOpCodes["MarketBoardPurchase"])
        {
            if (_marketBoardPurchaseHandler == null)
                return;

            var purchase = MarketBoardPurchase.Read(dataPtr);

            var sameQty = purchase.ItemQuantity == _marketBoardPurchaseHandler.ItemQuantity;
            var itemMatch = purchase.CatalogId == _marketBoardPurchaseHandler.CatalogId;
            var itemMatchHq = purchase.CatalogId == _marketBoardPurchaseHandler.CatalogId + 1_000_000;

            // Transaction succeeded
            if (sameQty && (itemMatch || itemMatchHq))
            {
                var offer = _listingCache.FirstOrDefault(
                    x => x.Listing.ListingId == _marketBoardPurchaseHandler.ListingId);
                // In order to properly add the transaction we need a valid offer in the cache
                if (offer != null)
                {
                    var listing = offer.Listing;
                    var item = _dataManager.Excel.GetSheet<Item>()
                                           .FirstOrDefault(x => x.RowId == listing.CatalogId);
                    // We need proper item information so we need a valid item.
                    if (item != null)
                    {
                        var itemName = item.Name.RawString;
                        var category = item.ItemSearchCategory.Value.Name.RawString;
                        var playerName = _clientState.LocalPlayer.Name.TextValue;

                        var zone = _dataManager.Excel.GetSheet<TerritoryType>()
                                               .FirstOrDefault(
                                                   x => x.RowId == _clientState.TerritoryType).PlaceName.Value.Name
                                               .RawString;
                        _marketDb.AddMarketTransaction(playerName, (long)_clientState.LocalContentId,
                                                       listing.RetainerName, (long)listing.RetainerId, itemName,
                                                       listing.CatalogId, listing.IsHq, (int)listing.ItemQuantity,
                                                       (int)listing.PricePerUnit, (int)listing.TotalTax, zone,
                                                       category);
                    }
                }
            }

            _marketBoardPurchaseHandler = null;
        }

        if (opCode == _dataManager.ServerOpCodes["MarketBoardOfferings"])
        {
            var offerings = MarketBoardCurrentOfferings.Read(dataPtr);
            foreach (var listing in offerings.ItemListings)
            {
                var entry = _listingCache.FirstOrDefault(x => x.Listing.ListingId == listing.ListingId);
                if (entry != null)
                {
                    var cachedListing = entry.Listing;
                    if (cachedListing.ItemQuantity != listing.ItemQuantity ||
                        cachedListing.PricePerUnit != listing.PricePerUnit ||
                        cachedListing.CatalogId != listing.CatalogId)
                        _listingCache.Remove(entry);
                }

                _listingCache.Add(new CachedMarketListing(DateTime.UtcNow, listing));
            }
        }
    }

    public void Dispose()
    {
        _network.NetworkMessage -= OnNetworkMessage;
        _framework.Update -= OnFrameworkUpdate;
        _clientState.Login -= OnLogin;
        _clientState.Logout -= OnLogout;
    }
}
