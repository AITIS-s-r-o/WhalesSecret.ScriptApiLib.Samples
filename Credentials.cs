using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.Entities;

namespace WhalesSecret.ScriptApiLib.Samples;

/// <summary>
/// Container of exchange API credentials for samples that need to establish private connection.
/// </summary>
/// <remarks>In order for those samples to work, you have to set valid credentials below. You only need to change credentials that you are going to use.</remarks>
public static class Credentials
{
    /// <summary>API key for Binance exchange using HMAC algorithm.</summary>
    public const string BinanceHmacApiKey = "CHANGE THIS Binance HMAC API key";

    /// <summary>API key for Binance exchange using RSA algorithm.</summary>
    public const string BinanceRsaApiKey = "CHANGE THIS Binance RSA API key";

    /// <summary>API key for Kucoin exchange.</summary>
    public const string KucoinApiKey = "CHANGE THIS Kucoin API key";

    /// <summary>API secret for Binance exchange using HMAC algorithm.</summary>
    public static readonly SensitiveByteArray BinanceHmacSecret = "CHANGE THIS Binance HMAC API secret"u8;

    /// <summary>API secret for Binance exchange using RSA algorithm.</summary>
    public static readonly SensitiveByteArray BinanceRsaSecret = "CHANGE THIS Binance RSA API secret"u8;

    /// <summary>API secret for Kucoin exchange.</summary>
    public static readonly SensitiveByteArray KucoinSecret = "CHANGE THIS Kucoin secret"u8;

    /// <summary>API passphrase for Kucoin exchange.</summary>
    public static readonly SensitiveByteArray KucoinPassphrase = "CHANGE THIS Kucoin passphrase"u8;

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
    /// Gets exchange API credentials for Kucoin exchange.
    /// </summary>
    /// <returns>Exchange API credentials for Kucoin exchange.</returns>
    public static IApiIdentity GetKucoinApiIdentity()
        => KucoinApiIdentity.Create(name: "KucoinCredentials", key: KucoinApiKey, secret: KucoinSecret, passphrase: KucoinPassphrase);
}