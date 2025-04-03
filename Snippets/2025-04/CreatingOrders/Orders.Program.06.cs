using Skender.Stock.Indicators;
using System.Diagnostics;
using System.Globalization;
using System.Web;
using WhalesSecret.ScriptApiLib;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;

// The Telegram API token should be in form "XXXXXXXXXX:YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY".
string? apiToken = null;

if (apiToken is null)
    throw new SanityCheckException("Please provide your Telegram API token.");

// The Telegram group identifier is in form "@my_group".
string? groupId = null;

if (groupId is null)
    throw new SanityCheckException("Please provide a Telegram group identifier.");

// Use the Binance Testnet.
CreateOptions createOptions = new(connectToBinanceSandbox: true, license: null);
await using ScriptApi scriptApi = await ScriptApi.CreateAsync(createOptions);

// Specify your Binance Testnet API credentials.
using IApiIdentity apiIdentity = BinanceApiIdentity.CreateHmac(name: "MyApiKey", key: "<KEY>", secret: Convert.FromBase64String("<SECRET>"));
scriptApi.SetCredentials(apiIdentity);

Console.WriteLine("Connecting...");
await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(ExchangeMarket.BinanceSpot, ConnectionOptions.Default);
Console.WriteLine("Connection succeeded!");

await using ICandlestickSubscription candlestickSubscription = await tradeClient.CreateCandlestickSubscriptionAsync(SymbolPair.BTC_EUR);
await using ITickerSubscription tickerSubscription = await tradeClient.CreateTickerSubscriptionAsync(SymbolPair.BTC_EUR);

// Get historical candlestick data for the last three days for 1-minute candles.
DateTime endTime = DateTime.Now;
DateTime startTime = endTime.AddDays(-3);

CandlestickData candlestickData = await tradeClient.GetCandlesticksAsync(SymbolPair.BTC_EUR, CandleWidth.Minute1, startTime, endTime);

// Compute RSI using Skender.Stock.Indicators.
// Add the package to the project using "dotnet add package Skender.Stock.Indicators".
// Add "using Skender.Stock.Indicators;" to the source file.
List<Quote> quotes = new();
foreach (Candle candle in candlestickData.Candles)
    quotes.Add(QuoteFromCandle(candle));

Task<Candle>? lastClosedCandleTask = null;

OrderSide? lastAction = null;

// Loop until the program is terminated using CTRL+C.
while (true)
{
    // Wait for a new ticker.
    Ticker lastTicker = await tickerSubscription.GetNewerTickerAsync();

    // If there is no task to get the latest closed candle, create one.
    lastClosedCandleTask ??= candlestickSubscription.WaitNextClosedCandlestickAsync(CandleWidth.Minute1);

    string priceStr = ToInvariantString(Math.Round(lastTicker.LastPrice!.Value, 2));
    Console.WriteLine($"The latest price is: {priceStr} EUR.");

    if (lastClosedCandleTask.IsCompleted)
    {
        Candle lastClosedCandle = await lastClosedCandleTask;
        quotes.Add(QuoteFromCandle(lastClosedCandle));
        lastClosedCandleTask = null;

        // Compute RSI.
        RsiResult lastRsi = quotes.GetRsi().Last();
        double rsiValue = Math.Round(lastRsi.Rsi!.Value, 2);

        string interpretation = rsiValue switch
        {
            < 30 => " (oversold!)",
            > 70 => " (overbought!)",
            _ => string.Empty
        };

        string rsiStr = ToInvariantString(rsiValue);

        Console.WriteLine();
        Console.WriteLine($"RSI was recomputed: {rsiStr}{interpretation}");
        Console.WriteLine();

        OrderSide? action = rsiValue switch
        {
            < 30 when (lastAction != OrderSide.Buy) => OrderSide.Buy,
            > 70 when (lastAction != OrderSide.Sell) => OrderSide.Sell,
            _ => null,
        };

        if (action is not null)
        {
            lastAction = action;

            ILiveMarketOrder liveMarketOrder = await tradeClient.CreateMarketOrderAsync(SymbolPair.BTC_EUR, action.Value, size: 0.00015m);
            await liveMarketOrder.WaitForFillAsync();

            string message = $"Bot says: Based on the RSI signal <b>{rsiStr}{interpretation}</b> for BTC/EUR on Binance, a {action.Value} market order with size " +
                $"{ToInvariantString(liveMarketOrder.Size)} was <b>placed</b>!";

            await SendTelegramMessageAsync(message);
        }
        else
        {
            string message = interpretation != string.Empty
                ? $"Bot says: RSI signals <b>{rsiStr}{interpretation}</b> for BTC/EUR on Binance! Current price is {priceStr} EUR."
                : $"Bot says: RSI does <b>not</b> provide a clear signal for BTC/EUR on Binance! Current price is {priceStr} EUR. RSI value is {rsiStr}.";

            await SendTelegramMessageAsync(message);
        }
    }
}

// In certain cultures, the decimal separator can be comma. We prefer dots.
static string ToInvariantString(object o)
    => string.Format(CultureInfo.InvariantCulture, $"{o}");

static Quote QuoteFromCandle(Candle candle)
{
    Quote quote = new()
    {
        Date = candle.Timestamp,
        Open = candle.OpenPrice,
        High = candle.HighPrice,
        Low = candle.LowPrice,
        Close = candle.ClosePrice,
        Volume = candle.BaseVolume,
    };

    return quote;
}

async Task SendTelegramMessageAsync(string message, bool htmlSyntax = true)
{
    string chatId = HttpUtility.UrlEncode(groupId);
    message = HttpUtility.UrlEncode(message);

    using HttpClient client = new();
    string uri = htmlSyntax
        ? $"https://api.telegram.org/bot{apiToken}/sendMessage?chat_id={chatId}&parse_mode=html&text={message}"
        : $"https://api.telegram.org/bot{apiToken}/sendMessage?chat_id={chatId}&text={message}";

    using HttpRequestMessage request = new(HttpMethod.Get, uri);
    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);

    string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    Debug.Assert(response.IsSuccessStatusCode, content);
}