namespace WhalesSecret.ScriptApiLib.Samples.AdvancedDemos.Accounting;

/// <summary>
/// Description of changes in balance for a single asset on an exchange.
/// </summary>
internal class AssetStats
{
    /// <summary>Difference in the total balance for the given asset.</summary>
    public decimal TotalDiff { get; set; }

    /// <summary>Difference in the balance due to trading activity for the given asset.</summary>
    public decimal TradingDiff { get; set; }

    /// <summary>Difference in the balance due to trading or deposit/withdrawal fees.</summary>
    public decimal FeeDiff { get; set; }

    /// <summary>Difference in the balance due to deposits.</summary>
    public decimal DepositDiff { get; set; }

    /// <summary>Difference in the balance due to withdrawals.</summary>
    public decimal WithdrawalDiff { get; set; }
}