using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.SharedLib.SystemSettings;

/// <summary>
/// Configuration of Kucoin API keys.
/// </summary>
public class KucoinApiKeyConfig
{
    /// <summary>API key for Kucoin exchange.</summary>
    public string Key { get; }

    /// <summary>API secret for Kucoin exchange.</summary>
    public string Secret { get; }

    /// <summary>API passphrase for Kucoin exchange.</summary>
    public string Passphrase { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="key">API key for Kucoin exchange.</param>
    /// <param name="secret">API secret for Kucoin exchange.</param>
    /// <param name="passphrase">API passphrase for Kucoin exchange.</param>
    /// <exception cref="InvalidArgumentException">Thrown if any of the parameters is <c>null</c> or empty.
    /// </exception>
    [JsonConstructor]
    public KucoinApiKeyConfig(string? key, string secret, string passphrase)
    {
        if (string.IsNullOrEmpty(key))
            throw new InvalidArgumentException($"'{nameof(key)}' must not be null or empty.", parameterName: nameof(key));

        if (string.IsNullOrEmpty(secret))
            throw new InvalidArgumentException($"'{nameof(secret)}' must not be null or empty.", parameterName: nameof(secret));

        if (string.IsNullOrEmpty(passphrase))
            throw new InvalidArgumentException($"'{nameof(passphrase)}' must not be null or empty.", parameterName: nameof(passphrase));

        this.Key = key;
        this.Secret = secret;
        this.Passphrase = passphrase;
    }

    /// <summary>
    /// Gets exchange API credentials for KuCoin exchange.
    /// </summary>
    /// <returns>New instance of exchange API credentials for KuCoin exchange.</returns>
    public IApiIdentity GetApiIdentity()
    {
        byte[] secretBytes = Encoding.UTF8.GetBytes(this.Secret);
        byte[] passphraseBytes = Encoding.UTF8.GetBytes(this.Passphrase);
        return KucoinApiIdentity.Create(name: "KucoinCredentials", key: this.Key, secret: secretBytes, passphrase: passphraseBytes);
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