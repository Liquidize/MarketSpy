using SQLite;

namespace MarketSpy.Database.Schemas;

public class KnownWealth : DbTable
{
    [Indexed]
    public long CharacterId { get; set; }

    [Indexed]
    public string CharacterName { get; set; }

    [Indexed]
    public long OwnerId { get; set; }

    public string Owner { get; set; }

    public int CurrentWealth { get; set; }
}
