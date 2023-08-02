using System.Net;

using Azure;
using Azure.AI.OpenAI;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

using SocialAIAssistant.ApiApp.Configurations;

namespace SocialAIAssistant.ApiApp.Triggers;

/// <summary>
/// This represents the HTTP trigger entity for ChatGPT completion.
/// </summary>
public class CompletionHttpTrigger
{
    private readonly OpenAIApiSettings _openAISettings;
    private readonly PromptSettings _promptSettings;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompletionHttpTrigger"/> class.
    /// </summary>
    /// <param name="openAISettings"><see cref="OpenAIApiSettings"/> instance.</param>
    /// <param name="promptSettings"><see cref="PromptSettings"/> instance.</param>
    /// <param name="loggerFactory"><see cref="ILoggerFactory"/> instance.</param>
    public CompletionHttpTrigger(OpenAIApiSettings openAISettings, PromptSettings promptSettings, ILoggerFactory loggerFactory)
    {
        this._openAISettings = openAISettings.ThrowIfNullOrDefault();
        this._promptSettings = promptSettings.ThrowIfNullOrDefault();
        this._logger = loggerFactory.ThrowIfNullOrDefault().CreateLogger<CompletionHttpTrigger>();
    }

    /// <summary>
    /// Invokes the HTTP trigger that completes the prompt.
    /// </summary>
    /// <param name="req"><see cref="HttpRequestData"/> instance.</param>
    /// <returns><see cref="HttpResponseData"/> instance.</returns>
    [Function(nameof(CompletionHttpTrigger.GetCompletionsAsync))]
    [OpenApiOperation(operationId: "getCompletions", tags: new[] { "completions" }, Summary = "Gets the completion from the OpenAI API", Description = "This gets the completion from the OpenAI API.", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiSecurity(schemeName: "function_key", schemeType: SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header)]
    [OpenApiRequestBody(contentType: "text/plain", bodyType: typeof(string), Required = true, Description = "The prompt to generate the completion.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Summary = "The completion generated from the OpenAI API.", Description = "This returns the completion generated from the OpenAI API.")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Summary = "Invalid request.", Description = "This indicates the request is invalid.")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Summary = "Internal server error.", Description = "This indicates the server is not working as expected.")]
    public async Task<HttpResponseData> GetCompletionsAsync([HttpTrigger(AuthorizationLevel.Function, "POST", Route = "completions")] HttpRequestData req)
    {
        this._logger.LogInformation("C# HTTP trigger function processed a request.");

        var response = default(HttpResponseData);
        var prompt = req.ReadAsString();
        if (prompt.IsNullOrWhiteSpace())
        {
            this._logger.LogError("No prompt");

            response = req.CreateResponse(HttpStatusCode.BadRequest);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString("The prompt is required.");

            return response;
        }

        var endpoint = new Uri(string.Format(this._openAISettings.Endpoint, this._openAISettings.Instance));
        var credential = new AzureKeyCredential(this._openAISettings.AuthKey);
        var client = new OpenAIClient(endpoint, credential);

        var chatCompletionsOptions = new ChatCompletionsOptions()
        {
            Messages =
                {
                    new ChatMessage(ChatRole.System, this._promptSettings.System),
                    new ChatMessage(ChatRole.User, this._promptSettings.Users[0]),
                    new ChatMessage(ChatRole.System, this._promptSettings.Assistants[0]),
                    new ChatMessage(ChatRole.User, this._promptSettings.Users[1]),
                    new ChatMessage(ChatRole.System, this._promptSettings.Assistants[1]),
                    new ChatMessage(ChatRole.User, this._promptSettings.Users[2]),
                    new ChatMessage(ChatRole.System, this._promptSettings.Assistants[2]),
                    new ChatMessage(ChatRole.User, prompt)
                },
            MaxTokens = 3000,
            Temperature = 0.7f,
        };

        var deploymentId = this._openAISettings.DeploymentId;

        try
        {
            var result = await client.GetChatCompletionsAsync(deploymentId, chatCompletionsOptions);
            var message = result.Value.Choices[0].Message.Content;

            this._logger.LogInformation(message);

            response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString(message);

            return response;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, ex.Message);

            response = req.CreateResponse(HttpStatusCode.InternalServerError);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString("Internal server error.");

            return response;
        }
    }
}
