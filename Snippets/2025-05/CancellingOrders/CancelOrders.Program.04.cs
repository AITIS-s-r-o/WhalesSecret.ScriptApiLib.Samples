using WhalesSecret.ScriptApiLib;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;

// Use the Binance Testnet.
CreateOptions createOptions = new(connectToBinanceSandbox: true);
await using ScriptApi scriptApi = await ScriptApi.CreateAsync(createOptions);

// Specify your Binance Testnet API credentials.
using IApiIdentity apiIdentity = BinanceApiIdentity.CreateHmac(name: "MyApiKey", key: "<KEY>", secret: Convert.FromBase64String("<SECRET>"));
scriptApi.SetCredentials(apiIdentity);

// Connect.
ITradeApiClient client = await scriptApi.ConnectAsync(ExchangeMarket.BinanceSpot);
Print("Ready to trade!");

Print("About to cancel all open orders.");
await client.CancelAllOrdersAsync();

// The behavior can be verified in the following manner:
IReadOnlyList<ILiveOrder> orders = await client.GetOpenOrdersAsync(OrderFilterOptions.AllOrders);
Print($"There are {orders.Count} open order(s).");

static void Print(string msg)
    => Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {msg}");