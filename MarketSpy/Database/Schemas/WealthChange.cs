using System;
using MarketSpy.Database.Enums;
using SQLite;

namespace MarketSpy.Database.Schemas;

public class WealthChange : DbTable
{
    public WealthChange() { }


    public WealthChange(
        string characterName, long characterId, int weath, int difference, string owner, long ownerId,
        WealthChangeType changeType)
    {
        Timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        CharacterName = characterName;
        CharacterId = characterId;
        Wealth = weath;
        WealthDifference = difference;
        Owner = owner;
        OwnerId = ownerId;
        ChangeType = changeType;
    }

    public WealthChange(string characterName, long characterId, int weath, int difference, WealthChangeType changeType)
    {
        Timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        CharacterName = characterName;
        CharacterId = characterId;
        Wealth = weath;
        WealthDifference = difference;
        Owner = null;
        OwnerId = 0;
        ChangeType = changeType;
    }

    [Ignore]
    public DateTime ChangeTime => DateTimeOffset.FromUnixTimeSeconds(Timestamp).Date;

    public string Owner { get; set; }

    public long OwnerId { get; set; }
    public int Wealth { get; set; }
    public int WealthDifference { get; set; }
    public WealthChangeType ChangeType { get; set; }
}
