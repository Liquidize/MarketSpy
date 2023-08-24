using System;
using System.Numerics;
using Dalamud.Game.ClientState;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using MarketSpy.Database;

namespace MarketSpy.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration Configuration;
    private ClientState ClientState;
    private MarketDatabase MarketDb;

    public ConfigWindow(Plugin plugin) : base(
        "Market Spy - Configuration Window",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(600, 300);
        SizeCondition = ImGuiCond.Always;

        Configuration = plugin.Configuration;
        MarketDb = plugin.MarketDb;
        ClientState = plugin.ClientState;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // can't ref a property, so use a local copy
        var dbPath = Configuration.DatabasePath;
        ImGui.Text("Database Path (Dont recommend you change)");
        if (ImGui.InputText("", ref dbPath, 256))
        {
            Configuration.DatabasePath = dbPath;
            Configuration.Save();
        }

        ImGui.NewLine();
    }
}
