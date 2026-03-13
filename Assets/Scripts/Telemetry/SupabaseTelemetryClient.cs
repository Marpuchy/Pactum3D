using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

public sealed class SupabaseTelemetryClient
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _apiKey;

    public SupabaseTelemetryClient(string projectUrl, string apiKey)
    {
        _httpClient = new HttpClient();
        _endpoint = $"{projectUrl}/rest/v1/telemetry_events";
        _apiKey = apiKey;
    }

    public async Task SendAsync(TelemetryEvent telemetryEvent)
    {
        var rowJson = SerializeTelemetryEvent(telemetryEvent);
        var json = "[" + rowJson + "]";
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.Add("apikey", _apiKey);
        request.Headers.Add("Prefer", "return=minimal");
        
        if (LooksLikeJwt(_apiKey))
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");

        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            string errorBody = string.Empty;
            try
            {
                errorBody = await response.Content.ReadAsStringAsync();
            }
            catch
            {
                // Ignore read errors and rethrow with status only.
            }

            throw new HttpRequestException(
                $"Supabase REST error {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {errorBody}");
        }
    }

    private static string SerializeTelemetryEvent(TelemetryEvent telemetryEvent)
    {
        if (telemetryEvent == null)
        {
            throw new ArgumentNullException(nameof(telemetryEvent));
        }

        return "{"
               + "\"event_name\":" + SerializeString(telemetryEvent.EventName) + ","
               + "\"player_id\":" + SerializeString(telemetryEvent.PlayerId) + ","
               + "\"session_id\":" + SerializeString(telemetryEvent.SessionId) + ","
               + "\"timestamp_utc\":" + SerializeString(telemetryEvent.TimestampUtc.ToUniversalTime().ToString("O")) + ","
               + "\"payload_json\":" + SerializeRawJsonOrString(telemetryEvent.PayloadJson)
               + "}";
    }

    private static bool LooksLikeJwt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        int firstDot = value.IndexOf('.');
        if (firstDot <= 0)
            return false;

        int secondDot = value.IndexOf('.', firstDot + 1);
        return secondDot > firstDot + 1 && secondDot < value.Length - 1;
    }

    private static string SerializeRawJsonOrString(string value)
    {
        if (value == null)
            return "null";

        string trimmed = value.Trim();
        if (trimmed.Length >= 2)
        {
            bool isObject = trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}';
            bool isArray = trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']';
            if (isObject || isArray)
                return trimmed;
        }

        return SerializeString(value);
    }

    private static string SerializeString(string value)
    {
        if (value == null)
        {
            return "null";
        }

        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (char.IsControl(c))
                    {
                        builder.Append("\\u");
                        builder.Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(c);
                    }
                    break;
            }
        }

        builder.Append('"');
        return builder.ToString();
    }
}
