using Skender.Stock.Indicators;
using System.Diagnostics;
using System.Globalization;
using System.Web;
using WhalesSecret.ScriptApiLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using WhalesSecret.TradeScriptLib.Exceptions;

// The Telegram API token should be in form "XXXXXXXXXX:YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY".
string? apiToken = null;

if (apiToken is null)
    throw new SanityCheckException("Please provide your Telegram API token.");

// The Telegram group identifier is in form "@my_group".
string? groupId = null;

if (groupId is null)
    throw new SanityCheckException("Please provide a Telegram group identifier.");

await using ScriptApi scriptApi = await ScriptApi.CreateAsync();

Console.WriteLine("Initializing Binance exchange.");

// Initialize information about various exchange assets and coins.
await scriptApi.InitializeMarketAsync(ExchangeMarket.BinanceSpot);

Console.WriteLine("Connecting...");

// Connect to the exchange without API credentials to work with market data.
ConnectionOptions options = new(ConnectionType.MarketData);
await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(ExchangeMarket.BinanceSpot, options);
Console.WriteLine("Connection succeeded!");

await using ICandlestickSubscription candlestickSubscription = await tradeClient.CreateCandlestickSubscriptionAsync(SymbolPair.BTC_USDT);
await using ITickerSubscription tickerSubscription = await tradeClient.CreateTickerSubscriptionAsync(SymbolPair.BTC_USDT);

// Get historical candlestick data for the last three days for 1-minute candles.
DateTime endTime = DateTime.Now;
DateTime startTime = endTime.AddDays(-3);

CandlestickData candlestickData = await tradeClient.GetCandlesticksAsync(SymbolPair.BTC_USDT, CandleWidth.Minute1, startTime, endTime);

// Compute RSI using Skender.Stock.Indicators.
// Add the package to the project using "dotnet add package Skender.Stock.Indicators".
// Add "using Skender.Stock.Indicators;" to the source file.
List<Quote> quotes = new();
foreach (Candle candle in candlestickData.Candles)
    quotes.Add(QuoteFromCandle(candle));

Task<Candle>? lastClosedCandleTask = null;

// Loop until the program is terminated using CTRL+C.
while (true)
{
    // Wait for a new ticker.
    Ticker lastTicker = await tickerSubscription.GetNewerTickerAsync();

    // If there is no task to get the latest closed candle, create one.
    lastClosedCandleTask ??= candlestickSubscription.WaitNextClosedCandlestickAsync(CandleWidth.Minute1);

    string priceStr = ToInvariantString(Math.Round(lastTicker.LastPrice!.Value, 2));
    Console.WriteLine($"The latest price is: {priceStr} USDT.");

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

        string message = interpretation != string.Empty
            ? $"Bot says: RSI signals <b>{rsiStr}{interpretation}</b> for BTC/USDT on Binance! Current price is {priceStr} USDT."
            : $"Bot says: RSI does <b>not</b> provide a clear signal for BTC/USDT on Binance! Current price is {priceStr} USDT. RSI value is {rsiStr}.";

        await SendTelegramMessageAsync(message);
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