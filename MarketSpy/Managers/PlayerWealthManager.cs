using System;
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
using MarketSpy.Database.Schemas;

namespace MarketSpy.Managers;

public class PlayerWealthManager
{
    private readonly ChatGui _chatGui;
    private readonly ClientState _clientState;
    private readonly Configuration _config;
    private readonly Framework _framework;
    private readonly MarketDatabase _marketDb;

    private string _currentTradePartner;

    private DateTime _lastCheckedTime = DateTime.MinValue;

    private bool hasDepositedIntoRetainer;
    private bool hasWithdrawnFromRetainer;

    public PlayerWealthManager(Plugin plugin)
    {
        _clientState = plugin.ClientState;
        _config = plugin.Configuration;
        _framework = plugin.Framework;
        _chatGui = plugin.ChatGui;
        _marketDb = plugin.MarketDb;

        _clientState.Login += OnLogin;
        _clientState.Logout += OnLogout;
        _chatGui.ChatMessage += OnChatMessage;
        _framework.Update += OnFrameworkUpdate;


        if (_clientState.IsLoggedIn) GetKnownWealth();
    }

    private KnownWealth? _currentPlayerWealth { set; get; }

    public int PlayerWealth => _currentPlayerWealth.CurrentWealth;

    private void CheckForRetainerUpdate()
    {
        // Cant update no known wealth loaded yet. sad boi hours
        if (_currentPlayerWealth == null) return;

        if (GetCurrentGil() != _currentPlayerWealth.CurrentWealth &&
            (hasDepositedIntoRetainer || hasWithdrawnFromRetainer))
        {
            UpdatePlayerGil(hasDepositedIntoRetainer
                                ? WealthChangeType.RetainerDeposit
                                : WealthChangeType.RetainerWithdraw);
            hasDepositedIntoRetainer = false;
            hasWithdrawnFromRetainer = false;
        }
    }

    private void OnFrameworkUpdate(Framework framework)
    {
        // For some reason attempting to update the players wealth immediately after depositing or withdrawing into
        // a retainer does not actually work. The gil value on the player object must need a game tick or so to update
        // so instead we will use a hacky method and check for changes this way. I have tested it like this for 
        // about 100 iterations, and the 1s wait seems to be long enough.
        var now = DateTime.UtcNow;
        if (_lastCheckedTime > now) return;
        CheckForRetainerUpdate();
        _lastCheckedTime = now.AddMilliseconds(1000);
    }

    private void GetKnownWealth()
    {
        if (_clientState.IsLoggedIn != true || _clientState.LocalPlayer == null) return;

        var playerName = _clientState.LocalPlayer.Name.TextValue;
        var knownWealth = _marketDb.GetKnownWealth((long)_clientState.LocalContentId);
        if (knownWealth != null)
        {
            _currentPlayerWealth = knownWealth;
            PluginLog.Information($"Obtained current known wealth info for player {playerName}");
        }
        else
        {
            _currentPlayerWealth =
                _marketDb.AddOrUpdateKnownWealth(playerName, (long)_clientState.LocalContentId, GetCurrentGil());
            _marketDb.AddPlayerWealthChange(playerName, _clientState.LocalContentId, GetCurrentGil(), 0,
                                            WealthChangeType.Init);
        }
    }

    private void OnLogout(object? sender, EventArgs e)
    {
        _currentPlayerWealth = null;
    }

    private void OnLogin(object? sender, EventArgs e)
    {
        GetKnownWealth();
    }

    private void OnChatMessage(
        XivChatType type, uint senderid, ref SeString sender, ref SeString message, ref bool ishandled)
    {
        if (_clientState.IsLoggedIn != true) return;

        // Type 569 is TRADE request
        if (type != XivChatType.SystemMessage && type != XivChatType.Echo && (int)type != 2105 &&
            type != XivChatType.RetainerSale && (int)type != 569) return;
        if (sender != null && sender.TextValue != "") return;
        if (message == null || string.IsNullOrEmpty(message.TextValue)) return;

        var input = message.TextValue.ToLower();

        // Trade request received store name for updating trade tables
        if (Regex.IsMatch(input, @"(.+?) wishes to trade with you\.", RegexOptions.IgnoreCase))
        {
            var payload = message.Payloads.FirstOrDefault(x => x.Type == PayloadType.Player);
            if (payload != null)
            {
                var playerPayload = (PlayerPayload)payload;
                _currentTradePartner = playerPayload.PlayerName;
            }

            return;
        }

        // Trade request sent  store name for updating trade tables
        if (Regex.IsMatch(input, @"Trade request sent to (.+?)\.", RegexOptions.IgnoreCase))
        {
            var payload = message.Payloads.FirstOrDefault(x => x.Type == PayloadType.Player);
            PluginLog.Debug("Player trade sent");
            if (payload != null)
            {
                var playerPayload = (PlayerPayload)payload;
                _currentTradePartner = playerPayload.PlayerName;
            }

            return;
        }


        // Trade
        if (Regex.IsMatch(input, @"Trade Complete\.", RegexOptions.IgnoreCase))
        {
            UpdatePlayerGil(WealthChangeType.Trade);
            return;
        }

        // Teleport
        if (Regex.IsMatch(input, @"You spent (?!0)(?:\d{1,3}(?:,\d{3}){0,2}|\d{1,9}) gil\.", RegexOptions.IgnoreCase))
        {
            UpdatePlayerGil(WealthChangeType.Teleport);
            return;
        }

        // NPC Buy
        if (Regex.IsMatch(input, @"You purchase .+? for (?!0)(?:\d{1,3}(?:,\d{3}){0,2}|\d{1,9}) gil\.",
                          RegexOptions.IgnoreCase))
        {
            UpdatePlayerGil(WealthChangeType.NPCShop);
            return;
        }

        // NPC Sell
        if (Regex.IsMatch(input, @"You sell .+? for (?!0)(?:\d{1,3}(?:,\d{3}){0,2}|\d{1,9}) gil\.",
                          RegexOptions.IgnoreCase))
        {
            UpdatePlayerGil(WealthChangeType.NPCShop);
            return;
        }

        // NPC buy back
        if (Regex.IsMatch(input, @"You buy back .+? for (?!0)(?:\d{1,3}(?:,\d{3}){0,2}|\d{1,9}) gil\.",
                          RegexOptions.IgnoreCase))
        {
            UpdatePlayerGil(WealthChangeType.NPCShop);
            return;
        }

        // Mail send
        if (Regex.IsMatch(input, @"You attach (?!0)(?:\d{1,3}(?:,\d{3}){0,2}|\d{1,9}) gil to the letter\.",
                          RegexOptions.IgnoreCase))
        {
            UpdatePlayerGil(WealthChangeType.MailSend);
            return;
        }

        // Mail receive
        if (Regex.IsMatch(input, @"(?!0)(?:\d{1,3}(?:,\d{3}){0,2}|\d{1,9}) gil taken from message\.",
                          RegexOptions.IgnoreCase))
        {
            UpdatePlayerGil(WealthChangeType.MailRecieved);
            return;
        }

        // Single market board item purchase
        if (Regex.IsMatch(input, @"You purchase (\d+) .+\.",
                          RegexOptions.IgnoreCase))
        {
            UpdatePlayerGil(WealthChangeType.Marketboard);
            return;
        }

        // Multi marketboard item purchase
        if (Regex.IsMatch(input, @"You purchase a .+\.",
                          RegexOptions.IgnoreCase))
        {
            UpdatePlayerGil(WealthChangeType.Marketboard);
            return;
        }

        if (Regex.IsMatch(input, @"(?!0)(?:\d{1,3}(?:,\d{3}){0,2}|\d{1,9}) gil is placed into the company chest\.",
                          RegexOptions.IgnoreCase))
        {
            UpdatePlayerGil(WealthChangeType.FCDeposit);
            return;
        }

        if (Regex.IsMatch(input, @"(?!0)(?:\d{1,3}(?:,\d{3}){0,2}|\d{1,9}) gil is removed from the company chest\.",
                          RegexOptions.IgnoreCase))
        {
            UpdatePlayerGil(WealthChangeType.FCWithdraw);
            return;
        }

        // Depositing into retainer
        if (Regex.IsMatch(input, @"Your gil has been safely deposited\.",
                          RegexOptions.IgnoreCase))
        {
            // UpdatePlayerGil(WealthChangeType.RetainerDeposit);
            hasDepositedIntoRetainer = true;
            return;
        }

        // Withdrawing from retainer
        if (Regex.IsMatch(input, @"Your gil has been safely withdrawn\.",
                          RegexOptions.IgnoreCase))
        {
            //  UpdatePlayerGil(WealthChangeType.RetainerWithdraw);
            hasWithdrawnFromRetainer = true;
            return;
        }

        // Am unknown change, just to be safe :)
        if (GetCurrentGil() != _currentPlayerWealth?.CurrentWealth) UpdatePlayerGil(WealthChangeType.Unknown);
    }

    public unsafe int GetCurrentGil()
    {
        return InventoryManager.Instance()->GetInventoryItemCount(1);
    }

    public void UpdatePlayerGil(WealthChangeType changeType)
    {
        if (_currentPlayerWealth == null)
        {
            PluginLog.Error("Unable to update player gil because the current known wealth was null.");
            return;
        }

        var newGil = GetCurrentGil();
        var currentGil = _currentPlayerWealth != null ? _currentPlayerWealth.CurrentWealth : 0;
        var difference = newGil - currentGil;
        if (newGil != currentGil)
        {
            var name = _currentPlayerWealth.CharacterName;
            _currentPlayerWealth = _marketDb.AddOrUpdateKnownWealth(name, _currentPlayerWealth.CharacterId, newGil);
            _marketDb.AddPlayerWealthChange(name, _clientState.LocalContentId, newGil, difference, changeType);
        }

        // If change type was also a trade, lets log that in the trade db
        if (changeType == WealthChangeType.Trade)
        {
            if (difference != 0)
            {
                _marketDb.AddPlayerTrade(_currentPlayerWealth.CharacterName, (long)_clientState.LocalContentId,
                                         _currentTradePartner, difference);
            }
        }
    }

    public void Dispose()
    {
        _chatGui.ChatMessage -= OnChatMessage;
        _clientState.Login -= OnLogin;
        _clientState.Logout -= OnLogout;
        _framework.Update -= OnFrameworkUpdate;
    }
}
