using System.Globalization;
using System.Text.Json.Serialization;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesSecret.ScriptApiLib.Samples.SharedLib.SystemSettings;

/// <summary>
/// Configuration of trading bots unrelated to the bot strategy. This configuration includes license, API keys, Telegram configuration, etc.
/// </summary>
public class SystemConfig
{
    /// <summary>Whale's Secret license, or <c>null</c> to use in the free mode.</summary>
    public string? License { get; }

    /// <summary>Path to the application data folder.</summary>
    public string AppDataPath { get; }

    /// <summary>Telegram configuration, or <c>null</c> to avoid sending messages to Telegram.</summary>
    public TelegramConfig? Telegram { get; }

    /// <summary>Exchange API keys configuration.</summary>
    public ApiKeysConfig ApiKeys { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="license">Whale's Secret license, or <c>null</c> to use in the free mode.</param>
    /// <param name="appDataPath">Path to the application data folder.</param>
    /// <param name="telegram">Telegram configuration, or <c>null</c> to avoid sending messages to Telegram.</param>
    /// <param name="apiKeys">>Exchange API keys configuration.</param>
    /// <exception cref="InvalidArgumentException">Thrown if:
    /// <list type="bullet">
    /// <item><paramref name="license"/> is an empty string, or</item>
    /// <item><paramref name="appDataPath"/> is <c>null</c> or an empty string.</item>
    /// </list>
    /// </exception>
    [JsonConstructor]
    public SystemConfig(string? license, string appDataPath, TelegramConfig? telegram, ApiKeysConfig apiKeys)
    {
        if ((license is not null) && (license.Length == 0))
            throw new InvalidArgumentException($"'{nameof(license)}' must not be an empty string.", parameterName: nameof(license));

        if (string.IsNullOrEmpty(appDataPath))
            throw new InvalidArgumentException($"'{nameof(appDataPath)}' must not be null or an empty string.", parameterName: nameof(appDataPath));

        this.License = license;
        this.AppDataPath = appDataPath;
        this.Telegram = telegram;
        this.ApiKeys = apiKeys;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}=`{3}`,{4}=`{5}`,{6}=`{7}`]",
            nameof(this.License), this.License.ToBoundedString(32),
            nameof(this.AppDataPath), this.AppDataPath,
            nameof(this.Telegram), this.Telegram,
            nameof(this.ApiKeys), this.ApiKeys
        );
    }
}
