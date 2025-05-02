using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace WhalesSecret.ScriptApiLib.Samples.SharedLib;

/// <summary>
/// Telegram API for samples that want to send notifications to Telegram.
/// </summary>
/// <remarks>In order for those samples to work, you have to set a valid <see cref="ApiToken"/> below.</remarks>
public class Telegram : IAsyncDisposable
{
    /// <summary>URL-encoded Telegram group ID to which to send the messages.</summary>
    private readonly string groupId;

    /// <summary>HTTP client to use to send messages to Telegram, or <c>null</c> to create a new instance.</summary>
    private readonly HttpClient httpClient;

    /// <summary><c>true</c> if <see cref="httpClient"/> was created by this instance, <c>false</c> otherwise.</summary>
    private readonly bool disposeHttpClient;

    /// <summary>Lock object to be used when accessing <see cref="disposedValue"/>.</summary>
    private readonly Lock disposedValueLock;

    /// <summary>Set to <c>true</c> if the object was disposed already, <c>false</c> otherwise. Used by the dispose pattern.</summary>
    /// <remarks>All access has to be protected by <see cref="disposedValueLock"/>.</remarks>
    private bool disposedValue;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="groupId">Telegram group ID to which to send the messages.</param>
    /// <param name="httpClient">HTTP client to use to send messages to Telegram, or <c>null</c> to create a new instance.</param>
    public Telegram(string groupId, HttpClient? httpClient = null)
    {
        this.disposedValueLock = new();
        this.groupId = HttpUtility.UrlEncode(groupId);

        if (httpClient is null)
        {
            this.httpClient = new();
            this.disposeHttpClient = true;
        }
        else this.httpClient = httpClient;
    }

    /// <summary>
    /// Sends a message to the Telegram group.
    /// </summary>
    /// <param name="message">Message to send.</param>
    /// <param name="htmlSyntax"><c>true</c> if the message is HTML-encoded, <c>false</c> otherwise.</param>
    /// <returns>If the function succeeds, the return value is <c>null</c>. Otherwise, the return value is an error message.</returns>
    public async Task<string?> SendMessageAsync(string message, bool htmlSyntax = true)
    {
        string? error = null;
        message = HttpUtility.UrlEncode(message);

        string uri = htmlSyntax
            ? $"https://api.telegram.org/bot{Credentials.TelegramApiToken}/sendMessage?chat_id={this.groupId}&parse_mode=html&text={message}"
            : $"https://api.telegram.org/bot{Credentials.TelegramApiToken}/sendMessage?chat_id={this.groupId}&text={message}";

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, uri);
            HttpResponseMessage response = await this.httpClient.SendAsync(request).ConfigureAwait(false);
            _ = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                error = $"Sending a HTTP request to Telegram failed with HTTP status code {response.StatusCode}.";
        }
        catch (Exception e)
        {
            error = e.Message;
        }

        return error;
    }

    /// <summary>
    /// Frees managed resources used by the object.
    /// </summary>
    /// <returns>A <see cref="ValueTask">task</see> that represents the asynchronous dispose operation.</returns>
    protected virtual ValueTask DisposeCoreAsync()
    {
        lock (this.disposedValueLock)
        {
            if (this.disposedValue)
                return default;

            this.disposedValue = true;
        }

        if (this.disposeHttpClient)
            this.httpClient.Dispose();

        return default;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeCoreAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}