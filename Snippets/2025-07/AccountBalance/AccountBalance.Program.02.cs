using System.Net.Http.Json;
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

decimal totalValue = await ConvertAsync(info);
Print($"Total value is {totalValue} USD.");

/// <summary>
/// Compute value of assets in USD using CoinGecko's simple price API.
/// </summary>
/// <seealso href="https://docs.coingecko.com/v3.0.1/reference/simple-price"/>
/// <example>https://api.coingecko.com/api/v3/simple/price?symbols=btc,ltc&vs_currencies=usd</example>
static async Task<decimal> ConvertAsync(ExchangeAccountInformation info)
{
    string symbols = string.Join(',', info.SymbolsInformation.Keys);

    using HttpClient httpClient = new();
    using HttpResponseMessage response = await httpClient
        .GetAsync($"https://api.coingecko.com/api/v3/simple/price?symbols={symbols}&vs_currencies=usd");
    var data = await response.Content.ReadFromJsonAsync<Dictionary<string, Dictionary<string, decimal>>>();
    if (data is null)
    {
        Print("Failed to retrieve exchange-rate data from CoinGecko.");
        return -1;
    }

    decimal usdValue = 0;

    foreach (string coin in info.SymbolsInformation.Keys)
    {
        if (data.TryGetValue(coin.ToLowerInvariant(), out var dict) && dict.TryGetValue("usd", out decimal rate))
        {
            usdValue += rate * info.SymbolsInformation[coin].TotalBalance;
        }
        else Print($"No USD value found for '{coin}' asset.");
    }

    return usdValue;
}

static void Print(string msg)
    => Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {msg}");