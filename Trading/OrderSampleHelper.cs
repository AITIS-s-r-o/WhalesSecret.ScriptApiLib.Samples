using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Exchanges;

namespace WhalesSecret.ScriptApiLib.Samples.Trading;

/// <summary>
/// Helper container for order samples with connected trading client.
/// </summary>
/// <remarks>
/// The helper uses <c>BTC/EUR</c> symbol pair for Binance exchange and <c>BTC/USDT</c> symbol pair for KuCoin exchange. Since the samples create real orders, a positive balance
/// is needed in the exchange wallets for the samples to work.
/// </remarks>
public class OrderSampleHelper : IAsyncDisposable
{
    /// <summary>Information about an exchange market and its tradable pairs.</summary>
    public ExchangeInfo ExchangeInfo { get; }

    /// <summary>Connected trade API client.</summary>
    public ITradeApiClient TradeApiClient { get; }

    /// <summary>Price of the best bid.</summary>
    public decimal BestBid { get; }

    /// <summary>Price of the best ask.</summary>
    public decimal BestAsk { get; }

    /// <summary>Symbol pair to be traded on the target exchange.</summary>
    public SymbolPair SelectedSymbolPair { get; }

    /// <summary>Volume precision for the <see cref="SymbolPair">selected symbol pair</see>.</summary>
    public int VolumePrecision { get; }

    /// <summary>Lock object to be used when accessing <see cref="disposedValue"/>.</summary>
    private readonly Lock disposedValueLock;

    /// <summary>Set to <c>true</c> if the object was disposed already, <c>false</c> otherwise. Used by the dispose pattern.</summary>
    /// <remarks>All access has to be protected by <see cref="disposedValueLock"/>.</remarks>
    private bool disposedValue;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="exchangeInfo">Information about an exchange market and its tradable pairs.</param>
    /// <param name="tradeApiClient">Connected trade API client.</param>
    /// <param name="bestBid">Price of the best bid.</param>
    /// <param name="bestAsk">Price of the best ask.</param>
    /// <param name="selectedSymbolPair">Symbol pair to be traded on the target exchange.</param>
    /// <param name="volumePrecision">Volume precision for the <paramref name="selectedSymbolPair">selected symbol pair</paramref>.</param>
    public OrderSampleHelper(ExchangeInfo exchangeInfo, ITradeApiClient tradeApiClient, decimal bestBid, decimal bestAsk, SymbolPair selectedSymbolPair, int volumePrecision)
    {
        this.disposedValueLock = new();

        this.ExchangeInfo = exchangeInfo;
        this.TradeApiClient = tradeApiClient;
        this.BestBid = bestBid;
        this.BestAsk = bestAsk;
        this.SelectedSymbolPair = selectedSymbolPair;
        this.VolumePrecision = volumePrecision;
    }

    /// <summary>
    /// Common initialization for order samples. A full-trading connection is established with the target exchange and an order book subscription is created in order to get access
    /// to the current order book snapshot. <see cref="BestBid"/> and <see cref="BestAsk"/> are filled based on the information from the order book.
    /// <see cref="SelectedSymbolPair"/> is filled with the selected symbol pair to be traded on the target exchange and its <see cref="VolumePrecision">volume precision</see> is
    /// also filled.
    /// </summary>
    /// <param name="scriptApi"> Trade script API with script environment.</param>
    /// <param name="exchangeMarket">Exchange market to connect to.</param>
    /// <param name="cancellationToken">Cancellation token with which the caller can cancel the operation.</param>
    /// <returns>Helper container for order samples with connected trading client.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
    /// <exception cref="OperationFailedException">Thrown if the operation failed.</exception>
    /// <exception cref="SanityCheckException">Thrown if a fundamental assumption of the code was violated.</exception>
    public static async Task<OrderSampleHelper> InitializeAsync(ScriptApi scriptApi, ExchangeMarket exchangeMarket, CancellationToken cancellationToken)
    {
        // Initialization of the market is required before connection can be created.
        await Console.Out.WriteLineAsync($"Initialize exchange market {exchangeMarket}.").ConfigureAwait(false);
        ExchangeInfo exchangeInfo = await scriptApi.InitializeMarketAsync(exchangeMarket, cancellationToken).ConfigureAwait(false);

        // Credentials must be set before we can create a private connection.

#pragma warning disable CA2000 // Dispose objects before losing scope
        IApiIdentity apiIdentity = exchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => Credentials.GetBinanceHmacApiIdentity(),
            ExchangeMarket.KucoinSpot => Credentials.GetKucoinApiIdentity(),
            _ => throw new SanityCheckException($"Unsupported exchange market {exchangeMarket} provided."),
        };
#pragma warning restore CA2000 // Dispose objects before losing scope

        scriptApi.SetCredentials(apiIdentity);

        await Console.Out.WriteLineAsync($"Connect to {exchangeMarket} exchange with full-trading access.").ConfigureAwait(false);

        // Default connection options use full-trading connection type, which means both public and private connections will be established with the exchange.
        ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, ConnectionOptions.Default).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Connection to {exchangeMarket} has been established successfully.").ConfigureAwait(false);

        SymbolPair symbolPair = exchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => SymbolPair.BTC_EUR,
            ExchangeMarket.KucoinSpot => SymbolPair.BTC_USDT,
            _ => throw new SanityCheckException($"Invalid exchange market {exchangeMarket} provided."),
        };

        // Extract information about the symbol pair we are going to trade.
        if (!exchangeInfo.SymbolPairLimits.TryGetValue(symbolPair, out ExchangeSymbolPairLimits? limits))
        {
            string msg = $"Symbol pair '{symbolPair}' is not supported by {exchangeMarket}.";
            await Console.Error.WriteLineAsync($"ERROR: {msg}").ConfigureAwait(false);
            throw new SanityCheckException(msg);
        }

        if (limits.VolumePrecision is null)
        {
            string msg = $"Volume precision is not known for symbol pair '{symbolPair}' on {exchangeMarket}.";
            await Console.Error.WriteLineAsync($"ERROR: {msg}").ConfigureAwait(false);
            throw new SanityCheckException(msg);
        }

        await Console.Out.WriteLineAsync($"Volume precision for symbol pair '{symbolPair}' on {exchangeMarket} is {limits.VolumePrecision}.").ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Get best bid and ask prices from an order book on {exchangeMarket}.").ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"Create subscription for '{symbolPair}' order book on {exchangeMarket}.").ConfigureAwait(false);

        decimal bestBid, bestAsk;
        await using (IOrderBookSubscription subscription = await tradeClient.CreateOrderBookSubscriptionAsync(symbolPair).ConfigureAwait(false))
        {
            await Console.Out.WriteLineAsync($"Order book subscription for '{symbolPair}' on {exchangeMarket} has been created successfully as '{
                subscription}'. Wait for the next order order book update.").ConfigureAwait(false);

            OrderBook orderBook = await subscription.GetOrderBookAsync(getMode: OrderBookGetMode.WaitUntilNew, cancellationToken).ConfigureAwait(false);

            if ((orderBook.Bids.Count == 0) || (orderBook.Asks.Count == 0))
            {
                string msg = $"Empty order book has been received for symbol pair '{symbolPair}' on {exchangeMarket}.";
                await Console.Error.WriteLineAsync($"ERROR: {msg}").ConfigureAwait(false);
                throw new SanityCheckException(msg);
            }

            bestBid = orderBook.Bids[0].Price;
            bestAsk = orderBook.Asks[0].Price;

            await Console.Out.WriteLineAsync($"Best bid price is {bestBid}, best ask price is {bestAsk}.").ConfigureAwait(false);

            await Console.Out.WriteLineAsync("Dispose order book subscription.").ConfigureAwait(false);
        }

        OrderSampleHelper orderSampleHelper = new(exchangeInfo, tradeClient, bestBid: bestBid, bestAsk: bestAsk, symbolPair, volumePrecision: limits.VolumePrecision.Value);
        return orderSampleHelper;
    }

    /// <summary>
    /// Frees managed resources used by the object.
    /// </summary>
    /// <returns>A <see cref="ValueTask">task</see> that represents the asynchronous dispose operation.</returns>
    protected virtual async ValueTask DisposeCoreAsync()
    {
        lock (this.disposedValueLock)
        {
            if (this.disposedValue)
                return;

            this.disposedValue = true;
        }

        await this.TradeApiClient.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeCoreAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}