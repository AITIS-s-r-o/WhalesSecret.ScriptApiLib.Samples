using System;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.Entities;

namespace WhalesSecret.ScriptApiLib.Samples.SharedLib;

/// <summary>
/// Container of exchange API and other credentials for basic samples that need to establish a private exchange connection or send authenticated messages.
/// </summary>
/// <remarks>In order for those samples to work, you have to set valid credentials below. You only need to change credentials that you are going to use.</remarks>
public static class Credentials
{
    /// <summary>API token for Telegram bot.</summary>
    /// <seealso href="https://medium.com/@whales_secret/trading-bot-in-c-part-2-notifications-1257dc1f4c48"/>
    public const string TelegramApiToken = "CHANGE THIS TO Telegram API token";

    /// <summary>API key for Binance exchange using HMAC algorithm.</summary>
    public const string BinanceHmacApiKey = "CHANGE THIS TO Binance HMAC API key";

    /// <summary>API key for Binance exchange using RSA algorithm.</summary>
    public const string BinanceRsaApiKey = "CHANGE THIS TO Binance RSA API key";

    /// <summary>API secret for Binance exchange using RSA algorithm.</summary>
    public const string BinanceRsaSecret = "CHANGE THIS TO BASE64 encoded Binance RSA API secret";

    /// <summary>API key for KuCoin exchange.</summary>
    public const string KucoinApiKey = "CHANGE THIS TO KuCoin API key";

    /// <summary>API key for Kraken exchange.</summary>
    public const string KrakenApiKey = "CHANGE THIS TO Kraken API key";

    /// <summary>API secret for Binance exchange using HMAC algorithm.</summary>
    public static readonly SensitiveByteArray BinanceHmacSecret = "CHANGE THIS TO Binance HMAC API secret"u8;

    /// <summary>API secret for KuCoin exchange.</summary>
    public static readonly SensitiveByteArray KucoinSecret = "CHANGE THIS TO KuCoin secret"u8;

    /// <summary>API passphrase for KuCoin exchange.</summary>
    public static readonly SensitiveByteArray KucoinPassphrase = "CHANGE THIS TO KuCoin passphrase"u8;

    /// <summary>API secret for Kraken exchange.</summary>
    public static readonly SensitiveByteArray KrakenSecret = "CHANGE THIS TO Kraken secret"u8;

    /// <summary>
    /// Gets exchange API credentials for Binance exchange using HMAC algorithm.
    /// </summary>
    /// <returns>Exchange API credentials for Binance exchange using HMAC algorithm.</returns>
    public static IApiIdentity GetBinanceHmacApiIdentity()
        => BinanceApiIdentity.CreateHmac(name: "BinanceHmacCredentials", key: BinanceHmacApiKey, BinanceHmacSecret);

    /// <summary>
    /// Gets exchange API credentials for Binance exchange using RSA algorithm.
    /// </summary>
    /// <returns>Exchange API credentials for Binance exchange using RSA algorithm.</returns>
    public static IApiIdentity GetBinanceRsaApiIdentity()
        => BinanceApiIdentity.CreateRsa(name: "BinanceRsaCredentials", key: BinanceRsaApiKey, Convert.FromBase64String(BinanceRsaSecret));

    /// <summary>
    /// Gets exchange API credentials for KuCoin exchange.
    /// </summary>
    /// <returns>Exchange API credentials for KuCoin exchange.</returns>
    public static IApiIdentity GetKucoinApiIdentity()
        => KucoinApiIdentity.Create(name: "KuCoinCredentials", key: KucoinApiKey, secret: KucoinSecret, passphrase: KucoinPassphrase);

    /// <summary>
    /// Gets exchange API credentials for Kraken exchange.
    /// </summary>
    /// <returns>Exchange API credentials for Kraken exchange.</returns>
    public static IApiIdentity GetKrakenApiIdentity()
        => KrakenApiIdentity.Create(name: "KrakenCredentials", key: KrakenApiKey, secret: KrakenSecret);
}