using System;
using System.Globalization;
using System.Text.Json.Serialization;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.SharedLib.SystemSettings;

/// <summary>
/// Configuration of Kraken Futures API keys.
/// </summary>
public class KrakenFuturesApiKeyConfig
{
    /// <summary>API key for Kraken Futures exchange.</summary>
    public string Key { get; }

    /// <summary>API secret for Kraken Futures exchange.</summary>
    public string Secret { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="key">API key for Kraken Futures exchange.</param>
    /// <param name="secret">API secret for Kraken Futures exchange.</param>
    /// <exception cref="InvalidArgumentException">Thrown if any of the parameters is <c>null</c> or empty.
    /// </exception>
    [JsonConstructor]
    public KrakenFuturesApiKeyConfig(string? key, string secret)
    {
        if (string.IsNullOrEmpty(key))
            throw new InvalidArgumentException($"'{nameof(key)}' must not be null or empty.", parameterName: nameof(key));

        if (string.IsNullOrEmpty(secret))
            throw new InvalidArgumentException($"'{nameof(secret)}' must not be null or empty.", parameterName: nameof(secret));

        this.Key = key;
        this.Secret = secret;
    }

    /// <summary>
    /// Gets exchange API credentials for KuCoin exchange.
    /// </summary>
    /// <returns>New instance of exchange API credentials for KuCoin exchange.</returns>
    public IApiIdentity GetApiIdentity()
    {
        byte[] secretBytes = Convert.FromBase64String(this.Secret);
        return KrakenApiIdentity.Create(name: "KrakenFuturesCredentials", key: this.Key, secret: secretBytes);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`]",
            nameof(this.Key), this.Key
        );
    }
}