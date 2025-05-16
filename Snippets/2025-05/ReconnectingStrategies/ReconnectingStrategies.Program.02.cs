using WhalesSecret.ScriptApiLib;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.ConnectionStrategy;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using WhalesSecret.TradeScriptLib.Entities.Orders;

// Use the Binance Testnet.
CreateOptions createOptions = new(connectToBinanceSandbox: true);
await using ScriptApi scriptApi = await ScriptApi.CreateAsync(createOptions);

// Specify your Binance Testnet API credentials.
using IApiIdentity apiIdentity = BinanceApiIdentity.CreateHmac(name: "MyApiKey", key: "<KEY>", secret: Convert.FromBase64String("<SECRET>"));
scriptApi.SetCredentials(apiIdentity);

// Use the BlockUntilReconnectedOrTimeout connection strategy.
BlockUntilReconnectedOrTimeout connectionStrategy = new(preRequestTimeout: TimeSpan.FromSeconds(60));
ConnectionOptions connectionOptions = new(connectionStrategy, onConnectedAsync: OnConnectedAsync, onDisconnectedAsync: OnDisconnectedAsync);
ITradeApiClient client = await scriptApi.ConnectAsync(ExchangeMarket.BinanceSpot, connectionOptions);
Print("Connected to Binance sandbox.");

IOrderBookSubscription subscription = await client.CreateOrderBookSubscriptionAsync(SymbolPair.BTC_EUR);

// Use: sudo netsh interface set interface name="Wi-Fi" admin=DISABLED
Print("Waiting 40 seconds for you to make sure your machine is offline.");
await Task.Delay(40_000);

Task<ILiveLimitOrder> createOrderTask = client.CreateLimitOrderAsync(SymbolPair.BTC_EUR, OrderSide.Buy, price: 90_000m, size: 0.0001m);
Task<OrderBook> orderBookTask = subscription.GetOrderBookAsync(CancellationToken.None);

// Use: sudo netsh interface set interface name="Wi-Fi" admin=ENABLE
Print("Make sure your machine is online. Waiting 60 seconds.");
await Task.Delay(60_000);

// The assumption is that we are connected again.
ILiveLimitOrder limitOrder = await createOrderTask;
Print($"Limit order '{limitOrder}' was placed!");

OrderBook orderBook = await orderBookTask;
Print("Received order book:");
Print(orderBook.ToFullString());

Task OnConnectedAsync(ITradeApiClient tradeApiClient)
{
    Print("Connected again.");
    return Task.CompletedTask;
}

Task OnDisconnectedAsync(ITradeApiClient tradeApiClient)
{
    Print("Disconnected.");
    return Task.CompletedTask;
}

static void Print(string msg)
    => Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {msg}");