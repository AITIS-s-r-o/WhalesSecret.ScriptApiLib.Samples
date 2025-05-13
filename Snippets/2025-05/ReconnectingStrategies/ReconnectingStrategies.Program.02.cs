using WhalesSecret.ScriptApiLib;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.ConnectionStrategy;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;

// Use the Binance Testnet.
CreateOptions createOptions = new(connectToBinanceSandbox: true);
await using ScriptApi scriptApi = await ScriptApi.CreateAsync(createOptions);

// Specify your Binance Testnet API credentials.
using IApiIdentity apiIdentity = BinanceApiIdentity.CreateHmac(name: "MyApiKey", key: "<KEY>", secret: Convert.FromBase64String("<SECRET>"));
scriptApi.SetCredentials(apiIdentity);

// Use the BlockUntilReconnectedOrTimeout connection strategy.
BlockUntilReconnectedOrTimeout connectionStrategy = new(preRequestTimeout: TimeSpan.FromSeconds(30));
ConnectionOptions connectionOptions = new(connectionStrategy);
ITradeApiClient client = await scriptApi.ConnectAsync(ExchangeMarket.BinanceSpot, connectionOptions);
Print("Connected to Binance sandbox.");

// Use: sudo netsh interface set interface name="Wi-Fi" admin=DISABLED
Print("Waiting 10 seconds for you to make sure your machine is offline.");
await Task.Delay(10_000);

Task<ILiveLimitOrder> createOrderTask = client.CreateLimitOrderAsync(SymbolPair.BTC_EUR, OrderSide.Buy, price: 90_000m, size: 0.0001m);

// Use: sudo netsh interface set interface name="Wi-Fi" admin=ENABLE
Print("Waiting 10 seconds for you to make sure your machine is online.");
await Task.Delay(10_000);

ILiveLimitOrder limitOrder = await createOrderTask;
Print($"Limit order '{limitOrder}' was placed!");

static void Print(string msg)
    => Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {msg}");