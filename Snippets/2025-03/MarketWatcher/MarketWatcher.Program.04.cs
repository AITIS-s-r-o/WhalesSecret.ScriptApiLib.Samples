using Skender.Stock.Indicators;
using WhalesSecret.ScriptApiLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using System.Diagnostics;
using System.Web;

await using ScriptApi scriptApi = await ScriptApi.CreateAsync();

// Initialize information about various exchange assets and coins.
await scriptApi.InitializeMarketAsync(ExchangeMarket.BinanceSpot);

// Connect to the exchange.
ConnectionOptions options = new(ConnectionType.MarketData);
await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(ExchangeMarket.BinanceSpot, options);

Console.WriteLine("🐋 got connected!");

DateTime endTime = DateTime.Now;
DateTime startTime = endTime.AddDays(-3);

CandlestickData candlestickData = await tradeClient.GetCandlesticksAsync(SymbolPair.BTC_USDT, CandleWidth.Hour1, startTime, endTime);

// Compute RSI using:
// <PackageReference Include="Skender.Stock.Indicators" Version="2.6.1" />
RsiResult? lastRsi = ComputeRsi(candlestickData.Candles);

if (lastRsi is not null)
{
    string? signal = lastRsi.Rsi switch
    {
        < 30 => "oversold",
        > 70 => "overbought",
        _ => null
    };

    if (signal is not null)
    {
        Console.WriteLine($"Last RSI value is: {lastRsi.Rsi}. The signal tells: {signal}");
        await SendTelegramMessageAsync($"Your daily observer: RSI signals {signal} for BTC/USDT on Binance!");
    }
    else
    {
        Console.WriteLine("No RSI signal at the moment, try it later!");
    }
}
else
{
    Console.WriteLine($"No data to compute RSI.");
}

static async Task SendTelegramMessageAsync(string message)
{
    string chatId = HttpUtility.UrlEncode("<YOUR_PUBLIC_TELEGRAM_CHANNEL_NAME>");
    string apiToken = "XXXXXXXXXX:YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY";
    message = HttpUtility.UrlEncode(message);

    using HttpClient client = new();
    string uri = $"https://api.telegram.org/bot{apiToken}/sendMessage?chat_id={chatId}&text={message}";
    using HttpRequestMessage request = new(HttpMethod.Get, uri);
    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);

    string content = await response.Content.ReadAsStringAsync();
    Debug.Assert(response.IsSuccessStatusCode, content);
}

static RsiResult? ComputeRsi(IEnumerable<Candle> candles)
{
    IEnumerable<Quote> quotes = candles.Select(c => new Quote()
    {
        Date = c.Timestamp,
        Open = c.OpenPrice,
        High = c.HighPrice,
        Low = c.LowPrice,
        Close = c.ClosePrice,
        Volume = c.BaseVolume,
    });

    IEnumerable<RsiResult> results = quotes.GetRsi();

    return results.Last();
}