using System;
using System.Collections.Generic;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using MarketSpy.Database.Enums;
using MarketSpy.Database.Schemas;
using MarketSpy.Internal;
using SQLite;

namespace MarketSpy.Database;

public class MarketDatabase
{
    private readonly DataManager _dataManager;

    private readonly List<FailedTransaction> _failedTransactions;
    private readonly Framework _framework;

    private readonly SQLiteConnection db;
    private ChatGui _chatGui;
    private DateTime _nextCheckTime = DateTime.MinValue;

    private Plugin plugin;

    public MarketDatabase(Plugin plugin)
    {
        this.plugin = plugin;
        _dataManager = plugin.DataManager;
        _chatGui = plugin.ChatGui;
        _framework = plugin.Framework;

        var config = plugin.Configuration;


        _failedTransactions = new List<FailedTransaction>();

        _framework.Update += OnFrameworkUpdate;

        db = new SQLiteConnection(config.DatabasePath, false);
        db.CreateTable<MarketTransaction>();
        db.CreateTable<WealthChange>();
        db.CreateTable<KnownWealth>();
        db.CreateTable<Trade>();
    }

    private void OnFrameworkUpdate(Framework framework)
    {
        var now = DateTime.UtcNow;
        if (_nextCheckTime > now) return;

        var tempList = new List<FailedTransaction>(_failedTransactions);

        if (_failedTransactions.Count != 0)
        {
            for (var i = 0; i < tempList.Count; i++)
            {
                var transaction = tempList[i];
                if (transaction.Type == DbTransactionType.Insert)
                {
                    if (db.Insert(transaction.TransactiobObj) != 0)
                        _failedTransactions.RemoveAt(i);
                    else
                    {
                        transaction.RetryCount += 1;
                        if (transaction.RetryCount == 3)
                        {
                            _failedTransactions.RemoveAt(i);
                            continue;
                        }

                        _failedTransactions[i] = transaction;
                    }
                }
            }
        }

        _nextCheckTime = now.AddMinutes(5);
    }

    public bool IsReady()
    {
        return !db.Handle.IsClosed;
    }


    public List<KnownWealth> GetRetainerKnownWealth(long ownerId)
    {
        return db.Table<KnownWealth>().Where(x => x.OwnerId == ownerId).ToList();
    }

    public KnownWealth GetKnownWealth(long characterId)
    {
        try
        {
            return db.Table<KnownWealth>().FirstOrDefault(x => x.CharacterId == characterId);
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, "Unable to load known wealth for player.");
            return null;
        }
    }


    public KnownWealth AddOrUpdateKnownWealth(
        string characterName, long characterId, int wealth, long ownerId = 0, string owner = null)
    {
        var knownWealth = db.Table<KnownWealth>()
                            .FirstOrDefault(
                                x => x.OwnerId == ownerId && x.CharacterId ==
                                     characterId);

        var isRetainer = ownerId != 0;

        var transactionType = DbTransactionType.Insert;
        try
        {
            if (knownWealth == null)
            {
                knownWealth = new KnownWealth
                {
                    CharacterName = characterName,
                    CharacterId = characterId,
                    CurrentWealth = wealth,
                    Owner = owner,
                    OwnerId = ownerId,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                var rows = db.Insert(knownWealth);

                if (rows > 0)
                {
                    PluginLog.Information(
                        $"Added new known wealth for character {characterName} (IS_RETAINER={isRetainer}, OWNER={owner})");
                }
                else
                {
                    PluginLog.Error(
                        $"Failed to add known wealth for character {characterName} (IS_RETAINER={isRetainer}, OWNER={owner})");
                }
            }
            else
            {
                knownWealth.CurrentWealth = wealth;
                knownWealth.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                transactionType = DbTransactionType.Update;
                var rows = db.Update(knownWealth);
                if (rows > 0)
                {
                    PluginLog.Information(
                        $"Updated known wealth for character {characterName} (IS_RETAINER={isRetainer}, OWNER={owner})");
                }
                else
                {
                    PluginLog.Error(
                        $"Failed to update known wealth for character {characterName} (IS_RETAINER={isRetainer}, OWNER={owner})");
                }
            }
        }
        catch (SQLiteException ex)
        {
            PluginLog.Error(ex, "Unable to update known wealth.");
            return knownWealth;
        }

        return knownWealth;
    }

    public void AddPlayerWealthChange(
        string player, ulong playerId, int wealth, int difference, WealthChangeType changeType)
    {
        var wc = new WealthChange(player, (long)playerId, wealth, difference, changeType);
        try
        {
            var rows = db.Insert(wc);
            if (rows != 0)
            {
                PluginLog.Information(
                    $"Added new wealth change event for player. (PLAYER={player}, WEALTH={wealth}, CHANGE TYPE={changeType}).");
            }
            else
            {
                PluginLog.Warning(
                    $"Failed to add new wealth change event for player (PLAYER={player}, WEALTH={wealth}, CHANGE TYPE={changeType}).");
            }
        }
        catch (SQLiteException ex)
        {
            PluginLog.Error(ex, "Unable to add a player wealth change. Caching it to try again later.");
            _failedTransactions.Add(new FailedTransaction(wc, DbTransactionType.Insert));
        }
    }

    public void AddRetainerWealthChange(
        string retainer, ulong retainerId, string owner, ulong ownerId, int wealth, int difference,
        WealthChangeType changeType)
    {
        var wc = new WealthChange(retainer, (long)retainerId, wealth, difference, owner, (long)ownerId, changeType);
        try
        {
            var rows = db.Insert(wc);
            if (rows != 0)
            {
                PluginLog.Information(
                    $"Added new retainer wealth change event. (RETAINER={retainer}, OWNER={owner}, WEALTH={wealth}, ChangeType={changeType})");
            }
            else
            {
                PluginLog.Warning(
                    $"Failed to add retainer wealth change event.  (RETAINER={retainer}, OWNER={owner}, WEALTH={wealth}, ChangeType={changeType})");
            }
        }
        catch (SQLiteException ex)
        {
            PluginLog.Error(ex, "Unable to add a retainer wealth change. Caching it to try again later.");
            _failedTransactions.Add(new FailedTransaction(wc, DbTransactionType.Insert));
        }
    }

    public void AddMarketTransaction(
        string characterName, long characterId, string retainerName, long retainerId, string itemName, uint itemId,
        bool isHq, int quantity, int costPerItem, int totalTax, string marketName, string searchCategory)
    {
        var totalValueBeforeTax = costPerItem * quantity;
        var totalValueAfterTax = totalTax + totalValueBeforeTax;
        var transaction = new MarketTransaction
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CharacterName = characterName,
            CharacterId = characterId,
            Retainer = retainerName,
            RetainerId = retainerId,
            ItemName = itemName,
            ItemId = itemId,
            Category = searchCategory,
            Quantity = quantity,
            ValuePerItem = costPerItem,
            ValuePerItemAfterTax = (float)totalValueAfterTax / quantity,
            TaxPaid = totalTax,
            TotalValue = totalValueBeforeTax,
            TotalValueAfterTax = totalValueAfterTax,
            IsHq = isHq,
            IsSale = false,
            TaxPercent = 5.0f,
            Location = marketName
        };
        try
        {
            var rows = db.Insert(transaction);
            if (rows != 0)
                PluginLog.Information($"Added new market transaction. (PLAYER={characterName}, ITEM={itemName}).");
            else
                PluginLog.Warning("Failed to add new market purchase transaction.");
        }
        catch (SQLiteException ex)
        {
            PluginLog.Error(ex, "Unable to add a market transaction event. Caching it to try again later.");
            _failedTransactions.Add(new FailedTransaction(transaction, DbTransactionType.Insert));
        }
    }

    public void AddMarketSaleTransaction(
        string characterName, long characterId, string retainerName, long retainerId, string itemName, uint itemId,
        int quantity, int profit, float salesTax, bool isHqItem,
        string searchCategory, string marketName)
    {
        var profitPerItem = profit / quantity;
        var totalValueBeforeTax = Helpers.CalculateBeforeTaxValue(profit, salesTax, true);
        var valuePerItemBeforeTax = totalValueBeforeTax / quantity;
        var taxPaid = totalValueBeforeTax - profit;
        var transaction = new MarketTransaction
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CharacterName = characterName,
            CharacterId = characterId,
            ItemName = itemName,
            ItemId = itemId,
            Quantity = quantity,
            ValuePerItemAfterTax = profitPerItem,
            TotalValueAfterTax = profit,
            TotalValue = totalValueBeforeTax,
            ValuePerItem = valuePerItemBeforeTax,
            Category = searchCategory,
            IsHq = isHqItem,
            IsSale = true,
            TaxPercent = salesTax,
            Location = marketName,
            TaxPaid = taxPaid,
            Retainer = retainerName,
            RetainerId = retainerId
        };

        try
        {
            var rows = db.Insert(transaction);
            if (rows != 0)
            {
                PluginLog.Information(
                    $"Added new market sale  transaction. (PLAYER={characterName}, ITEM={itemName}).");
            }
            else
                PluginLog.Warning("Failed to add new market sale transaction.");
        }
        catch (SQLiteException ex)
        {
            PluginLog.Error(ex, "Unable to add a retainer sale event. Caching it to try again later.");
            _failedTransactions.Add(new FailedTransaction(transaction, DbTransactionType.Insert));
        }
    }

    public void AddPlayerTrade(string characterName, long characterId, string tradingPlayer, int netReceived)
    {
        var trade = new Trade
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CharacterName = characterName,
            CharacterId = characterId,
            TradePartner = tradingPlayer,
            NetReceived = netReceived
        };
        try
        {
            var rows = db.Insert(trade);
            if (rows != 0)
            {
                PluginLog.Information(
                    $@"Trade complete detected, and gil was given or taken. A new trade info was added to the database. (PLAYER={characterName}, TRADE PARTNER={tradingPlayer}).");
            }
            else
            {
                PluginLog.Warning(
                    $@"Trade complete detected, and gil was given or taken. But new trade info was unable to be added to the database. (PLAYER={characterName}, TRADE PARTNER={tradingPlayer}).");
            }
        }
        catch (SQLiteException ex)
        {
            PluginLog.Error(ex, "Unable to add a player trade event. Caching it to try again later.");
            _failedTransactions.Add(new FailedTransaction(trade, DbTransactionType.Insert));
        }
    }

    public TableQuery<T> GetTable<T>() where T : new()
    {
        return db.Table<T>();
    }

    public void Dipoose()
    {
        // Close connection and free up the memory used by the db
        db.Close();
        db.Dispose();
        _framework.Update -= OnFrameworkUpdate;
    }
}
