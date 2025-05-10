using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WhalesSecret.TradeScriptLib.API.TradingV1.Budget;

namespace WhalesSecret.ScriptApiLib.Samples.SharedLib;

/// <summary>
/// Helper methods for working with budget reports.
/// </summary>
public static class Reports
{
    /// <summary>Separator of values on a single row in the report file.</summary>
    private const char ReportFileValueSeparator = ',';

    /// <summary>
    /// Converts budget report to string that can be printed to the console.
    /// </summary>
    /// <param name="budgetReport">Budget report to convert to string.</param>
    /// <returns>String representation of the budget report.</returns>
    public static string BudgetReportToString(BudgetReport budgetReport)
    {
        string initialValueStr = string.Create(CultureInfo.InvariantCulture, $"{budgetReport.InitialValue}");
        string finalValueStr = string.Create(CultureInfo.InvariantCulture, $"{budgetReport.FinalValue}");
        string totalProfitStr = string.Create(CultureInfo.InvariantCulture, $"{budgetReport.TotalProfit}");
        string totalFeesValueStr = string.Create(CultureInfo.InvariantCulture, $"{budgetReport.TotalFeesValue}");
        string totalReservationsValueStr = string.Create(CultureInfo.InvariantCulture, $"{budgetReport.TotalReservationsValue}");

        StringBuilder stringBuilder = new();
        _ = stringBuilder
            .AppendLine("Budget report:")
            .AppendLine(CultureInfo.InvariantCulture, $"  start time: {budgetReport.StartTime} UTC")
            .AppendLine(CultureInfo.InvariantCulture, $"  end time: {budgetReport.EndTime} UTC")
            .AppendLine(CultureInfo.InvariantCulture, $"  initial value: {initialValueStr} {budgetReport.PrimaryAsset}")
            .AppendLine(CultureInfo.InvariantCulture, $"  final value: {finalValueStr} {budgetReport.PrimaryAsset}")
            .AppendLine(CultureInfo.InvariantCulture, $"  profit/loss: {totalProfitStr} {budgetReport.PrimaryAsset}")
            .AppendLine(CultureInfo.InvariantCulture, $"  fees value paid: {totalFeesValueStr} {budgetReport.PrimaryAsset}")
            .AppendLine(CultureInfo.InvariantCulture, $"  reservations: {totalReservationsValueStr} {budgetReport.PrimaryAsset}")
            .AppendLine()
            .AppendLine("Current budget:")
            .AppendLine();

        foreach ((string assetName, decimal amount) in budgetReport.FinalBudget)
            _ = stringBuilder.AppendLine(CultureInfo.InvariantCulture, $" {assetName}: {amount}");

        _ = stringBuilder.AppendLine();

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Creates a CSV table from a list of budget reports.
    /// </summary>
    /// <param name="budgetReports">List of budget reports.</param>
    /// <returns>CSV table in a string format that represents the list of budget reports.</returns>
    public static string BudgetReportsToCsvString(IReadOnlyList<BudgetReport> budgetReports)
    {
        BudgetReport lastBudgetReport = budgetReports[^1];
        StringBuilder fileContentBuilder = new();
        string primaryAsset = lastBudgetReport.PrimaryAsset;

        // Compose the header from the latest report.
        _ = fileContentBuilder
            .Append("Report Date Time (UTC)")
            .Append(ReportFileValueSeparator)
            .Append("Total Report Period")
            .Append(ReportFileValueSeparator)
            .Append(CultureInfo.InvariantCulture, $"Value ({primaryAsset})")
            .Append(ReportFileValueSeparator)
            .Append(CultureInfo.InvariantCulture, $"Diff last report ({primaryAsset})")
            .Append(ReportFileValueSeparator)
            .Append(CultureInfo.InvariantCulture, $"P/L ({primaryAsset})")
            .Append(ReportFileValueSeparator)
            .Append(CultureInfo.InvariantCulture, $"Reserves {primaryAsset}")
            .Append(ReportFileValueSeparator);

        string[] assetNames = lastBudgetReport.FinalBudget.Keys.Order().ToArray();

        for (int i = 0; i < assetNames.Length; i++)
        {
            _ = fileContentBuilder
                .Append(CultureInfo.InvariantCulture, $"Budget Balance {assetNames[i]}")
                .Append(ReportFileValueSeparator);
        }

        string[] feeAssetNames = lastBudgetReport.FeesPaid.Keys.Order().ToArray();

        for (int i = 0; i < feeAssetNames.Length; i++)
        {
            _ = fileContentBuilder.Append(CultureInfo.InvariantCulture, $"Fees Paid {assetNames[i]}");

            if (i != feeAssetNames.Length - 1)
                _ = fileContentBuilder.Append(ReportFileValueSeparator);
        }

        _ = fileContentBuilder.AppendLine();

        decimal prevValue = 0;

        for (int i = -1; i < budgetReports.Count; i++)
        {
            BudgetSnapshot snapshot;
            BudgetSnapshot feesPaid;

            if (i == -1)
            {
                // Second row is the initial budget line
                _ = fileContentBuilder
                    .Append(lastBudgetReport.StartTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                    .Append(ReportFileValueSeparator)
                    .Append(ReportFileValueSeparator)
                    .Append(CultureInfo.InvariantCulture, $"{lastBudgetReport.InitialValue}")
                    .Append(ReportFileValueSeparator)
                    .Append('0')
                    .Append(ReportFileValueSeparator)
                    .Append('0')
                    .Append(ReportFileValueSeparator)
                    .Append('0')
                    .Append(ReportFileValueSeparator);

                snapshot = lastBudgetReport.InitialBudget;
                feesPaid = new();

                prevValue = lastBudgetReport.InitialValue;
            }
            else
            {
                BudgetReport report = budgetReports[i];
                TimeSpan period = report.EndTime - lastBudgetReport.StartTime;
                string periodStr = period >= TimeSpan.FromDays(1)
                    ? period.ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture)
                    : period.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

                decimal diff = report.FinalValue - prevValue;

                _ = fileContentBuilder
                    .Append(report.EndTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                    .Append(ReportFileValueSeparator)
                    .Append(periodStr)
                    .Append(ReportFileValueSeparator)
                    .Append(CultureInfo.InvariantCulture, $"{report.FinalValue}")
                    .Append(ReportFileValueSeparator)
                    .Append(CultureInfo.InvariantCulture, $"{diff}")
                    .Append(ReportFileValueSeparator)
                    .Append(CultureInfo.InvariantCulture, $"{report.TotalProfit}")
                    .Append(ReportFileValueSeparator)
                    .Append(CultureInfo.InvariantCulture, $"{report.TotalReservationsValue}")
                    .Append(ReportFileValueSeparator);

                snapshot = report.FinalBudget;
                feesPaid = report.FeesPaid;

                prevValue = report.FinalValue;
            }

            for (int assetNameIndex = 0; assetNameIndex < assetNames.Length; assetNameIndex++)
            {
                string assetName = assetNames[assetNameIndex];

                if (snapshot.TryGetValue(assetName, out decimal value))
                    _ = fileContentBuilder.Append(CultureInfo.InvariantCulture, $"{value}");

                _ = fileContentBuilder.Append(ReportFileValueSeparator);
            }

            for (int feeAssetNameIndex = 0; feeAssetNameIndex < feeAssetNames.Length; feeAssetNameIndex++)
            {
                string assetName = feeAssetNames[feeAssetNameIndex];

                if (feesPaid.TryGetValue(assetName, out decimal value))
                    _ = fileContentBuilder.Append(CultureInfo.InvariantCulture, $"{value}");

                if (feeAssetNameIndex != feeAssetNames.Length - 1)
                    _ = fileContentBuilder.Append(ReportFileValueSeparator);
            }

            _ = fileContentBuilder.AppendLine();
        }

        return fileContentBuilder.ToString();
    }
}