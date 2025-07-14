using System.Globalization;

namespace WhalesSecret.ScriptApiLib.Samples.AdvancedDemos.LeveragedDcaCalculator;

/// <summary>
/// Result of a L-DCA calculation.
/// </summary>
public class LdcaResult
{
    /// <summary>Close price of the last candle in the calculated time-frame.</summary>
    public decimal FinalPrice { get; }

    /// <summary>Final base symbol balance.</summary>
    public decimal FinalBaseBalance { get; }

    /// <summary>Final quote symbol balance.</summary>
    public decimal FinalQuoteBalance { get; }

    /// <summary>Sum of the paid fees.</summary>
    public decimal FeesPaid { get; }

    /// <summary>Symbol of the fees.</summary>
    public string FeeSymbol { get; }

    /// <summary>Average order price in the quote symbol.</summary>
    public decimal AverageOrderPrice { get; }

    /// <summary>Total end balance expressed in the quote symbol. If leverage was used, this is the total value of the leveraged amount.</summary>
    public decimal TotalValue { get; }

    /// <summary>Sum of all funds needed to execute the orders.</summary>
    /// <remarks>
    /// If leverage was used, this is in the quote symbol. If leverate was not used, the is in the quote symbol for buy orders and in the base symbol for sell orders.
    /// </remarks>
    public decimal TotalInvestedAmount { get; }

    /// <summary>Profit relative to the <see cref="TotalInvestedAmount"/>. Negative value means loss.</summary>
    public decimal ProfitPercent { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="finalPrice">Close price of the last candle in the calculated time-frame.</param>
    /// <param name="finalBaseBalance">Final base symbol balance.</param>
    /// <param name="finalQuoteBalance">Final quote symbol balance.</param>
    /// <param name="feesPaid">Sum of the paid fees.</param>
    /// <param name="feeSymbol">Symbol of the fees.</param>
    /// <param name="averageOrderPrice">Average order price in the quote symbol.</param>
    /// <param name="totalValue">Total end balance expressed in the quote symbol. If leverage was used, this is the total value of the leveraged amount.</param>
    /// <param name="totalInvestedAmount">Sum of all funds needed to execute the orders.</param>
    /// <param name="profitPercent">Profit relative to the <see cref="TotalInvestedAmount"/>. Negative value means loss.</param>
    public LdcaResult(decimal finalPrice, decimal finalBaseBalance, decimal finalQuoteBalance, decimal feesPaid, string feeSymbol, decimal averageOrderPrice, decimal totalValue,
        decimal totalInvestedAmount, decimal profitPercent)
    {
        this.FinalPrice = finalPrice;
        this.FinalBaseBalance = finalBaseBalance;
        this.FinalQuoteBalance = finalQuoteBalance;
        this.FeesPaid = feesPaid;
        this.FeeSymbol = feeSymbol;
        this.AverageOrderPrice = averageOrderPrice;
        this.TotalValue = totalValue;
        this.TotalInvestedAmount = totalInvestedAmount;
        this.ProfitPercent = profitPercent;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}={1},{2}={3},{4}={5},{6}={7},{8}=`{9}`,{10}={11},{12}={13},{14}={15},{16}={17}]",
            nameof(this.FinalPrice), this.FinalPrice,
            nameof(this.FinalBaseBalance), this.FinalBaseBalance,
            nameof(this.FinalQuoteBalance), this.FinalQuoteBalance,
            nameof(this.FeesPaid), this.FeesPaid,
            nameof(this.FeeSymbol), this.FeeSymbol,
            nameof(this.AverageOrderPrice), this.AverageOrderPrice,
            nameof(this.TotalValue), this.TotalValue,
            nameof(this.TotalInvestedAmount), this.TotalInvestedAmount,
            nameof(this.ProfitPercent), this.ProfitPercent
        );
    }
}