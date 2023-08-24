using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using Newtonsoft.Json.Linq;

namespace MarketSpy.Managers;

public class MarketTaxManager
{
    private readonly ChatGui _chatGui;
    private readonly ClientState _clientState;

    private readonly Dictionary<string, float> _currentTaxRates = new();

    public MarketTaxManager(ClientState clientState, ChatGui chat)
    {
        _clientState = clientState;
        _clientState.Login += OnLogin;
        _clientState.Logout += OnLogout;

        _chatGui = chat;
        _chatGui.ChatMessage += OnChatMessage;
    }

    public string CurrentWorld { private set; get; }

    private void OnChatMessage(
        XivChatType type, uint senderid, ref SeString sender, ref SeString message, ref bool ishandled)
    {
        if (_clientState.IsLoggedIn != true) return;
        if (type != XivChatType.SystemMessage) return;
        if (sender != null && sender.TextValue != "") return;
        if (message == null || string.IsNullOrEmpty(message.TextValue)) return;
        var regExp = new Regex(@"You successfully travel to (.+?)\.", RegexOptions.IgnoreCase);
        var input = message.TextValue.ToLower();
        var match = regExp.Match(input);
        if (match.Success) RefreshTaxRates(match.Groups[1].Value);
    }

    private void OnLogout(object? sender, EventArgs e)
    {
        CurrentWorld = string.Empty;
        _currentTaxRates.Clear();
    }

    private void OnLogin(object? sender, EventArgs e)
    {
        if (_clientState.IsLoggedIn != true || _clientState.LocalPlayer == null) return;
        if (_clientState.LocalPlayer.CurrentWorld?.GameData != null)
            RefreshTaxRates(_clientState.LocalPlayer.CurrentWorld.GameData.Name.RawString);
    }

    public float GetTaxRate(string cityName)
    {
        return _currentTaxRates.ContainsKey(cityName.ToLower()) ? _currentTaxRates[cityName.ToLower()] : 0f;
    }

    public void RefreshTaxRates(string newWorld)
    {
        PluginLog.Debug("Refreshing taxes");

        if (string.IsNullOrEmpty(newWorld))
        {
            PluginLog.Warning("Unable to update tax rates, world object passed to function is null.");
            return;
        }

        using (var client = new HttpClient())
        {
            try
            {
                var response = client.GetStringAsync($"https://universalis.app/api/v2/tax-rates?world={newWorld}")
                                     .Result;
                var json = JObject.Parse(response);

                // Check for 404 or other error.
                // TODO: Inform the player in a better way?
                if (json.HasValues != true ||
                    (json["status"] != null && Convert.ToInt32(json["status"]?.ToString()) == 404))
                {
                    PluginLog.Error(
                        $"Unable to parse market tax rates from Universalis. Returned JSON is empty or world not found. (WORLD={newWorld})");
                    return;
                }

                _currentTaxRates.Clear();

                foreach (var prop in json)
                {
                    var keyLower = prop.Key.ToLower();
                    if (_currentTaxRates.ContainsKey(prop.Key) != true)
                    {
                        _currentTaxRates.Add(keyLower, Convert.ToSingle(prop.Value?.ToString()));
                        PluginLog.Information("Added new taxrate: " + _currentTaxRates[keyLower]);
                    }
                }

                CurrentWorld = newWorld;
            }
            catch (HttpRequestException ex)
            {
                // We sad bois
                PluginLog.Error(ex, "Failed to update the market tax rates due to HTTP Exception.");
            }
        }
    }

    public void Dispose()
    {
        _clientState.Login -= OnLogin;
        _clientState.Logout -= OnLogout;
        _chatGui.ChatMessage -= OnChatMessage;
    }
}
