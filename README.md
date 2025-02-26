# Whale's Secret ScriptApiLib Samples

Samples that demonstrate abilities of [Whale's Secret ScriptApiLib](https://www.nuget.org/packages/WhalesSecret.ScriptApiLib).

Whale's Secret ScriptApiLib is a .NET library that provides unified API to different digital assets platforms with focus on easy of use and robust error handling.

## How to Start

The simplest example is [PublicConnection sample](blob/master/Connections/PublicConnection.cs) which simply 
connects to a selected exchange market without need to have credentials or license. Such a public connection enables you to download public market data, such as order books, 
tickers, or candlesticks.

The core of this sample looks as follows:

```csharp
await using ScriptApi scriptApi = await ScriptApi.CreateAsync().ConfigureAwait(false);

// Initialization of the market is required before connection can be created.
_ = await scriptApi.InitializeMarketAsync(exchangeMarket, timeoutCts.Token).ConfigureAwait(false);

// Market-data connection type is the only connection type that does not need exchange API credentials.
ConnectionOptions connectionOptions = new(connectionType: ConnectionType.MarketData);
await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, connectionOptions).ConfigureAwait(false);
```


## License
          
Whale's Secret ScriptApiLib Samples is published under [MIT license](LICENSE). The Whale's Secret ScriptApiLib itself is published under a commercial license that allows for free
use of all its features except for placing orders above a certain size. Each exchange market defines minimal size restrictions on orders that can be placed for trading each 
supported symbol pair. The free license of Whale's Secret ScriptApiLib allows placing orders up to a fixed multiple of the minimal order size. For more details about the license 
and the order size limits, see the [license of the Whale's Secret ScriptApiLib](https://www.nuget.org/packages/WhalesSecret.ScriptApiLib/1.0.0.64/License).
