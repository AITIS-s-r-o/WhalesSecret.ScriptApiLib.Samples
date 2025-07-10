using System;
using System.Globalization;

namespace WhalesSecret.ScriptApiLib.Samples.AdvancedDemos.LeveragedDcaCalculator;

/// <summary>
/// Details of an order that was placed with leverage.
/// </summary>
public class LeveragedOrder
{
    /// <summary>Price when the order was opened.</summary>
    public decimal EntryPrice { get; }

    /// <summary>Initial margin of the order in the quote symbol. This is the amount we lose in case the position is liquidated.</summary>
    public decimal InitialMargin { get; }

    /// <summary>Size of the order in base symbol after applying the leverage.</summary>
    public decimal PositionBaseAmount { get; }

    /// <summary>Size of the order in quote symbol after applying the leverage.</summary>
    public decimal PositionQuoteAmount { get; }

    /// <summary>Price at which the order is liquidated.</summary>
    public decimal LiquidationPrice { get; }

    /// <summary>UTC time when the position was opened.</summary>
    public DateTime OpenTimeUtc { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="entryPrice">Price when the order was opened.</param>
    /// <param name="initialMargin">Initial margin of the order in the quote symbol.</param>
    /// <param name="positionBaseValue">Size of the order in base symbol after applying the leverage.</param>
    /// <param name="positionQuoteValue">Size of the order in quote symbol after applying the leverage.</param>
    /// <param name="liquidationPrice">Price at which the order is liquidated.</param>
    /// <param name="openTimeUtc">UTC time when the position was opened.</param>
    public LeveragedOrder(decimal entryPrice, decimal initialMargin, decimal positionBaseValue, decimal positionQuoteValue, decimal liquidationPrice, DateTime openTimeUtc)
    {
        this.EntryPrice = entryPrice;
        this.InitialMargin = initialMargin;
        this.PositionBaseAmount = positionBaseValue;
        this.PositionQuoteAmount = positionQuoteValue;
        this.LiquidationPrice = liquidationPrice;
        this.OpenTimeUtc = openTimeUtc;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}={3},{4}={5},{6}={7},{8}={9},{10}={11:yyyy-MM-dd HH:mm:ss}]",
            nameof(this.EntryPrice), this.EntryPrice,
            nameof(this.InitialMargin), this.InitialMargin,
            nameof(this.PositionBaseAmount), this.PositionBaseAmount,
            nameof(this.PositionQuoteAmount), this.PositionQuoteAmount,
            nameof(this.LiquidationPrice), this.LiquidationPrice,
            nameof(this.OpenTimeUtc), this.OpenTimeUtc
        );
    }
}