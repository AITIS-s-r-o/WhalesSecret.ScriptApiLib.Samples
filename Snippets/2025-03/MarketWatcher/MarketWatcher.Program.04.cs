using System.Diagnostics;
using System.Web;

// Telegram API token to authorize message sending.
string apiToken = "XXXXXXXXXX:XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";

// Telegram group where to send a message to.
string groupId = "@your_group";

// Messages can be formatted using HTML syntax (e.g. <b>bold</b>, <i>italics</i>, etc.).
await SendTelegramMessageAsync("First test message from <b>the robot</b>!");

async Task SendTelegramMessageAsync(string message, bool htmlSyntax = true)
{
    string chatId = HttpUtility.UrlEncode(groupId);
    message = HttpUtility.UrlEncode(message);

    using HttpClient client = new();
    string uri = htmlSyntax
        ? $"https://api.telegram.org/bot{apiToken}/sendMessage?chat_id={chatId}&parse_mode=html&text={message}"
        : $"https://api.telegram.org/bot{apiToken}/sendMessage?chat_id={chatId}&text={message}";

    using HttpRequestMessage request = new(HttpMethod.Get, uri);
    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);

    string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    Debug.Assert(response.IsSuccessStatusCode, content);
}