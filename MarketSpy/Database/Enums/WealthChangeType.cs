namespace MarketSpy.Database.Enums;

public enum WealthChangeType
{
    Init = -1,
    Unknown = 0,
    Trade = 1,
    Marketboard = 2,
    NPCShop = 4,
    Teleport = 5,
    MailSend = 6,
    MailRecieved = 7,
    RetainerWithdraw = 8,
    RetainerDeposit = 9,
    FCWithdraw = 10,
    FCDeposit = 11
}
