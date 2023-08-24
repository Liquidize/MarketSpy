using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using MarketSpy.Database;
using MarketSpy.Database.Enums;
using MarketSpy.Internal;

namespace MarketSpy.Managers;

/// <summary>
///     Class to handle retainer wealth management, retrieves known retainer wealth, and updates
///     retainer wealth when needed.
/// </summary>
public class RetainerWealthManager
{
    private readonly ChatGui _chatGui;
    private readonly ClientState _clientState;
    private readonly Configuration _config;
    private readonly Dictionary<long, int> _currentRetainerGIl = new();
    private readonly Framework _framework;
    private readonly MarketDatabase _marketDb;

    private readonly Regex _pluralItemSaleRegex = new(
        @"The (\d+) ([\s\S]*?|.+?) you put up for sale in the (.+?) markets ha(?:s|ve) sold for ((?!0)(?:\d{1,3}(?:,\d{3}){0,2}|\d{1,9})) gil \(after fees\)\.",
        RegexOptions.IgnoreCase);

    private readonly Regex _singularItemSaleRegex = new(
        @"The (.+?) you put up for sale in the (.+?) markets has sold for ((?!0)(?:\d{1,3}(?:,\d{3}){0,2}|\d{1,9})) gil \(after fees\)\.",
        RegexOptions.IgnoreCase);

    private readonly MarketTaxManager _taxManager;
    private DateTime _lastCheckedTime = DateTime.MinValue;

    public RetainerWealthManager(Plugin plugin)
    {
        _taxManager = plugin.TaxManager;
        _marketDb = plugin.MarketDb;
        _config = plugin.Configuration;
        _clientState = plugin.ClientState;
        _framework = plugin.Framework;
        _chatGui = plugin.ChatGui;

        _clientState.Login += OnLogin;
        _chatGui.ChatMessage += OnChatMessage;

        Enable();
        Refresh();
    }

    public bool Enabled { get; private set; }
    
    private void OnChatMessage(
        XivChatType type, uint senderid, ref SeString sender, ref SeString message, ref bool ishandled)
    {
        if (_clientState.IsLoggedIn != true) return;
        if (type != XivChatType.SystemMessage && type != XivChatType.Echo && (int)type != 2105 &&
            type != XivChatType.RetainerSale) return;
        if (sender != null && sender.TextValue != "") return;
        if (message == null || string.IsNullOrEmpty(message.TextValue)) return;
        var input = message.TextValue.ToLower();

        // Check for a market sale of one whole item :')
        if (_singularItemSaleRegex.IsMatch(input))
        {
            var match = _singularItemSaleRegex.Match(input);
            var payload = message.Payloads.FirstOrDefault(t => t.Type == PayloadType.Item);
            if (payload == null)
            {
                PluginLog.Warning("Can't add new market sale transaction, no item payload in the message.");
                return;
            }

            var player = _clientState?.LocalPlayer?.Name.TextValue;
            var itemPayload = (ItemPayload)payload;
            var itemName = itemPayload.Item?.Name.RawString;
            var itemId = itemPayload.ItemId;
            var isHq = itemPayload.IsHQ;
            var amount = 1;
            var searchCategory = itemPayload.Item?.ItemSearchCategory?.Value;
            var market = match.Groups[2].Value;
            var profit = Convert.ToInt32(match.Groups[3].Value.Replace(",", ""));
            var salesTax = _taxManager.GetTaxRate(market);

            _marketDb.AddMarketSaleTransaction(player, (long)_clientState.LocalContentId, null, 0, itemName, itemId,
                                               amount, profit, salesTax, isHq,
                                               searchCategory.Name.RawString, market);
            return;
        }

        // Check for a market sale of <x> number of items
        if (_pluralItemSaleRegex.IsMatch(input))
        {
            var match = _pluralItemSaleRegex.Match(input);
            var payload = message.Payloads.FirstOrDefault(t => t.Type == PayloadType.Item);
            if (payload == null)
            {
                PluginLog.Warning("Can't add new market sale transaction, no item payload in the message.");
                return;
            }

            var player = _clientState?.LocalPlayer?.Name.TextValue;
            var itemPayload = (ItemPayload)payload;
            var itemName = itemPayload.Item?.Name.RawString;
            var itemId = itemPayload.ItemId;
            var isHq = itemPayload.IsHQ;
            var amount = Convert.ToInt32(match.Groups[1].Value);
            var searchCategory = itemPayload.Item?.ItemSearchCategory?.Value;
            var market = match.Groups[3].Value;
            var profit = Convert.ToInt32(match.Groups[4].Value.Replace(",", ""));
            var salesTax = _taxManager.GetTaxRate(market);

            _marketDb.AddMarketSaleTransaction(player, (long)_clientState.LocalContentId, null, 0, itemName, itemId,
                                               amount, profit, salesTax, isHq,
                                               searchCategory.Name.RawString, market);
        }
    }

    private void OnLogin(object? sender, EventArgs e)
    {
        Refresh();
    }

    public void Refresh()
    {
        _currentRetainerGIl.Clear();

        if (_marketDb == null || _clientState.LocalPlayer == null || _clientState.IsLoggedIn != true) return;

        var knownWealths = _marketDb.GetRetainerKnownWealth((long)_clientState.LocalContentId);

        foreach (var wealth in knownWealths) _currentRetainerGIl.Add(wealth.CharacterId, wealth.CurrentWealth);

        PluginLog.Information(
            $"Refreshed Retainer Cache for {_clientState.LocalPlayer.Name.TextValue} total retainers in cache is {_currentRetainerGIl.Count}");
    }

    public void Enable()
    {
        Enabled = true;
        Refresh();
        _framework.Update += _frameworkOnUpdate;
    }

    public void Disable()
    {
        Enabled = false;
        _framework.Update -= _frameworkOnUpdate;
        _clientState.Login -= OnLogin;
        _chatGui.ChatMessage -= OnChatMessage;
    }

    public void Dispose()
    {
        _currentRetainerGIl.Clear();
        Disable();
    }

    private unsafe void UpdateRetainers()
    {
        if (_clientState.LocalPlayer == null || RetainerManager.Instance()->Ready != 1)
            return;

        var maxRetainers = RetainerManager.Instance()->GetRetainerCount();
        for (var i = 0; i < maxRetainers; i++)
        {
            var retainer = RetainerManager.Instance()->GetRetainerBySortedIndex((uint)i);
            if (retainer != null)
            {
                var retainerName = Helpers.RetainerNameFromAddress(retainer->Name).TextValue;
                var ownerName = _clientState.LocalPlayer.Name.TextValue;
                var retainerGil = (int)retainer->Gil;
                var retainerId = retainer->RetainerID;

                if (_currentRetainerGIl.ContainsKey((long)retainer->RetainerID) != true)
                {
                    _marketDb.AddOrUpdateKnownWealth(retainerName,
                                                     (long)retainerId,
                                                     retainerGil, (long)_clientState.LocalContentId,
                                                     ownerName);
                    _currentRetainerGIl.Add((long)retainer->RetainerID, retainerGil);

                    _marketDb.AddRetainerWealthChange(retainerName, retainerId, ownerName, _clientState.LocalContentId,
                                                      retainerGil, 0,
                                                      WealthChangeType.Unknown);
                    PluginLog.Information(
                        $"Added new retainer to cache, and updated the database. (RETAINER={retainerName}, OWNER={ownerName}).");
                }
                else
                {
                    var current = _currentRetainerGIl[(long)retainer->RetainerID];
                    if (current != retainer->Gil)
                    {
                        _marketDb.AddOrUpdateKnownWealth(Helpers.RetainerNameFromAddress(retainer->Name).TextValue,
                                                         (long)retainerId, retainerGil,
                                                         (long)_clientState.LocalContentId,
                                                         _clientState.LocalPlayer.Name.TextValue);
                        _currentRetainerGIl[(long)retainer->RetainerID] = retainerGil;

                        var diffrence = retainer->Gil - current;

                        _marketDb.AddRetainerWealthChange(Helpers.RetainerNameFromAddress(retainer->Name).TextValue,
                                                          retainerId, ownerName, _clientState.LocalContentId,
                                                          (int)retainer->Gil,
                                                          (int)diffrence, WealthChangeType.Unknown);
                    }
                }
            }
        }
    }

    private void _frameworkOnUpdate(Framework framework)
    {
        var now = DateTime.UtcNow;
        if (_lastCheckedTime > now) return;

        UpdateRetainers();
        _lastCheckedTime = now.AddMilliseconds(5000);
    }
}
