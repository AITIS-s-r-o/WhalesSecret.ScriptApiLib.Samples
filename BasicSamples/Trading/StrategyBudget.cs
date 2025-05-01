using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Samples.AccessLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Budget;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Utils.Orders;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Trading;

/// <summary>
/// Sample that demonstrates how to use trading strategy budget.
/// <para>
/// Trading strategy budget has two roles:
/// <list type="bullet">Prevention of creating orders that would exceed the budget.</list>
/// <list type="bullet">Calculation of profit and loss of the orders created by the associated trade API client.</list>
/// </para>
/// <para>This sample is a toy example to demonstrate the calculation of the profit and loss. Two market orders are executed and the budget report is generated.</para>
/// <para>Private connections are necessary to create orders. Exchange API credentials have to be set.</para>
/// </summary>
/// <remarks>
/// Trading strategy budget is associated with <see cref="ITradeApiClient">trade API client</see> when <see cref="BudgetRequest"/> is used in <see cref="ConnectionOptions"/>
/// parameter of <see cref="ScriptApi.ConnectAsync(ExchangeMarket, ConnectionOptions?)"/> method. The budget request includes the initial budget, which is a listing of all assets
/// and their respective amounts that the client can use and is restricted by. The budget request also includes the primary asset, which is the asset in which the value of
/// the budget is calculated.
/// <para>
/// For example, the budget request can define the initial budget to be <c>0.1</c> BTC and <c>5,000</c> EUR. If the primary asset is set to BTC, the budget's initial value will
/// be calculated in BTC. Let's say the price of 1 BTC at the start time is <c>80,000</c> EUR. The initial value of the budget will be <c>0.1 + 5,000 / 80,000 = 0.1625</c> BTC.
/// </para>
/// <para>
/// Let's assume then that the client makes trades and by doing so gains <c>0.02</c> BTC and loses <c>400</c> EUR including trading fees. Suppose further that the price of 1 BTC
/// changes to 85,000 EUR. Using <see cref="ITradeApiClient.GenerateReportAsync(CancellationToken)"/> we can generate report that will give us the current value of the budget in
/// the primary asset as well as profit and loss. In our example, the current value of the budget will be <c>0.1 + 0.02 + (5,000 - 400) / 85,000 = 0.12 + 0.05176 = 0.17176</c> BTC.
/// Therefore, the profit will be calculated as <c>0.17176 - 0.1625 = 0.00926</c> BTC.
/// </para>
/// <para>
/// Note that the budget does not take into account the actual balance in the exchange wallet. It is a mechanism that is implemented within an instance of a trade API client. It is
/// possible to create multiple trade API client instances, each with different budget, trading a different strategy. This is useful when you want to compare different strategies
/// side by side. However, you must make sure that your exchange wallet balance is sufficient to cover the budgets of all clients that are running simultaneously. Also note that
/// due to the nature of some order types and due to deficiencies of API of some exchanges, you should have some buffer in your exchange wallet on top of the sum of all
/// simultaneously running budgets.
/// </para>
/// <para>IMPORTANT: You have to change the secrets in <see cref="Credentials"/> to make the sample work.</para>
/// </remarks>
public class StrategyBudget : IScriptApiSample
{
    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

        CreateOptions createOptions = new(license: License.WsLicense);
        await using ScriptApi scriptApi = await ScriptApi.CreateAsync(createOptions, timeoutCts.Token).ConfigureAwait(false);

        string primaryAsset = "BTC";
        string secondaryAsset = exchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => "EUR",
            ExchangeMarket.KucoinSpot => "USDT",
            _ => throw new SanityCheckException($"Invalid exchange market {exchangeMarket} provided."),
        };

        BudgetSnapshot initialBudget = new()
        {
            { primaryAsset, 0.0001m },
            { secondaryAsset, 50m },
        };

        BudgetRequest budgetRequest = new(strategyName: "Strategy budget sample", primaryAsset: primaryAsset, initialBudget);
        ConnectionOptions connectionOptions = new(ConnectionType.FullTrading, budgetRequest: budgetRequest);

        await using OrderSampleHelper helper = await OrderSampleHelper.InitializeAsync(scriptApi, exchangeMarket, timeoutCts.Token, connectionOptions).ConfigureAwait(false);
        ITradeApiClient tradeClient = helper.TradeApiClient;
        SymbolPair symbolPair = helper.SelectedSymbolPair;

        OrderRequestBuilder<MarketOrderRequest> marketBuilder = new(helper.ExchangeInfo);

        // The client order ID suffix is a special requirement when the budget is used. We can either use null client order ID in our requests, or we need to specify the suffix,
        // which will be altered by the budget. The suffix is used to uniquely identify the orders that are created by trade API client with the budget.
        string clientOrderId = string.Create(CultureInfo.InvariantCulture, $"budget-sample-1{ITradingStrategyBudget.ClientOrderIdSuffix}");
        string clientOrderId2 = string.Create(CultureInfo.InvariantCulture, $"budget-sample-2{ITradingStrategyBudget.ClientOrderIdSuffix}");

        // Buy a small amount of bitcoin.
        decimal quoteOrderSize = exchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => 6.0m,
            ExchangeMarket.KucoinSpot => 1.0m,
            _ => throw new SanityCheckException($"Invalid exchange market {exchangeMarket} provided."),
        };

        Console.WriteLine();
        Console.WriteLine("Build a market order request for the first order.");
        MarketOrderRequest marketOrderRequest = marketBuilder
            .SetClientOrderId(clientOrderId)
            .SetSide(OrderSide.Buy)
            .SetSymbolPair(symbolPair)
            .SetSizeInBaseSymbol(false)
            .SetSize(quoteOrderSize)
            .Build();

        Console.WriteLine($"Constructed market order request: {marketOrderRequest}");
        Console.WriteLine();

        Console.WriteLine("Place the order.");
        Console.WriteLine();

        ILiveMarketOrder marketOrder = await tradeClient.CreateOrderAsync(marketOrderRequest, timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine($"Market order '{marketOrder} is live.");
        Console.WriteLine();

        Console.WriteLine("Wait until the market order is filled.");
        await marketOrder.WaitForFillAsync(timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine("The first order was fully filled.");
        Console.WriteLine();

        Console.WriteLine("Wait 5 seconds before placing the second order.");
        Console.WriteLine();
        await Task.Delay(TimeSpan.FromSeconds(5), timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine("Build a market order request for the second order.");
        marketOrderRequest = marketBuilder
            .SetClientOrderId(clientOrderId2)
            .SetSide(OrderSide.Sell)
            .Build();

        Console.WriteLine($"Constructed market order request: {marketOrderRequest}");
        Console.WriteLine();

        Console.WriteLine("Place the order.");
        Console.WriteLine();

        marketOrder = await tradeClient.CreateOrderAsync(marketOrderRequest, timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine($"Market order '{marketOrder} is live.");
        Console.WriteLine();

        Console.WriteLine("Wait until the market order is filled.");
        await marketOrder.WaitForFillAsync(timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine("The second order has concluded. Now calculate profit and loss.");
        Console.WriteLine();

        BudgetReport budgetReport = await tradeClient.GenerateBudgetReportAsync(timeoutCts.Token).ConfigureAwait(false);

        string report = $$"""
            Budget report:
              start time: {{budgetReport.StartTime}}
              end time: {{budgetReport.EndTime}}
              initial value: {{budgetReport.InitialValue}} {{primaryAsset}}
              final value: {{budgetReport.FinalValue}} {{primaryAsset}}
              profit/loss: {{budgetReport.TotalProfit}} {{primaryAsset}}
              initial primary asset holdings: {{budgetReport.InitialBudget[primaryAsset]}} {{primaryAsset}}
              initial secondary asset holdings: {{budgetReport.InitialBudget[secondaryAsset]}} {{secondaryAsset}}
              final primary asset holdings: {{budgetReport.FinalBudget[primaryAsset]}} {{primaryAsset}}
              final secondary asset holdings: {{budgetReport.FinalBudget[secondaryAsset]}} {{secondaryAsset}}
            """;

        Console.WriteLine(report);
        Console.WriteLine();

        Console.WriteLine("Disposing trade API client and script API.");
    }
}