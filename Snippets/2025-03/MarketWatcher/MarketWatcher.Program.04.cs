using System.Diagnostics;
using System.Web;

// Telegram API token to authorize message sending.
string apiToken = "XXXXXXXXXX:XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";

// Telegram group where to send a message to.
string groupId = "@your_group";

string message = HttpUtility.UrlEncode("RSI signals oversold for BTC/USDT on Binance!");

using (HttpClient client = new())
{
    string uri = $"https://api.telegram.org/bot{apiToken}/sendMessage?chat_id={groupId}&text={message}";
    using HttpRequestMessage request = new(HttpMethod.Get, uri);
    HttpResponseMessage response = client.Send(request).ConfigureAwait(false);

    string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    Debug.Assert(response.IsSuccessStatusCode, content);
}