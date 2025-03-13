using WhalesSecret.ScriptApiLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.Entities;

await using ScriptApi scriptApi = await ScriptApi.CreateAsync();

// Initialize information about various exchange assets and coins.
await scriptApi.InitializeMarketAsync(ExchangeMarket.BinanceSpot);

// Connect to the exchange.
ConnectionOptions options = new(ConnectionType.MarketData);
await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(ExchangeMarket.BinanceSpot, options);

Console.WriteLine("🐋 got connected!");