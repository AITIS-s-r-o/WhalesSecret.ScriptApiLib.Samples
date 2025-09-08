using System;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.SharedLib.SystemSettings;

/// <summary>
/// Exchange API keys configuration.
/// </summary>
public class ApiKeysConfig
{
    /// <summary>Configuration of Binance API keys, or <c>null</c> not to configure API keys for Binance.</summary>
    public BinanceApiKeyConfig? Binance { get; }

    /// <summary>Configuration of KuCoin API keys, or <c>null</c> not to configure API keys for KuCoin.</summary>
    public KucoinApiKeyConfig? Kucoin { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="binance">Configuration of Binance API keys, or <c>null</c> not to configure API keys for Binance.</param>
    /// <param name="kucoin">Configuration of KuCoin API keys, or <c>null</c> not to configure API keys for KuCoin.</param>
    [JsonConstructor]
    public ApiKeysConfig(BinanceApiKeyConfig? binance, KucoinApiKeyConfig? kucoin)
    {
        this.Binance = binance;
        this.Kucoin = kucoin;
    }

    /// <summary>
    /// Gets exchange API credentials for Binance exchange.
    /// </summary>
    /// <returns>Exchange API credentials for Binance exchange.</returns>
    /// <exception cref="InvalidOperationException">Thrown if Binance API keys are not configured.</exception>
    public IApiIdentity GetBinanceApiIdentity()
    {
        if (this.Binance is null)
            throw new InvalidOperationException("Binance API keys are not configured.");

        return this.Binance.GetApiIdentity();
    }

    /// <summary>
    /// Gets exchange API credentials for KuCoin exchange.
    /// </summary>
    /// <returns>Exchange API credentials for KuCoin exchange.</returns>
    /// <exception cref="InvalidOperationException">Thrown if KuCoin API keys are not configured.</exception>
    public IApiIdentity GetKucoinApiIdentity()
    {
        if (this.Kucoin is null)
            throw new InvalidOperationException("KuCoin API keys are not configured.");

        return this.Kucoin.GetApiIdentity();
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
            _ => throw new SanityCheckException($"Unsupported exchange market {exchangeMarket} provided."),
        };
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}=`{3}`]",
            nameof(this.Binance), this.Binance,
            nameof(this.Kucoin), this.Kucoin
        );
    }
}