# Whale's Secret ScriptApiLib Samples

Samples that demonstrate abilities of [Whale's Secret ScriptApiLib](https://www.nuget.org/packages/WhalesSecret.ScriptApiLib).

Whale's Secret ScriptApiLib is a .NET library that provides unified API to different digital assets platforms with focus on easy of use and robust error handling.

## How to Start

The simplest example is [PublicConnection sample](BasicSamples/Connections/PublicConnection.cs) which simply connects to a selected exchange market without need to have credentials or license.
Such a public connection enables you to download public market data, such as order books, tickers, or candlesticks.

The core of this sample looks as follows:

```csharp
await using ScriptApi scriptApi = await ScriptApi.CreateAsync().ConfigureAwait(false);

// Market-data connection type is the only connection type that does not need exchange API credentials.
ConnectionOptions connectionOptions = new(connectionType: ConnectionType.MarketData);
await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, connectionOptions).ConfigureAwait(false);
```

## Running Basic Samples

Make sure [.NET9](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) is installed on your computer. Build the samples .NET solution using the latest MSVS or any other .NET 
tool of your choice. Then simply run the main executable as a console application in order to see the following usage instructions:

```
Usage: WhalesSecret.ScriptApiLib.Samples.BasicSamples <sampleName> <exchangeMarket>

    sampleName - Name of the sample to run. Following values are supported:

        Accounts/ExchangeAccount - Demonstrates how to get balances for assets in the exchange account wallet and basic trading fees. Requires credentials.
        Connections/Public - Demonstrates how to connect to an exchange via public connection.
        Connections/Private - Demonstrates how to connect to an exchange via private connection using exchange API credentials. Requires credentials.
        Exchanges/ExchangeInformation - Demonstrates what kind of information can we get from the exchange market initialization.
        Indicators/RSI - Demonstrates how to integrate an RSI indicator from a third party with Whale's Secrets ScriptApiLib. The sample also demonstrates how to retrieve historical candle data.
        Subscriptions/Candle.Basic - Basic candle subscription sample. Demonstrates how a candle subscription can created and consumed.
        Subscriptions/Candle.Set - Advanced candle subscription sample. Demonstrates how multiple candlestick subscriptions can be created and consumed at the same time.
        Subscriptions/OrderBook.Basic - Basic order book subscription sample. Demonstrates how an order book subscription can be created and consumed.
        Subscriptions/OrderBook.Set - Advanced order book subscription sample. Demonstrates how multiple order book subscriptions can be created and consumed at the same time.
        Subscriptions/OrderBook.Arbitrage - Advanced order book subscription sample. Demonstrates how to monitor order books on two different exchanges at the same time.
        Subscriptions/Ticker.Basic - Basic ticker subscription sample. Demonstrates how a ticker subscription can be created and consumed.
        Subscriptions/Ticker.Set - Advanced ticker subscription sample. Demonstrates how multiple ticker subscriptions can be created and consumed at the same time.
        Trading/Order.Size.Small - Basic order sample. Demonstrates how small-sized orders can be placed. Requires credentials.
        Trading/Order.Size.Large - Basic order sample. Demonstrates how larger-sized orders can be placed. Requires credentials and a valid license.
        Trading/Order.Updates - Basic order's updates sample. Demonstrates how order's updates can be consumed. Requires credentials.
        Trading/Order.Builder - Basic order request builder sample. Demonstrates how orders can be build using the builder pattern. Requires credentials.
        Trading/Order.Open.List - Sample that demonstrates how to get a list of open orders. Requires credentials.
        Trading/Order.CancelAll - Basic order's sample that demonstrates how to cancel all orders. Requires credentials.
        Trading/TradeOrder.History - Demonstrates getting historical trades and orders records. Requires credentials.
        Trading/Budget - Demonstrates working with trading strategy budget. Requires credentials.
        Trading/BracketedOrder - Sample that demonstrates placing bracketed order. Requires credentials.
        Trading/Interactive - Sample that demonstrates creating orders and cancelling them in an interactive mode. Requires credentials.

    exchangeMarket - Exchange market to use in the sample. Supported values are BinanceSpot,KucoinSpot
```

Choose the sample you want and run it in console:

```
WhalesSecret.ScriptApiLib.Samples Subscriptions/Ticker.Basic BinanceSpot
```

## Advanced Demos

In the `AdvancedDemos` folder you can find more complex samples, eaching having its own project:
- **DCA** - Dollar Cost Averaging trading bot. This bot periodically places market orders in order to buy (or sell) the selected base asset for the constant amount of the selected quote asset. The bot also create reports about its performance and writes the report history it into a CSV file.
- **MomentumBreakout** - More complex trading bot that implements Momentum Breakout trading strategy. This bot monitors short-period and long-period Exponential Moving Averages (EMAs) and looks for momentum breakouts. Each trade must be confirmed by Relative Strength Index (RSI) indicator to avoid buying in overbought conditions and selling in oversold conditions. Further, we require certain minimal volume to confirm strength of the breakout. The bot also uses Average True Range (ATR) to measure volatility and set dynamic stop-loss and take-profit levels. For full description of the algorithm please see description of [its Program class](AdvancedDemos/MomentumBreakout/Program.cs).

## Credentials

Some samples require valid API credentials. This means you have to have an account on the exchange market you want to run sample against and you have to modify
[Credentials.cs](Credentials.cs) file to include your real credentials.

## License
          
Whale's Secret ScriptApiLib Samples is published under [MIT license](LICENSE). The Whale's Secret ScriptApiLib itself is published under a commercial license that allows for free
use of all its features except for placing orders above a certain size. Each exchange market defines minimal size restrictions on orders that can be placed for trading each 
supported symbol pair. The free license of Whale's Secret ScriptApiLib allows placing orders up to a fixed multiple of the minimal order size. For more details about the license 
and the order size limits, see the [license of the Whale's Secret ScriptApiLib](https://www.nuget.org/packages/WhalesSecret.ScriptApiLib/1.0.0.64/License).

If you want to run the samples with a purchased Whale's Secret ScriptApiLib license, you need to modify [License.cs](License.cs) file to include your license.
