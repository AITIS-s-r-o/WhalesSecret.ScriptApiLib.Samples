using WhalesSecret.ScriptApiLib;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.API.TradingV1;
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

// Connect.
ITradeApiClient client = await scriptApi.ConnectAsync(ExchangeMarket.BinanceSpot);
Print("Ready to trade!");

// Make sure the price is greater than the current market price.
Print("Place a limit sell order to sell 0.00015 BTC @ 120,000 EUR.");
ILiveLimitOrder order = await client.CreateLimitOrderAsync(SymbolPair.BTC_EUR, OrderSide.Sell, price: 120_000m, size: 0.00015m);

Print("Wait 2 seconds.");
await Task.Delay(2_000);

Print($"Cancel the order '{order.ExchangeOrderId}'.");
try
{
    await client.CancelOrderAsync(order); // <--- Actually cancels the order.
}
catch (NotFoundException e)
{
    Print($"[!] {nameof(NotFoundException)}: {e.Message}");
}

Print($"Attempt to cancel the order '{order.ExchangeOrderId}' again.");
try
{
    await client.CancelOrderAsync(order);
    throw new SanityCheckException("No, no, no. This exception should not be thrown.");
}
catch (NotFoundException e)
{
    Print($"[!] {nameof(NotFoundException)}: {e.Message}");
}

static void Print(string msg)
    => Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {msg}");