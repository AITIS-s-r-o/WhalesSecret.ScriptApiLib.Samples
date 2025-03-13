using WhalesSecret.ScriptApiLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;

await using ScriptApi scriptApi = await ScriptApi.CreateAsync();

// Initialize information about various exchange assets and coins.
await scriptApi.InitializeMarketAsync(ExchangeMarket.BinanceSpot);

// Connect to the exchange.
ConnectionOptions options = new(ConnectionType.MarketData);
await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(ExchangeMarket.BinanceSpot, options);

Console.WriteLine("🐋 got connected!");

DateTime endTime = DateTime.Now;
DateTime startTime = endTime.AddDays(-3);

CandlestickData candlestickData = await tradeClient.GetCandlesticksAsync(SymbolPair.BTC_USDT, CandleWidth.Hour1, startTime, endTime);

// Compute RSI.
List<float> list = candlestickData.Candles.Select(x => (float)x.ClosePrice).ToList();
List<float> rsiValues = RSIUtils.RSI.CalculateRSI(list);
float? lastRsi = rsiValues.Last();

string action = lastRsi switch
{
    null => "No data",
    > 70 => "Sell",
    < 30 => "Buy",
    _ => "Hold"
};

Console.WriteLine($"Last RSI value is: {rsiValues.Last()}. The signal tells: {action}");

// Source code from: https://codepal.ai/code-generator/query/0yb8AHbY/csharp-calculate-rsi-values
namespace RSIUtils
{
    public class RSI
    {
        public static List<float> CalculateRSI(List<float> data)
        {
            List<float> rsiValues = new();

            // Check if the data list is empty or contains only one element.
            if (data.Count < 2)
            {
                throw new ArgumentException("The data list should contain at least two elements.");
            }

            // Calculate the first RSI value.
            float firstChange = data[1] - data[0];
            float firstGain = Math.Max(firstChange, 0);
            float firstLoss = Math.Abs(Math.Min(firstChange, 0));
            float firstRS = firstGain / firstLoss;
            float firstRSI = 100 - (100 / (1 + firstRS));
            rsiValues.Add(firstRSI);

            // Calculate the subsequent RSI values.
            for (int i = 2; i < data.Count; i++)
            {
                float change = data[i] - data[i - 1];
                float gain = Math.Max(change, 0);
                float loss = Math.Abs(Math.Min(change, 0));
                float RS = (gain + (rsiValues[i - 2] * (i - 1))) / (loss + (rsiValues[i - 2] * (i - 1)));
                float RSI = 100 - (100 / (1 + RS));
                rsiValues.Add(RSI);
            }

            return rsiValues;
        }
    }
}