using System;
using System.Globalization;
using System.Text.Json.Serialization;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.SharedLib.SystemSettings;

/// <summary>
/// Exchange API keys configuration.
/// </summary>
public class ApiKeysConfig
{
    /// <summary>Configuration of Binance Spot API keys, or <c>null</c> not to configure API keys for Binance.</summary>
    public BinanceApiKeyConfig? Binance { get; }

    /// <summary>Configuration of KuCoin Spot API keys, or <c>null</c> not to configure API keys for KuCoin.</summary>
    public KucoinApiKeyConfig? Kucoin { get; }

    /// <summary>Configuration of Kraken Spot API keys, or <c>null</c> not to configure API keys for Kraken.</summary>
    public KrakenApiKeyConfig? Kraken { get; }

    /// <summary>Configuration of Kraken Futures API keys, or <c>null</c> not to configure API keys for Kraken Futures.</summary>
    public KrakenFuturesApiKeyConfig? KrakenFutures { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="binance">Configuration of Binance Spot API keys, or <c>null</c> not to configure API keys for Binance Spot.</param>
    /// <param name="kucoin">Configuration of KuCoin Spot API keys, or <c>null</c> not to configure API keys for KuCoin Spot.</param>
    /// <param name="kraken">Configuration of Kraken Spot API keys, or <c>null</c> not to configure API keys for Kraken Spot.</param>
    /// <param name="krakenFutures">Configuration of Kraken Futures API keys, or <c>null</c> not to configure API keys for Kraken Futures.</param>
    [JsonConstructor]
    public ApiKeysConfig(BinanceApiKeyConfig? binance, KucoinApiKeyConfig? kucoin, KrakenApiKeyConfig? kraken, KrakenFuturesApiKeyConfig? krakenFutures)
    {
        this.Binance = binance;
        this.Kucoin = kucoin;
        this.Kraken = kraken;
        this.KrakenFutures = krakenFutures;
    }

    /// <summary>
    /// Gets exchange API credentials for Binance Spot exchange.
    /// </summary>
    /// <returns>Exchange API credentials for Binance Spot exchange.</returns>
    /// <exception cref="InvalidOperationException">Thrown if Binance Spot API keys are not configured.</exception>
    public IApiIdentity GetBinanceApiIdentity()
    {
        if (this.Binance is null)
            throw new InvalidOperationException("Binance Spot API keys are not configured.");

        return this.Binance.GetApiIdentity();
    }

    /// <summary>
    /// Gets exchange API credentials for KuCoin Spot exchange.
    /// </summary>
    /// <returns>Exchange API credentials for KuCoin Spot exchange.</returns>
    /// <exception cref="InvalidOperationException">Thrown if KuCoin Spot API keys are not configured.</exception>
    public IApiIdentity GetKucoinApiIdentity()
    {
        if (this.Kucoin is null)
            throw new InvalidOperationException("KuCoin Spot API keys are not configured.");

        return this.Kucoin.GetApiIdentity();
    }

    /// <summary>
    /// Gets exchange API credentials for Kraken Spot exchange.
    /// </summary>
    /// <returns>Exchange API credentials for Kraken Spot exchange.</returns>
    /// <exception cref="InvalidOperationException">Thrown if Kraken Spot API keys are not configured.</exception>
    public IApiIdentity GetKrakenApiIdentity()
    {
        if (this.Kraken is null)
            throw new InvalidOperationException("Kraken Spot API keys are not configured.");

        return this.Kraken.GetApiIdentity();
    }

    /// <summary>
    /// Gets exchange API credentials for Kraken Futures exchange.
    /// </summary>
    /// <returns>Exchange API credentials for Kraken Futures exchange.</returns>
    /// <exception cref="InvalidOperationException">Thrown if Kraken Futures API keys are not configured.</exception>
    public IApiIdentity GetKrakenFuturesApiIdentity()
    {
        if (this.KrakenFutures is null)
            throw new InvalidOperationException("Kraken Futures API keys are not configured.");

        return this.KrakenFutures.GetApiIdentity();
    }

    /// <summary>
    /// Gets exchange API credentials for the given exchange.
    /// </summary>
    /// <param name="exchangeMarket">Exchange market for which to get API credentials.</param>
    /// <returns>Exchange API credentials for the given exchange.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the requested API keys are not configured.</exception>
    public IApiIdentity GetApiIdentity(ExchangeMarket exchangeMarket)
    {
        return exchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => this.GetBinanceApiIdentity(),
            ExchangeMarket.KucoinSpot => this.GetKucoinApiIdentity(),
            ExchangeMarket.KrakenSpot => this.GetKrakenApiIdentity(),
            ExchangeMarket.KrakenFutures => this.GetKrakenFuturesApiIdentity(),
            _ => throw new SanityCheckException($"Unsupported exchange market {exchangeMarket} provided."),
        };
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}=`{3}`,{4}=`{5}`,{6}=`{7}`]",
            nameof(this.Binance), this.Binance,
            nameof(this.Kucoin), this.Kucoin,
            nameof(this.Kraken), this.Kraken,
            nameof(this.KrakenFutures), this.KrakenFutures
        );
    }
}