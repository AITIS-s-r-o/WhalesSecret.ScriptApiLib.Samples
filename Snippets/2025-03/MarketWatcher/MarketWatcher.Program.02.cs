using WhalesSecret.ScriptApiLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;

await using ScriptApi scriptApi = await ScriptApi.CreateAsync();

Console.WriteLine("Connecting...");

// Connect to the exchange without API credentials to work with market data.
ConnectionOptions options = new(ConnectionType.MarketData);
await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(ExchangeMarket.BinanceSpot, options);
Console.WriteLine("Connection succeeded!");

await using ICandlestickSubscription subscription = await tradeClient.CreateCandlestickSubscriptionAsync(SymbolPair.BTC_USDT);

// Get historical candlestick data for the last three days for 1-minute candles.
DateTime endTime = DateTime.Now;
DateTime startTime = endTime.AddDays(-3);

CandlestickData candlestickData = await tradeClient.GetCandlesticksAsync(SymbolPair.BTC_USDT, CandleWidth.Minute1, startTime, endTime);