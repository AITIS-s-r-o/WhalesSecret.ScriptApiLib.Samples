using System.Diagnostics;
using System.Web;

string chatId = "<YOUR_PUBLIC_TELEGRAM_CHANNEL_NAME>";
string apiToken = "XXXXXXXXXX:YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY";
string message = HttpUtility.UrlEncode("RSI signals oversold for BTC/USDT on Binance!");

using (HttpClient client = new())
{
    string uri = $"https://api.telegram.org/bot{apiToken}/sendMessage?chat_id={chatId}&text={message}";
    using HttpRequestMessage request = new(HttpMethod.Get, uri);
    HttpResponseMessage response = client.Send(request);

    string content = await response.Content.ReadAsStringAsync();
    Debug.Assert(response.IsSuccessStatusCode, content);
}