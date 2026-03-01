using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using System.Text.Json;
using System.Net.Http;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MotiveAI.Events;

public class Function
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(25)
    };

    private static Dictionary<string, string> CorsHeaders => new()
    {
        ["Content-Type"] = "application/json",
        ["Access-Control-Allow-Origin"] = "*",
        ["Access-Control-Allow-Headers"] = "Authorization,Content-Type"
    };

    public async Task<APIGatewayProxyResponse> FunctionHandler(
        APIGatewayProxyRequest request,
        ILambdaContext context)
    {
        try
        {
            var url = "https://api.gdeltproject.org/api/v2/doc/doc" +
                      "?query=government+policy+military+economy" +
                      "&mode=artlist" +
                      "&maxrecords=15" +
                      "&format=json" +
                      "&timespan=2h" +
                      "&sourcelang=English" +
                      "&sourcecountry=US";

            //var url = "https://api.ipify.org?format=json";

            context.Logger.LogInformation("Calling GDELT...");
            var response = await _http.GetStringAsync(url);
            context.Logger.LogInformation($"GDELT raw: {response.Substring(0, Math.Min(200, response.Length))}");
            context.Logger.LogInformation($"GDELT responded, length: {response.Length}");

            var gdelt = JsonSerializer.Deserialize<GdeltResponse>(response,
                               new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var events = gdelt?.Articles?.Select(a => new
            {
                title = a.Title,
                url = a.Url,
                domain = a.Domain,
                seendate = a.Seendate
            }) ?? Enumerable.Empty<object>();

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Headers = CorsHeaders,
                Body = JsonSerializer.Serialize(new { events })
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error: {ex.Message}");
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Headers = CorsHeaders,
                Body = JsonSerializer.Serialize(new { error = ex.Message })
            };
        }
    }
}

public class GdeltResponse
{
    public List<GdeltArticle>? Articles { get; set; }
}

public class GdeltArticle
{
    public string? Title { get; set; }
    public string? Url { get; set; }
    public string? Domain { get; set; }
    public string? Seendate { get; set; }
}