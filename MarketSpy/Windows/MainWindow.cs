using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using ImPlotNET;
using MarketSpy.Database;
using MarketSpy.Database.Enums;
using MarketSpy.Database.Schemas;

namespace MarketSpy.Windows;

public class MainWindow : Window, IDisposable
{
    private int _currentSelectedCharacter;
    private int _currentSelectGraphOption;
    private bool _useLocalTime;

    private bool _usePlotLines = true;
    private bool _usePlotMarkers = true;
    private bool _useShadedPlotLines;


    public MainWindow(Plugin plugin) : base(
        "Market Spy", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        _config = plugin.Configuration;
        _db = plugin.MarketDb;
        _clientState = plugin.ClientState;

        _useLocalTime = _config.GetOrAddGraphOption("useLocalTime", true);
        _usePlotLines = _config.GetOrAddGraphOption("usePlotLines", true);
        _usePlotMarkers = _config.GetOrAddGraphOption("usePlotMarkers", true);
        _useShadedPlotLines = _config.GetOrAddGraphOption("useShadedPlotLines", false);
    }

    private ClientState _clientState { get; init; }
    private Configuration _config { get; init; }

    private MarketDatabase _db { get; init; }

    public void Dispose() { }

    public override void Draw()
    {
        if (_db == null || _db.IsReady() != true) return;

        var scale = ImGui.GetIO().FontGlobalScale;

        // Window Setup
        ImGui.SetNextWindowSize(new Vector2(800, 600) * scale, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(350, 225) * scale, new Vector2(10000, 10000) * scale);

        ImGui.Spacing();

        if (_clientState.IsLoggedIn != true)
        {
            ImGui.Text("Please log into a character to view wealth information.");
            return;
        }


        if (ImGui.BeginTabBar("tabBar"))
        {
            if (ImGui.BeginTabItem("Wealth Graphs##wealthDataTab"))
            {
                DrawWealthGraphTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    public void DrawWealthGraphTab()
    {
        var distinctCharacters = _db.GetTable<WealthChange>().Where(x => x.Owner == null)
                                    .DistinctBy(x => x.CharacterName)
                                    .Select(x => x.CharacterName).ToArray();


        var selectedChar = _currentSelectedCharacter;

        ImGui.SetNextItemWidth(150);
        if (ImGui.Combo("Character", ref selectedChar, distinctCharacters,
                        distinctCharacters.Length))
            _currentSelectedCharacter = selectedChar;


        ImGui.SetNextItemWidth(150);
        string[] graphOptions = { "Wealth over Time", "Wealth Change by Type" };
        ImGui.Combo("Graph Type", ref _currentSelectGraphOption, graphOptions, graphOptions.Length);

        var selected = distinctCharacters[_currentSelectedCharacter];
        var childScale = new Vector2(ImGui.GetWindowWidth() - 15, ImGui.GetWindowHeight() - 100);


        // var valuesY = sorted.Select(x => (double)Math.Round(x.Wealth / divisionFactor, 2)).ToArray();

        if (ImGui.Checkbox("Use Local Time", ref _useLocalTime))
        {
            ImPlot.GetStyle().UseLocalTime = _useLocalTime;
            _config.SetGraphOption("useLocalTime", _useLocalTime);
        }

        if (_currentSelectGraphOption == 0) DrawWealthOverTimeGraph(selected, false);
        if (_currentSelectGraphOption == 1) DrawWealthChangeByTypeGraph(selected);
        //  if (_currentSelectGraphOption == 2) DrawTradeGainLossByPlayer(selected);
        //  if (_currentSelectGraphOption == 2) DrawWealthOverTimeGraph(selected, true);
    }

    private void DrawTradeGainLossByPlayer(string selectedCharacter)
    {
        var data = _db.GetTable<Trade>().Where(x => x.CharacterName == selectedCharacter && x.TradePartner != null);

        var average = data.Average(x => x.NetReceived);
        var divisionFactor = CalculateDivisionFactor(average);

        var partnerNetGainLoss = data.GroupBy(x => x.TradePartner)
                                     .ToDictionary(group => group.Key,
                                                   group => (float)Math.Round(group.Sum(
                                                                                  trade => trade.NetReceived) /
                                                                              divisionFactor, 2));

        var partners = partnerNetGainLoss.Keys.ToArray();
        var values = partnerNetGainLoss.Values.ToArray();

        if (ImPlot.BeginPlot("##tradeByPartner", ImGui.GetContentRegionAvail()))
        {
            ImPlot.SetupAxesLimits(0, partners.Length, values.Min(), values.Max());

            for (var i = 0; i < partners.Length; i++)
            {
                var x = (float)i;
                ImPlot.PlotBars(partners[i], ref x, ref values[i], 1, 0.9f);
            }

            ImPlot.EndPlot();
        }
    }

    private void DrawWealthChangeByTypeGraph(string selectedCharacter)
    {
        var data = _db.GetTable<WealthChange>()
                      .Where(x => x.CharacterName == selectedCharacter && x.ChangeType != WealthChangeType.Init)
                      .ToList();

        var distinctTypes = data.DistinctBy(x => x.ChangeType).Select(x => x.ChangeType).ToArray();
        var changeTypeData = new Dictionary<string, float>();

        for (var i = 0; i < distinctTypes.Count(); i++)
        {
            var type = distinctTypes[i];
            var typePercent = (float)data.Where(x => x.ChangeType == type).Count() / data.Count();
            changeTypeData.Add($"{type.ToString()} ({Math.Round(typePercent * 100, 2)}%)", typePercent);
        }


        var windowSize = ImGui.GetWindowSize();
        var changeTypeValues = changeTypeData.Values.ToArray();
        var spaceAvailable = ImGui.GetContentRegionAvail();
        if (ImPlot.BeginPlot("##wealthTypeGraph", spaceAvailable))
        {
            ImPlot.SetupAxes(null, null, ImPlotAxisFlags.NoDecorations, ImPlotAxisFlags.NoDecorations);
            ImPlot.SetupAxesLimits(0, 1, 0, 1);
            ImPlot.PlotPieChart(changeTypeData.Keys.ToArray(), ref changeTypeValues[0], changeTypeValues.Length, 0.5,
                                0.5, 0.4, "%.2f", 90f);

            ImPlot.EndPlot();
        }
    }

    private int FindIndex(double[] timeValues, double time)
    {
        var left = 0;
        var right = timeValues.Length - 1;

        while (left <= right)
        {
            var mid = left + ((right - left) / 2);
            var midValue = timeValues[mid];

            if (Math.Abs(midValue - time) <= 0.5)
                return mid; // Found a close match within half a second
            if (midValue < time)
                left = mid + 1; // Adjust left boundary
            else
                right = mid - 1; // Adjust right boundary
        }

        return -1; // No close match within half a second found
    }

    private void DrawWealthOverTimeGraph(string selectedCharacter, bool isWealthDifference)
    {
        var data = _db.GetTable<WealthChange>()
                      .Where(x => x.CharacterName == selectedCharacter ||
                                  (x.Owner != null && x.Owner == selectedCharacter)).ToList();


        var divisionFactor =
            CalculateDivisionFactor(data.Average(x => isWealthDifference ? x.WealthDifference : x.Wealth));
        var unitName = GetUnitName(divisionFactor);

        var sorted = data.OrderBy(x => x.ChangeTime).ToList();
        var min = sorted.Min(x => isWealthDifference ? x.WealthDifference : x.Wealth);
        var max = sorted.Max(x => isWealthDifference ? x.WealthDifference : x.Wealth);

        var valuesY = sorted
                      .Select(x => Math.Round((isWealthDifference ? x.WealthDifference : x.Wealth) / divisionFactor, 2))
                      .ToArray();
        var valuesX = sorted.Select(x => (double)x.Timestamp).ToArray();

        var spaceAvailable = ImGui.GetContentRegionAvail();

        if (ImPlot.BeginPlot("##wealthGraph", spaceAvailable))
        {
            var yLabel = isWealthDifference ? $"Wealth Difference ({unitName})" : $"Wealth ({unitName})";
            ImPlot.SetupAxes("Time", yLabel);
            ImPlot.SetupAxisScale(ImAxis.X1, ImPlotScale.Time);
            ImPlot.SetNextAxesLimits(valuesX.Min(), valuesX.Max(), valuesY.Min(), valuesY.Max(), ImPlotCond.Once);

            var halfWidth = valuesX.Length > 1 ? (valuesX[1] - valuesX[0]) * 0.9f : 0.9f;

            if (ImPlot.IsPlotHovered())
            {
                var mouse = ImPlot.GetPlotMousePos();
                var idx = FindIndex(valuesX, mouse.x);

                if (idx != -1)
                {
                    var toolL = ImPlot.PlotToPixels(mouse.x - (halfWidth * 1.5), mouse.y).X;
                    var toolR = ImPlot.PlotToPixels(mouse.x + (halfWidth * 1.5), mouse.y).X;
                    var toolT = ImPlot.GetPlotPos().Y;
                    var toolB = ImPlot.GetPlotSize().Y;
                    var drawList = ImPlot.GetPlotDrawList();
                    ImPlot.PushPlotClipRect();
                    //drawList.AddRectFilled(new Vector2(toolL, toolT), new Vector2(toolR, toolB),
                    //                        ImGui.GetColorU32(new Vector4(128, 128, 128, 64)));
                    ImPlot.PopPlotClipRect();

                    ImGui.BeginTooltip();
                    var dateTime = DateTimeOffset.FromUnixTimeSeconds(sorted[idx].Timestamp).ToString("G");
                    if (_useLocalTime)
                        dateTime = DateTimeOffset.FromUnixTimeSeconds(sorted[idx].Timestamp).ToLocalTime()
                                                 .ToString("G");
                    ImGui.Text($"Time: {dateTime}");
                    ImGui.Text($"Wealth: {sorted[idx].Wealth.ToString("N")}");
                    ImGui.Text($"Difference: {sorted[idx].WealthDifference.ToString("N")}");
                    ImGui.Text($"Change Type: {sorted[idx].ChangeType.ToString()}");
                    ImGui.EndTooltip();
                }
            }

            PlotCharacterData(selectedCharacter, sorted, divisionFactor, isWealthDifference);

            PlotRetainerData(sorted, divisionFactor, isWealthDifference);

            ImPlot.EndPlot();
        }
    }

    private float CalculateDivisionFactor(double average)
    {
        if (average > 1000000) return 1000000;
        if (average > 1000) return 1000;
        return 1;
    }

    private string GetUnitName(float divisionFactor)
    {
        if (divisionFactor == 1000000) return "million gil";
        if (divisionFactor == 1000) return "thousand gil";
        return "gil";
    }

    private void PlotCharacterData(
        string selectedCharacter, List<WealthChange> data, float divisionFactor, bool isWealthDifference)
    {
        var characterWealthChanges = data.Where(x => x.CharacterName == selectedCharacter);
        var characterXValues = characterWealthChanges.Select(x => (double)x.Timestamp).ToArray();
        var characterYValues = characterWealthChanges
                               .Select(x => Math.Round(
                                           (isWealthDifference ? x.WealthDifference : x.Wealth) / divisionFactor, 2))
                               .ToArray();

        PlotLineBarData(selectedCharacter, characterXValues, characterYValues, characterXValues.Length);
    }

    private void PlotRetainerData(List<WealthChange> data, float divisionFactor, bool isWealthDifference)
    {
        var allRetainerData = data.Where(x => x.OwnerId != 0);
        var distinctRetainers = allRetainerData.DistinctBy(x => x.CharacterName).Select(x => x.CharacterName).ToArray();

        foreach (var retainerName in distinctRetainers)
        {
            var retainerData = allRetainerData.Where(x => x.CharacterName == retainerName);
            var retainerXValues = retainerData.Select(x => (double)x.Timestamp).ToArray();
            var retainerYValues = retainerData
                                  .Select(x => Math.Round(
                                              (isWealthDifference ? x.WealthDifference : x.Wealth) / divisionFactor, 2))
                                  .ToArray();

            PlotLineBarData(retainerName, retainerXValues, retainerYValues, retainerXValues.Length);
        }
    }

    private void PlotLineBarData(string dataName, double[] xValues, double[] yValues, int dataLength)
    {
        ImPlot.SetNextMarkerStyle(_usePlotMarkers ? ImPlotMarker.Circle : ImPlotMarker.None);

        if (_usePlotLines && _useShadedPlotLines != true)
            ImPlot.PlotLine(dataName, ref xValues[0], ref yValues[0], dataLength);
        else if (_usePlotLines && _useShadedPlotLines)
            ImPlot.PlotShaded(dataName, ref xValues[0], ref yValues[0], dataLength);
        else
            ImPlot.PlotBars(dataName, ref xValues[0], ref yValues[0], dataLength, 0.9f);

        DrawPlotLegend(dataName);
    }

    public void DrawPlotLegend(string dataName)
    {
        if (ImPlot.BeginLegendPopup(dataName, ImGuiMouseButton.Right))
        {
            if (ImGui.Checkbox("Line Plot", ref _usePlotLines)) _config.SetGraphOption("usePlotLines", _usePlotLines);

            if (_usePlotLines)
            {
                if (ImGui.Checkbox("Markers", ref _usePlotMarkers))
                    _config.SetGraphOption("usePlotMarkers", _usePlotMarkers);

                if (ImGui.Checkbox("Shaded", ref _useShadedPlotLines))
                    _config.SetGraphOption("useShadedPlotLines", _useShadedPlotLines);
            }

            ImPlot.EndLegendPopup();
        }
    }
}
