using System;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Network;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using MarketSpy.Database;
using MarketSpy.Managers;
using MarketSpy.Windows;

namespace MarketSpy;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/mspy";
    
    public ChatGui ChatGui;
    public ClientState ClientState;
    public DataManager DataManager;
    public Framework Framework;

    public GameNetwork Network;

    public WindowSystem WindowSystem = new("MarketSpy");

    public Plugin(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
        [RequiredVersion("1.0")] CommandManager commandManager,
        [RequiredVersion("1.0")] GameNetwork network,
        [RequiredVersion("1.0")] DataManager dataManager,
        [RequiredVersion("1.0")] ChatGui chat,
        [RequiredVersion("1.0")] ClientState client,
        [RequiredVersion("1.0")] Framework framework)
    {
        PluginInterface = pluginInterface;
        CommandManager = commandManager;
        Network = network;
        DataManager = dataManager;
        ChatGui = chat;
        ClientState = client;
        Framework = framework;

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        MarketDb = new MarketDatabase(this);
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the Market Spy main window."
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

        TaxManager = new MarketTaxManager(client, chat);
        PlayerWealthManager = new PlayerWealthManager(this);
        RetainerWealthManager = new RetainerWealthManager(this);
        TransactionManager = new MarketTransactionManager(this);

        // Logged in already so load the wealth info
        if (ClientState.IsLoggedIn)
        {
            if (ClientState?.LocalPlayer?.CurrentWorld.GameData?.Name.RawString != null)
                TaxManager.RefreshTaxRates(ClientState?.LocalPlayer?.CurrentWorld.GameData?.Name.RawString);
        }
    }

    private DalamudPluginInterface PluginInterface { get; init; }
    private CommandManager CommandManager { get; init; }
    public Configuration Configuration { get; init; }

    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public MarketDatabase MarketDb { get; init; }
    public RetainerWealthManager RetainerWealthManager { get; set; }

    public MarketTaxManager TaxManager { get; set; }

    public PlayerWealthManager PlayerWealthManager { get; set; }

    public MarketTransactionManager TransactionManager { get; set; }

    public string Name => "Market Spy";

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(CommandName);

        ConfigWindow.Dispose();
        MainWindow.Dispose();
        MarketDb.Dipoose();
        RetainerWealthManager.Dispose();
        TaxManager.Dispose();
        PlayerWealthManager.Dispose();
        TransactionManager.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just display our main ui
        MainWindow.IsOpen = true;
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void DrawConfigUI()
    {
        ConfigWindow.IsOpen = true;
    }
}
