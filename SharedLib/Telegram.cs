using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace WhalesSecret.ScriptApiLib.Samples.SharedLib;

/// <summary>
/// Telegram API for samples that want to send notifications to Telegram.
/// </summary>
public class Telegram : IAsyncDisposable
{
    /// <summary>Maximum length of a Telegram message</summary>
    private const int MaxMessageLength = 4096;

    /// <summary>URL-encoded Telegram group ID to which to send messages.</summary>
    private readonly string groupId;

    /// <summary>Telegram API token.</summary>
    private readonly string apiToken;

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
    /// <param name="groupId">Telegram group ID to which to send messages.</param>
    /// <param name="apiToken">Telegram API token.</param>
    /// <param name="httpClient">HTTP client to use to send messages to Telegram, or <c>null</c> to create a new instance.</param>
    public Telegram(string groupId, string apiToken, HttpClient? httpClient = null)
    {
        this.disposedValueLock = new();
        this.groupId = HttpUtility.UrlEncode(groupId);
        this.apiToken = apiToken;

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
    /// <param name="message">Message to send. Note that the message is expected to be a HTML-encoded message.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>If the function succeeds, the return value is <c>null</c>. Otherwise, the return value is an error message.</returns>
    public async Task<string?> SendMessageAsync(string message, CancellationToken cancellationToken)
    {
        string[] parts = SplitStringByMaxLength(message, MaxMessageLength);

        string? error = null;
        foreach (string part in parts)
        {
            string encodedMessagePart = HttpUtility.UrlEncode(message);

            string uri = $"https://api.telegram.org/bot{this.apiToken}/sendMessage?chat_id={this.groupId}&parse_mode=html&text={encodedMessagePart}";

            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, uri);
                using HttpResponseMessage response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                string responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    error = $"Sending a HTTP request to Telegram failed with HTTP status code {response.StatusCode}. Response content:{Environment.NewLine}{responseContent}";
            }
            catch (HttpRequestException e)
            {
                error = $"Sending a HTTP request to Telegram failed with HTTP request error {e.HttpRequestError}: {e.Message}";
            }
            catch (Exception e)
            {
                error = e.Message;
            }

            if (error is not null)
                break;
        }

        return error;
    }

    /// <summary>
    /// Split string into chunks of a maximum length.
    /// </summary>
    /// <param name="input">Input string to split.</param>
    /// <param name="maxLength">Maximum length of each output string.</param>
    /// <returns>List of strings that together form the input string and are all <paramref name="maxLength"/> long except for the last one, which can shorter.</returns>
    private static string[] SplitStringByMaxLength(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input))
            return Array.Empty<string>();

        // Calculate the number of chunks needed.
        int chunkCount = (int)Math.Ceiling((double)input.Length / maxLength);
        string[] result = new string[chunkCount];

        for (int i = 0; i < chunkCount; i++)
        {
            int startIndex = i * maxLength;
            int length = Math.Min(maxLength, input.Length - startIndex);
            result[i] = input.Substring(startIndex, length);
        }

        return result;
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