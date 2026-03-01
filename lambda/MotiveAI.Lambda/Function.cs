using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using System.Text.Json;
using Anthropic.SDK.Messaging;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MotiveAI.Lambda;

public class Function
{
    private readonly IAmazonSecretsManager _secretsManager;

    public Function()
    {
        _secretsManager = new AmazonSecretsManagerClient();
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(
        APIGatewayProxyRequest request,
        ILambdaContext context)
    {
        try
        {
            // Parse request body
            var body = JsonSerializer.Deserialize<AnalyzeRequest>(
                request.Body ?? "{}",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            context.Logger.LogInformation($"Raw body: {request.Body}");
            context.Logger.LogInformation($"Topic: {body?.Topic}");

            if (string.IsNullOrWhiteSpace(body?.Topic))
            {
                return BadRequest("Topic is required");
            }

            // Pull Claude API key from Secrets Manager — never hardcoded
            var secretArn = Environment.GetEnvironmentVariable("CLAUDE_SECRET_ARN");
            var secretValue = await _secretsManager.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = secretArn
            });

            // Call Claude
            var client = new AnthropicClient(secretValue.SecretString);
            var response = await client.Messages.GetClaudeMessageAsync(new MessageParameters
            {
                Model = AnthropicModels.Claude4Sonnet,
                MaxTokens = 1024,
                Messages = new List<Message>
                {
                    new Message
                    {
                        Role = RoleType.User,
                        Content = new List<ContentBase>
                        {
                            new TextContent { Text = BuildPrompt(body.Topic, body.Context) }
                        }
                    }
                }
            });

            var analysis = response.Content[0].ToString();

            return Ok(JsonSerializer.Serialize(new { analysis }));
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error: {ex.Message}");
            return ServerError(ex.Message);
        }
    }

    private static string BuildPrompt(string topic, string? additionalContext)
    {
        return $"""
            You are an incentive analyst. Your job is to look past official narratives 
            and ask: given what this actor stood to gain or avoid, what are the most 
            plausible motivations behind this action?

            Topic: {topic}
            {(string.IsNullOrWhiteSpace(additionalContext) ? "" : $"Additional context: {additionalContext}")}

            Analyze:
            1. Who are the key actors?
            2. What pressures were they under at the time?
            3. What did they stand to gain or avoid?
            4. What does the timing suggest?
            5. What is the most likely real motivation?

            Be direct. Be skeptical of official narratives. Follow the incentives.
            """;
    }

    private static Dictionary<string, string> CorsHeaders => new()
    {
        ["Content-Type"] = "application/json",
        ["Access-Control-Allow-Origin"] = "*",
        ["Access-Control-Allow-Headers"] = "Authorization,Content-Type"
    };

    private static APIGatewayProxyResponse Ok(string body) => new()
    {
        StatusCode = 200,
        Headers = CorsHeaders,
        Body = body
    };

    private static APIGatewayProxyResponse BadRequest(string message) => new()
    {
        StatusCode = 400,
        Headers = CorsHeaders,
        Body = JsonSerializer.Serialize(new { error = message })
    };

    private static APIGatewayProxyResponse ServerError(string message) => new()
    {
        StatusCode = 500,
        Headers = CorsHeaders,
        Body = JsonSerializer.Serialize(new { error = message })
    };
}

public record AnalyzeRequest(string Topic, string? Context);