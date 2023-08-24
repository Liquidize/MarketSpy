using SQLite;

namespace MarketSpy.Database.Schemas;

public class DbTable
{
    [AutoIncrement]
    [PrimaryKey]
    public int Id { get; set; }

    [Indexed]
    public long CharacterId { get; set; }

    [Indexed]
    public string CharacterName { get; set; }


    public long Timestamp { get; set; }
}
