using System;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesSecret.ScriptApiLib.Samples.SharedLib.SystemSettings;

/// <summary>
/// Configuration of Binance API keys.
/// </summary>
public class BinanceApiKeyConfig
{
    /// <summary>API key for Binance exchange using HMAC algorithm, or <c>null</c> to use RSA algorithm keys.</summary>
    public string? HmacKey { get; }

    /// <summary>API secret for Binance exchange using HMAC algorithm, or <c>null</c> to use RSA algorithm keys.</summary>
    public string? HmacSecret { get; }

    /// <summary>API key for Binance exchange using RSA algorithm, or <c>null</c> to use HMAC algorithm keys.</summary>
    public string? RsaKey { get; }

    /// <summary>API secret for Binance exchange using RSA algorithm, or <c>null</c> to use HMAC algorithm keys.</summary>
    public string? RsaSecret { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="hmacKey">API key for Binance exchange using HMAC algorithm, or <c>null</c> to use RSA algorithm keys.</param>
    /// <param name="hmacSecret">API secret for Binance exchange using HMAC algorithm, or <c>null</c> to use RSA algorithm keys.</param>
    /// <param name="rsaKey">API key for Binance exchange using RSA algorithm, or <c>null</c> to use HMAC algorithm keys.</param>
    /// <param name="rsaSecret">API secret for Binance exchange using RSA algorithm, or <c>null</c> to use HMAC algorithm keys.</param>
    /// <exception cref="InvalidArgumentException">Thrown if information is provided for both HMAC and RSA algorithms, or if all keys or all secrets are <c>null</c> or empty.
    /// </exception>
    [JsonConstructor]
    public BinanceApiKeyConfig(string? hmacKey, string? hmacSecret, string? rsaKey, string? rsaSecret)
    {
        if (hmacKey is not null)
        {
            if (rsaKey is not null)
            {
                throw new InvalidArgumentException($"'{nameof(hmacKey)}' is specified, so '{nameof(rsaKey)}' must be null.",
                    parameterName: nameof(rsaKey));
            }

            if (hmacKey.Length == 0)
                throw new InvalidArgumentException($"'{nameof(hmacKey)}' must not be an empty string.", parameterName: nameof(hmacKey));

            if (string.IsNullOrEmpty(hmacSecret))
                throw new InvalidArgumentException($"'{nameof(hmacSecret)}' must be specified and not empty when '{nameof(hmacKey)}' is specified.",
                    parameterName: nameof(hmacSecret));
        }

        if (rsaKey is not null)
        {
            if (hmacKey is not null)
            {
                throw new InvalidArgumentException($"'{nameof(rsaKey)}' is specified, so '{nameof(hmacKey)}' must be null.",
                    parameterName: nameof(hmacKey));
            }

            if (rsaKey.Length == 0)
                throw new InvalidArgumentException($"'{nameof(rsaKey)}' must not be an empty string.", parameterName: nameof(rsaKey));

            if (string.IsNullOrEmpty(rsaSecret))
                throw new InvalidArgumentException($"'{nameof(rsaSecret)}' must be specified and not empty when '{nameof(rsaKey)}' is specified.",
                    parameterName: nameof(rsaSecret));
        }

        this.HmacKey = hmacKey;
        this.HmacSecret = hmacSecret;
        this.RsaKey = rsaKey;
        this.RsaSecret = rsaSecret;
    }

    /// <summary>
    /// Gets exchange API credentials for Binance exchange using HMAC or RSA algorithm.
    /// </summary>
    /// <returns>Exchange API credentials for Binance exchange using HMAC or RSA algorithm.</returns>
    public IApiIdentity GetApiIdentity()
    {
        IApiIdentity apiIdentity;
        if ((this.HmacKey is not null) && (this.HmacSecret is not null))
        {
            byte[] secretBytes = Encoding.UTF8.GetBytes(this.HmacSecret);
            apiIdentity = BinanceApiIdentity.CreateHmac(name: "BinanceHmacCredentials", key: this.HmacKey, secretBytes);
        }
        else if ((this.RsaKey is not null) && (this.RsaSecret is not null))
        {
            byte[] secretBytes = Convert.FromBase64String(this.RsaSecret);
            apiIdentity = BinanceApiIdentity.CreateHmac(name: "BinanceRsaCredentials", key: this.RsaKey, secretBytes);
        }
        else throw new SanityCheckException("Neither HMAC nor RSA credentials are available.");

        return apiIdentity;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}=`{3}`]",
            nameof(this.HmacKey), this.HmacKey,
            nameof(this.RsaKey), this.RsaKey.ToBoundedString(32)
        );
    }
}