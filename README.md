# Market Spy

A wealth tracking plugin for Dalamud. This plugin tracks your gil earned and spent through various means to display in interactive graphs and tables (eventually :()).

## Main Points

* Gil tracking
  * Tracks player gil earned and lost from the most common actions (Trading, marketboard usage, retainer deposit/withdraw, fc chest usage, teleporting, etc)
  * Tracks retainer gil earned and lost (though with a quirk - due to how retainer gil seems to get updated, we can never really know why it was updated so all updates to retainer are considered "unknown" for now)
* Trade tracking
  * Tracks when a gil trade is made with another player, storing the net amount of gil gained/lossed from the trade.
* Marketboard transaction tracking
  * Tracks when a marketboard item purchase happens and stores important information about it such as: item name, item id, cost, value per item, who you bought it from, when you bought it, where you bought it from.
  * Tracks when a retainer sells an item on the marketboard (though with a few quirks listed below, but this is fine for the purposes of my own personal usage/what i was planning for this plugin)
    * It checks via chat messages, and so it only can track it if you are logged in on your home work and not in an intance. It is probably possible to do it via manually checking "market history"
      but even that will have its limitation.
    * It does not know which of your retainers sold the item.
    * It cannot tell who bought the item.
    * For the sake of "which market" unlike when buying an item, it is not the town/city where the market board was located, but rather where your retainer is dispatched.
    * Sale tax rates are obtained through an web request to Universalis on login and on world transfer.
* Graphing and data viewing
  * Currently there are a limited number of graphs and ways to view the data. More methods and types of visualizations will be added eventually.
    * Wealth over Time - Displays a bar or line graph with your personal and retainer wealth gain, displaying a new plot for each new wealth change event.
    * Wealth Change by Type - Displays a pie chart showing the proportional change in your wealth by types of change (In change count entries, not wealth for that type - yet)
* Data Storage
  * Data in stored useing a local SQLite3 database. You can view the table schemas in the Database/Schemas folder.
    * The database is not password protected or anything, it is your data, do as you wish with it :).


My intentions for this plugin was to simply create a way of automating what I already did for my personal wealth tracking in Excel. So it will likely evolve over time based on what I feel like adding,
but if you have an improvement or suggestion feel free to reach out.

## TODO

* Code cleanup - It is messy I know, was sort of learning how to interact with Dalamud and XIV while making this. It will get better :)
* More data visualization, need to figure out what I want to visualize as a graph or table.
* Show market transaction information somehow
* More data filtering/aggregation options
* Custom database querying/graphs? maybe

## To Use

### Installing via Repo

This repository acts as a custom dalamud repository that you can use to install/update the plugin via the dalamud plugin installer. Follow the below steps to install it that way if you don't want to build it yourself.

1. Launch the game and use `/xlsettings` in chat or `xlsettings` in the Dalamud Console to open up the Dalamud settings.
    * In here, go to `Experimental`, and add the the url 'https://raw.githubusercontent.com/Liquidize/MarketSpy/main/repo.json' to the list of custom repositories.
2. Next, use `/xlplugins` (chat) or `xlplugins` (console) to open up the Plugin Installer.
    * In here, search for and install 'Market Spy'
3. You should now be able to use `/mspy` (chat) or `myspy` (console) to open the main window!
    * Data will automatically be collected and stored in the database when specific events occur, retainer information will only be updated when you use the retainer bell (Due to limitations)

### Building

1. Open up `MarketSpy.sln` in your C# editor of choice (likely [Visual Studio 2022](https://visualstudio.microsoft.com) or [JetBrains Rider](https://www.jetbrains.com/rider/)).
2. Build the solution. By default, this will build a `Debug` build, but you can switch to `Release` in your IDE.
3. The resulting plugin can be found at `MarketSpy/bin/x64/Debug/MarketSpy.dll` (or `Release` if appropriate.)

### Activating in-game

1. Launch the game and use `/xlsettings` in chat or `xlsettings` in the Dalamud Console to open up the Dalamud settings.
    * In here, go to `Experimental`, and add the full path to the `MarketSpy.dll` to the list of Dev Plugin Locations.
2. Next, use `/xlplugins` (chat) or `xlplugins` (console) to open up the Plugin Installer.
    * In here, go to `Dev Tools > Installed Dev Plugins`, and the `SamplePlugin` should be visible. Enable it.
3. You should now be able to use `/pmycommand` (chat) or `pmycommand` (console)!

Note that you only need to add it to the Dev Plugin Locations once (Step 1); it is preserved afterwards. You can disable, enable, or load your plugin on startup through the Plugin Installer.

### Reconfiguring for your own uses

Basically, just replace all references to `SamplePlugin` in all of the files and filenames with your desired name. You'll figure it out 😁

Dalamud will load the JSON file (by default, `SamplePlugin/SamplePlugin.json`) next to your DLL and use it for metadata, including the description for your plugin in the Plugin Installer. Make sure to update this with information relevant to _your_ plugin!