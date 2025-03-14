using WhalesSecret.ScriptApiLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;

await using ScriptApi scriptApi = await ScriptApi.CreateAsync();

// Initialize information about various exchange assets and coins.
await scriptApi.InitializeMarketAsync(ExchangeMarket.BinanceSpot);

// Connect to the exchange.
ConnectionOptions options = new(ConnectionType.MarketData);
await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(ExchangeMarket.BinanceSpot, options);

Console.WriteLine("🐋 got connected!");

await using IOrderBookSubscription subscription = await tradeClient.CreateOrderBookSubscriptionAsync(SymbolPair.BTC_USDT);

while (true)
{
    OrderBook orderBook = await subscription.GetOrderBookAsync(getMode: OrderBookGetMode.WaitUntilNew);

    Console.WriteLine($"[{DateTime.Now}] Top of book is: {orderBook.Bids[0]}|{orderBook.Asks[0]} (spread: {orderBook.Spread}).");
    await Task.Delay(TimeSpan.FromSeconds(5));
}