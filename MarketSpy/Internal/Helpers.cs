using System;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;

namespace MarketSpy.Internal;

public static class Helpers
{
    /// <summary>
    /// Converts a retainer name pointer to an SeString for use.
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public static unsafe SeString RetainerNameFromAddress(byte* address)
    {
        return MemoryHelper.ReadSeStringNullTerminated((IntPtr)address);
    }

    /// <summary>
    ///     Calculate the value of a market transaction before the tax was taken out.
    /// </summary>
    /// <param name="totalValue"></param>
    /// <param name="salesTax"></param>
    /// <param name="isSale"></param>
    /// <returns></returns>
    public static int CalculateBeforeTaxValue(int totalValue, float salesTax, bool isSale)
    {
        var taxDecimal = salesTax / 100;
        var effectiveRate = isSale ? 1 - taxDecimal : 1 + taxDecimal;
        var valueBeforeTax = totalValue / effectiveRate;
        return (int)valueBeforeTax;
    }
}
