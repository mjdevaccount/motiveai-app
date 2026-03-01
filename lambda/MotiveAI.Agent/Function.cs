using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MotiveAI.Agent;

public class Function
{
    private static readonly HttpClient _http = CreateHttpClient();
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    // CIKs are 10-digit padded as required by the submissions endpoint
    private static readonly Dictionary<string, string> CikMap = new()
    {
        ["NVDA"]  = "0001045810",
        ["META"]  = "0001326801",
        ["GOOGL"] = "0001652044",
        ["MSFT"]  = "0000789019",
        ["AAPL"]  = "0000320193",
        ["UNH"]   = "0000731766",
        ["CI"]    = "0001739940"
    };

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MotiveAI research@motiveai.com");
        return client;
    }

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
            var cutoff = DateTime.UtcNow.AddDays(-60);

            context.Logger.LogInformation(
                $"Checking {CikMap.Count} symbols: {string.Join(", ", CikMap.Keys)}");
            context.Logger.LogInformation(
                $"Cutoff date: {cutoff:yyyy-MM-dd}");

            var tasks = CikMap.Keys
                .Select(symbol => FetchRecentFiling(symbol, cutoff, context));
            var results = await Task.WhenAll(tasks);

            var found    = results.Where(r => r.Filing != null).ToList();
            var notFound = results.Where(r => r.Filing == null).Select(r => r.Symbol).ToList();

            context.Logger.LogInformation(
                $"Summary: {found.Count}/{CikMap.Count} symbols had an earnings 8-K in the last 60 days");

            if (notFound.Count > 0)
                context.Logger.LogInformation(
                    $"No recent earnings 8-K for: {string.Join(", ", notFound)}");

            var summary = new
            {
                symbolsChecked = CikMap.Count,
                filingsFound   = found.Count,
                cutoffDate     = cutoff.ToString("yyyy-MM-dd"),
                filings        = found.Select(r => new
                {
                    symbol          = r.Symbol,
                    cik             = r.Filing!.Cik,
                    accessionNumber = r.Filing.AccessionNumber,
                    form            = r.Filing.Form,
                    filingDate      = r.Filing.FilingDate,
                    items           = r.Filing.Items
                }),
                symbolsWithNoRecentFiling = notFound
            };

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Headers = CorsHeaders,
                Body = JsonSerializer.Serialize(summary)
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Fatal error: {ex.Message}");
            return Error(500, ex.Message);
        }
    }

    private static async Task<SymbolResult> FetchRecentFiling(
        string symbol, DateTime cutoff, ILambdaContext context)
    {
        try
        {
            var cik = CikMap[symbol];
            context.Logger.LogInformation($"{symbol}: fetching submissions for CIK {cik}...");

            var filing = await FetchLatest8K(symbol, cik, cutoff, context);

            if (filing is null)
                context.Logger.LogInformation($"{symbol}: no earnings 8-K (item 2.02) in the last 60 days");
            else
                context.Logger.LogInformation(
                    $"{symbol}: found 8-K filed {filing.FilingDate}, items: {filing.Items} ({filing.AccessionNumber})");

            return new SymbolResult(symbol, filing);
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning($"{symbol}: error fetching filings — {ex.Message}");
            return new SymbolResult(symbol, null);
        }
    }

    private static async Task<EdgarFiling?> FetchLatest8K(
        string symbol, string cik, DateTime cutoff, ILambdaContext context)
    {
        var url = $"https://data.sec.gov/submissions/CIK{cik}.json";
        var json = await _http.GetStringAsync(url);

        var submissions = JsonSerializer.Deserialize<EdgarSubmissionsResponse>(json, _json);
        var recent = submissions?.Filings?.Recent;

        if (recent?.Form == null || recent.FilingDate == null || recent.AccessionNumber == null)
        {
            context.Logger.LogWarning($"{symbol}: submissions response missing expected arrays");
            return null;
        }

        // Arrays are parallel and ordered most-recent-first
        // Require item 2.02 (Results of Operations) to target earnings releases specifically
        for (var i = 0; i < recent.Form.Count; i++)
        {
            var items = recent.Items?[i] ?? "";
            if (recent.Form[i] == "8-K"
                && items.Contains("2.02")
                && DateTime.TryParse(recent.FilingDate[i], out var filingDate)
                && filingDate >= cutoff)
            {
                return new EdgarFiling
                {
                    Cik             = cik,
                    AccessionNumber = recent.AccessionNumber[i],
                    Form            = recent.Form[i],
                    FilingDate      = recent.FilingDate[i],
                    Items           = items
                };
            }
        }

        return null;
    }

    private static APIGatewayProxyResponse Error(int statusCode, string message) => new()
    {
        StatusCode = statusCode,
        Headers = CorsHeaders,
        Body = JsonSerializer.Serialize(new { error = message })
    };
}

public record SymbolResult(string Symbol, EdgarFiling? Filing);

// Submissions endpoint response — camelCase fields match with PropertyNameCaseInsensitive
public class EdgarSubmissionsResponse
{
    public string? Cik   { get; set; }
    public string? Name  { get; set; }
    public EdgarFilingsContainer? Filings { get; set; }
}

public class EdgarFilingsContainer
{
    public EdgarRecentFilings? Recent { get; set; }
}

public class EdgarRecentFilings
{
    public List<string>? AccessionNumber { get; set; }
    public List<string>? FilingDate      { get; set; }
    public List<string>? Form            { get; set; }
    public List<string>? Items           { get; set; }
}

public class EdgarFiling
{
    public string Cik             { get; set; } = "";
    public string AccessionNumber { get; set; } = "";
    public string Form            { get; set; } = "";
    public string FilingDate      { get; set; } = "";
    public string Items           { get; set; } = "";
}
