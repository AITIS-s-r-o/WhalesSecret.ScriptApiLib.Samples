using System.Globalization;
using System.Text.Json.Serialization;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.SharedLib.SystemSettings;

/// <summary>
/// Telegram configuration.
/// </summary>
/// <seealso href="https://medium.com/@whales_secret/trading-bot-in-c-part-2-notifications-1257dc1f4c48"/>
public class TelegramConfig
{
    /// <summary>Telegram group ID to send messages to.</summary>
    public string GroupId { get; }

    /// <summary>API token for Telegram bot.</summary>
    public string ApiToken { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="groupId">Telegram group ID to send messages to.</param>
    /// <param name="apiToken">API token for Telegram bot.</param>
    /// <exception cref="InvalidArgumentException">Thrown if:
    /// <list type="bullet">
    /// <item><paramref name="groupId"/> is <c>null</c> or an empty string, or</item>
    /// <item><paramref name="apiToken"/> is <c>null</c> or an empty string.</item>
    /// </list>
    /// </exception>
    [JsonConstructor]
    public TelegramConfig(string groupId, string apiToken)
    {
        if (string.IsNullOrEmpty(groupId))
            throw new InvalidArgumentException($"'{nameof(groupId)}' must not be null or an empty string.", parameterName: nameof(groupId));

        if (string.IsNullOrEmpty(apiToken))
            throw new InvalidArgumentException($"'{nameof(apiToken)}' must not be null or an empty string.", parameterName: nameof(apiToken));

        this.GroupId = groupId;
        this.ApiToken = apiToken;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`]",
            nameof(this.GroupId), this.GroupId
        );
    }
}