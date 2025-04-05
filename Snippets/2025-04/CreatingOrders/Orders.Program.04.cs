using WhalesSecret.ScriptApiLib;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;

// Use the Binance Testnet.
CreateOptions createOptions = new(connectToBinanceSandbox: true);
await using ScriptApi scriptApi = await ScriptApi.CreateAsync(createOptions);

// Specify your Binance Testnet API credentials.
using IApiIdentity apiIdentity = BinanceApiIdentity.CreateHmac(name: "MyApiKey", key: "<KEY>", secret: Convert.FromBase64String("<SECRET>"));
scriptApi.SetCredentials(apiIdentity);

// Connect.
ITradeApiClient client = await scriptApi.ConnectAsync(ExchangeMarket.BinanceSpot);
Console.WriteLine("Ready to trade!");

// WARNING: Placing of non-small orders is a paid feature of the ScriptApiLib.
ILiveLimitOrder order = await client.CreateLimitOrderAsync(SymbolPair.BTC_EUR, OrderSide.Sell, price: 75_000m, size: 1.0m);