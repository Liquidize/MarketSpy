using MarketSpy.Database.Enums;

namespace MarketSpy.Database;

public struct FailedTransaction
{
    public object TransactiobObj { get; private set; }
    public DbTransactionType Type { get; private set; }

    public int RetryCount { get; set; } = 0;


    public FailedTransaction(object transactiobObj, DbTransactionType type)
    {
        TransactiobObj = transactiobObj;
        Type = type;
    }
}
