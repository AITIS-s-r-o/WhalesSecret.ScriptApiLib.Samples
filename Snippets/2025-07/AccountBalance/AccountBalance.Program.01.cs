using System.Text;
using WhalesSecret.ScriptApiLib;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Account;

await using ScriptApi scriptApi = await ScriptApi.CreateAsync();

// Specify your KuCoin API credentials.
using IApiIdentity apiIdentity = KucoinApiIdentity.Create(name: "MyApiKey", key: Environment.GetEnvironmentVariable("WS_KUCOIN_KEY") ?? string.Empty,
    secret: Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("WS_KUCOIN_SECRET") ?? string.Empty),
    passphrase: Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("WS_KUCOIN_PASSPHRASE") ?? string.Empty));

scriptApi.SetCredentials(apiIdentity);

// Connect.
ITradeApiClient client = await scriptApi.ConnectAsync(ExchangeMarket.KucoinSpot);
Print("Connected to KuCoin.");

ExchangeAccountInformation info = client.GetLatestExchangeAccountInformation();
Print("Assets (available balance / total balance):");

foreach ((string symbol, AccountSymbolInformation accountInfo) in info.SymbolsInformation)
{
    Print($"* {symbol}: {accountInfo.AvailableBalance} / {accountInfo.TotalBalance}");
}

static void Print(string msg)
    => Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {msg}");