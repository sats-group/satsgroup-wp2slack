using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace SatsGroup.Devs.WorkplaceToSlackPoster;

public class HttpCallbackFunction
{
    private readonly HttpClient _httpClient;

    public HttpCallbackFunction(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }
    
    [Function(nameof(WorkplaceCallback))]
    public async Task<HttpResponseData> WorkplaceCallback(
        [HttpTrigger(AuthorizationLevel.Anonymous,"get", "post", Route = null)]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken cancellationToken)
    {
        var log = executionContext.GetLogger(nameof(WorkplaceCallback));

        var query = 
            Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(req.Url.Query);
        
        if (IsVerificationRequest(query))
        {
            if (query.ContainsKey("hub.verify_token")
                && query["hub.verify_token"] == Environment.GetEnvironmentVariable("VerificationToken"))
                return Ok(req, query["hub.challenge"]);
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var json = await req.ReadAsStringAsync();
        log.LogInformation("Webhook called. Payload={Payload}", json);

        if (!IsValidRequest(req, json, log))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        if (json == null)
            return req.CreateResponse(HttpStatusCode.BadRequest);
        
        var callbackPayLoad = JsonConvert.DeserializeObject<WorkplaceGroupPosts>(json);

        foreach (var post in callbackPayLoad.Entry)
        {
            var groupName = await GetName(post.Id, cancellationToken);
            var change = post.Changes.First();
            var payload = new
            {
                text = $"New post in workplace group '{groupName}'",
                blocks = new object[]
                {
                    new
                    {
                        type = "section",
                        text = new
                        {
                            type = "mrkdwn",
                            text = NewOrEditText(post, groupName) +
                                   $"\n*<{change.Value.Permalink_url}|Go to post>*",
                        },
                    },
                    new
                    {
                        type = "section",
                        text = new
                        {
                            type = "mrkdwn",
                            text = post.Changes.First().Value.Message,
                        },
                    },
                    new
                    {
                        type = "section",
                        fields = new[]
                        {
                            new
                            {
                                type = "mrkdwn",
                                text =
                                    $"*Posted by:* {await GetName(change.Value.From.Id, cancellationToken)}"
                            }
                        },
                    },
                }
            };
            var jsonPayload = JsonConvert.SerializeObject(payload);

            var slackWebhookUri = Environment.GetEnvironmentVariable("SlackWebhookUri");
            if (string.IsNullOrEmpty(slackWebhookUri))
            {
                throw new InvalidOperationException("SlackWebhookUri env variable missing - unable to post to slack");
            }
            var response = await _httpClient.PostAsync(
                new Uri(slackWebhookUri),
                new StringContent(jsonPayload, Encoding.UTF8, "application/json"), cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        return req.CreateResponse(HttpStatusCode.OK);
    }

    private bool IsValidRequest(HttpRequestData req, string payload, ILogger log)
    {
        var secret = Environment.GetEnvironmentVariable("AppSecret");

        if (string.IsNullOrEmpty(secret))
            return true;

        const string signatureHeader = "X-Hub-Signature-256";

        const string prefix = "sha256=";

        if (!req.Headers.Contains(signatureHeader))
        {
            log.LogWarning("Signature header missing");
            return false;
        }

        var shaHeader = req.Headers.GetValues(signatureHeader).First();
        log.LogInformation("SignatureHeader={SignatureHeader}", shaHeader);
        var signature = shaHeader[prefix.Length..];

        using var hasher = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var result = hasher.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return ToHexString(result) == signature;
    }

    private static string ToHexString(IReadOnlyCollection<byte> bytes)
    {
        var builder = new StringBuilder(bytes.Count * 2);
        foreach (var b in bytes)
        {
            builder.Append($"{b:x2}");
        }

        return builder.ToString();
    }

    private string NewOrEditText(Entry post, string groupName)
    {
        if (post.Changes.First().Value.Verb == "add")
            return $"There is a new post in *{groupName}*!";
        return $"A post in *{groupName}* was edited";
    }

    private async Task<string> GetName(string id, CancellationToken cancellationToken)
    {
        var response =
            await _httpClient.GetAsync(
                new Uri(
                    $"https://graph.facebook.com/{id}?fields=name&access_token={Environment.GetEnvironmentVariable("AccessToken")}"), cancellationToken);
        return JsonConvert.DeserializeObject<IdNameResponse>(await response.Content.ReadAsStringAsync(cancellationToken)).Name;
    }

    private record IdNameResponse(string Id, string Name);

    private static HttpResponseData Ok(HttpRequestData req, string challenge)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteString(challenge);
        return response;
    }

    private static bool IsVerificationRequest(Dictionary<string, StringValues> req)
        => req.ContainsKey("hub.mode")
               && req["hub.mode"] == "subscribe";
}

public record WorkplaceGroupPosts(
    Entry[] Entry,
    string Object
);

public record Entry(
    string Id,
    int Time,
    Changes[] Changes
);

public record Changes(
    Value Value,
    string Field
);

public record Value(
    string Created_time,
    Community Community,
    From From,
    string Message,
    string Permalink_url,
    string Post_id,
    string Target_type,
    string Type,
    string Verb
);

public record Community(
    string Id
);

public record From(
    string Id,
    string Name
);

