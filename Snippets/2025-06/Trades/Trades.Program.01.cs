using System.Text;
using WhalesSecret.ScriptApiLib;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;

await using ScriptApi scriptApi = await ScriptApi.CreateAsync();

// Specify your KuCoin API credentials.
using IApiIdentity apiIdentity = KucoinApiIdentity.Create(name: "MyApiKey", key: Environment.GetEnvironmentVariable("WS_KUCOIN_KEY") ?? string.Empty,
    secret: Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("WS_KUCOIN_SECRET") ?? string.Empty),
    passphrase: Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("WS_KUCOIN_PASSPHRASE") ?? string.Empty));

scriptApi.SetCredentials(apiIdentity);

// Connect.
ITradeApiClient client = await scriptApi.ConnectAsync(ExchangeMarket.KucoinSpot);
Print("Connected to KuCoin.");

DateOnly yesterdayDate = DateOnly.FromDateTime(DateTime.UtcNow);

IReadOnlyList<ITrade> trades = await client.GetTradesAsync(yesterdayDate);

Print($"For {yesterdayDate:yyyy-MM-dd}, there were {trades.Count} trades:");

int i = 0;
foreach (ITrade trade in trades)
{
    i++;
    Print($"#{i}: {trade}");
}

static void Print(string msg)
    => Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {msg}");