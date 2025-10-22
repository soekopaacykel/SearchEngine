using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

// LoadBalancerController
// ----------------------
// This controller acts as a very small HTTP reverse proxy ("load balancer").
// It accepts ANY route and forwards the request to one of the configured backend servers.
// Strategy: pick a random backend for each incoming request, copy headers/body through,
// send it to the backend, and stream the backend response back to the caller.
// Notes:
// - We skip forwarding certain hop-by-hop headers per RFC 7230 (e.g., Connection, TE, etc.).
// - We stream bodies to avoid buffering large payloads in memory.
// - This is intentionally simple for clarity.
namespace Loadbalencer.Controllers
{
    [ApiController]
    // Catch-all route: matches any path and forwards it (e.g., /api/search?q=abc)
    [Route("{**catchAll}")]
    public class LoadBalancerController : ControllerBase
    {
        // Backends to forward to. You can add more URLs here.
        private static readonly string[] BackendServers =
            (Environment.GetEnvironmentVariable("BACKENDS") ?? "http://localhost:5154,http://localhost:5155")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Factory for creating HttpClient instances (recommended over new HttpClient())
        private readonly IHttpClientFactory _httpClientFactory;
        // Random used to select a backend per request (simple load balancing)
        private static readonly Random _random = new();

        public LoadBalancerController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // Support all common HTTP verbs so every request type is proxied.
        [HttpGet, HttpPost, HttpPut, HttpDelete, HttpPatch, HttpHead, HttpOptions]
        public async Task Proxy()
        {
            // Log the incoming request for visibility
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Incoming: {Request.Method} {Request.Path}{Request.QueryString}");
            try
            {
                // 1) Choose a backend at random
                var selected = BackendServers[_random.Next(BackendServers.Length)];

                // 2) Create an HttpClient and set a reasonable timeout
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                // 3) Build the target URL by combining backend base with incoming path and query
                var targetUrl = $"{selected}{Request.Path}{Request.QueryString}";
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] -> Forwarding to: {targetUrl}");

                // 4) Create an HttpRequestMessage mirroring the incoming method and URL
                using var forwardRequest = new HttpRequestMessage(new HttpMethod(Request.Method), targetUrl);

                // 5) Copy request headers and body to the outgoing request
                CopyRequestHeaders(HttpContext, forwardRequest);
                CopyRequestBody(HttpContext, forwardRequest);

                // 6) Send to backend, reading headers first and streaming the body
                using var backendResponse = await client.SendAsync(forwardRequest, HttpCompletionOption.ResponseHeadersRead);

                // 7) Copy status, headers, and stream the response back to the original client
                await CopyResponseAsync(HttpContext, backendResponse);

                // Log where the request went and what status came back
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {Request.Method} {Request.Path} -> {selected} (Status: {backendResponse.StatusCode})");
            }
            catch (Exception ex)
            {
                // If anything fails, return 502 (Bad Gateway) so the caller knows it's a proxy error
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Error: {ex.Message}");
                Response.StatusCode = StatusCodes.Status502BadGateway;
                await Response.WriteAsync("Load Balancer Error: Unable to reach backend servers");
            }
        }

        // Copy incoming request headers into the proxied request, excluding hop-by-hop headers
        private static void CopyRequestHeaders(HttpContext context, HttpRequestMessage forwardRequest)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // RFC 7230 hop-by-hop headers that should not be forwarded by proxies
                "Host", "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization",
                "TE", "Trailer", "Transfer-Encoding", "Upgrade"
            };

            foreach (var (key, value) in context.Request.Headers)
            {
                if (excluded.Contains(key)) continue;

                // Try to add as request headers; if they belong to content headers, add to Content instead
                if (!forwardRequest.Headers.TryAddWithoutValidation(key, (IEnumerable<string>)value))
                {
                    if (forwardRequest.Content == null)
                        forwardRequest.Content = new ByteArrayContent(Array.Empty<byte>());
                    forwardRequest.Content.Headers.TryAddWithoutValidation(key, (IEnumerable<string>)value);
                }
            }
        }

        // Copy request body stream if there is a body (e.g., POST/PUT/PATCH)
        private static void CopyRequestBody(HttpContext context, HttpRequestMessage forwardRequest)
        {
            if (context.Request.ContentLength is > 0)
            {
                // Stream the incoming request body directly to the backend
                forwardRequest.Content = new StreamContent(context.Request.Body);

                // Preserve Content-Type if present so the backend can parse the body correctly
                if (!string.IsNullOrEmpty(context.Request.ContentType))
                {
                    forwardRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(context.Request.ContentType);
                }
            }
        }

        // Copy the backend response (status + headers) and stream the body back to the client
        private static async Task CopyResponseAsync(HttpContext context, HttpResponseMessage backendResponse)
        {
            // Mirror the backend status code
            context.Response.StatusCode = (int)backendResponse.StatusCode;

            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // RFC 7230 hop-by-hop headers not to forward
                "Transfer-Encoding", "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization",
                "TE", "Trailer", "Upgrade"
            };

            // Local function to copy headers while skipping excluded ones
            void CopyHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
            {
                foreach (var header in headers)
                {
                    if (!excluded.Contains(header.Key))
                    {
                        context.Response.Headers[header.Key] = header.Value.ToArray();
                    }
                }
            }

            // Copy both response headers and content headers
            CopyHeaders(backendResponse.Headers);
            CopyHeaders(backendResponse.Content.Headers);

            // Stream the backend response body directly to the client
            await using var responseStream = await backendResponse.Content.ReadAsStreamAsync();
            await responseStream.CopyToAsync(context.Response.Body);
        }
    }
}
