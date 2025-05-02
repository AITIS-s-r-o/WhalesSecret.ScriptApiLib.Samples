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

IReadOnlyList<ILiveOrder> orders = await client.GetOpenOrdersAsync(OrderFilterOptions.AllOrders);
Print($"There are {orders.Count} open order(s).");

// Now we can, for example, print when the orders were created, or we can cancel the orders one by one, etc.
foreach (ILiveOrder o in orders)
    Print($"Order '{o.ExchangeOrderId}' was created at {o.CreateTime} UTC.");

static void Print(string msg)
    => Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {msg}");