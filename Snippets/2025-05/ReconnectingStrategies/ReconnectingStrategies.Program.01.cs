using WhalesSecret.ScriptApiLib;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.ConnectionStrategy;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;

// Use the Binance Testnet.
CreateOptions createOptions = new(connectToBinanceSandbox: true);
await using ScriptApi scriptApi = await ScriptApi.CreateAsync(createOptions);

// Specify your Binance Testnet API credentials.
using IApiIdentity apiIdentity = BinanceApiIdentity.CreateHmac(name: "MyApiKey", key: "<KEY>", secret: Convert.FromBase64String("<SECRET>"));
scriptApi.SetCredentials(apiIdentity);

// Use the FailInstantlyIfNotConnected connection strategy.
ConnectionOptions connectionOptions = new(FailInstantlyIfNotConnected.Instance);
ITradeApiClient client = await scriptApi.ConnectAsync(ExchangeMarket.BinanceSpot, connectionOptions);
Print("Connected to Binance sandbox.\n");

// Use: sudo netsh interface set interface name="Wi-Fi" admin=DISABLED
Print("Waiting 10 seconds for you to make sure your machine is offline.");
await Task.Delay(10_000);

try
{
    Print("Attempt to create a market order while disconnected.");
    await client.CreateMarketOrderAsync(SymbolPair.BTC_EUR, OrderSide.Buy, size: 0.0001m);

    throw new SanityCheckException("Should not be called.");
}
catch (NotConnectedException)
{
    Print($"{nameof(NotConnectedException)} was thrown as expected!\n");
}

try
{
    // Similarly, getting open orders will throw the same exception, even
    // though the method is querying a local storage and does not send an
    // explicit API request to Binance. The issue is that "not being online
    // could lead to returning wrong data".
    Print("Attempt to retrieve all open orders while disconnected.");
    IReadOnlyList<ILiveOrder> openOrders = await client.GetOpenOrdersAsync();

    throw new SanityCheckException("Should not be called.");
}
catch (NotConnectedException)
{
    Print($"{nameof(NotConnectedException)} was thrown as expected!\n");
}

// Use: sudo netsh interface set interface name="Wi-Fi" admin=ENABLED
Print("The demonstration is over. You can re-enable your Internet access.");

static void Print(string msg)
    => Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {msg}");