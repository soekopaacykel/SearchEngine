using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseRouting();

// Load balancer configuration
var backendServers = new[]
{
    "http://localhost:5154",
    "http://localhost:5155"
};

var random = new Random();

// Catch all requests and forward them to backend servers
app.MapFallback(async (HttpContext context, IHttpClientFactory httpClientFactory) =>
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Incoming request: {context.Request.Method} {context.Request.Path}");
    
    try
    {
        // Select a random backend server
        var selectedServer = backendServers[random.Next(backendServers.Length)];

        // Create HTTP client with timeout
        var httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        // Build the target URL
        var targetUrl = $"{selectedServer}{context.Request.Path}{context.Request.QueryString}";
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Forwarding to: {targetUrl}");

        // For simple GET requests (most common case)
        if (context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            var response = await httpClient.GetAsync(targetUrl);
            
            // Copy response status code
            context.Response.StatusCode = (int)response.StatusCode;
            
            // Copy content type
            if (response.Content.Headers.ContentType != null)
            {
                context.Response.ContentType = response.Content.Headers.ContentType.ToString();
            }
            
            // Copy response body
            var responseContent = await response.Content.ReadAsStringAsync();
            await context.Response.WriteAsync(responseContent);
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {context.Request.Method} {context.Request.Path} -> {selectedServer} (Status: {response.StatusCode})");
        }
        else
        {
            // For other HTTP methods, use the more complex approach
            var requestMessage = new HttpRequestMessage();
            requestMessage.Method = new HttpMethod(context.Request.Method);
            requestMessage.RequestUri = new Uri(targetUrl);

            // Copy headers (except Host)
            foreach (var header in context.Request.Headers)
            {
                if (!header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            // Copy body for POST/PUT requests
            if (context.Request.ContentLength.HasValue && context.Request.ContentLength.Value > 0)
            {
                var bodyContent = new byte[context.Request.ContentLength.Value];
                await context.Request.Body.ReadExactlyAsync(bodyContent, 0, bodyContent.Length);
                requestMessage.Content = new ByteArrayContent(bodyContent);

                if (context.Request.ContentType != null)
                {
                    requestMessage.Content.Headers.ContentType =
                        System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
                }
            }

            var response = await httpClient.SendAsync(requestMessage);
            
            // Copy response
            context.Response.StatusCode = (int)response.StatusCode;
            
            foreach (var header in response.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            foreach (var header in response.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            var responseContent = await response.Content.ReadAsByteArrayAsync();
            await context.Response.Body.WriteAsync(responseContent, 0, responseContent.Length);
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {context.Request.Method} {context.Request.Path} -> {selectedServer} (Status: {response.StatusCode})");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Error forwarding request: {ex.Message}");
        context.Response.StatusCode = 502; // Bad Gateway
        await context.Response.WriteAsync("Load Balancer Error: Unable to reach backend servers");
    }
});

Console.WriteLine("Load Balancer starting...");
Console.WriteLine("Backend servers:");
foreach (var server in backendServers)
{
    Console.WriteLine($"  - {server}");
}
Console.WriteLine("Load Balancer running on http://localhost:5000");
Console.WriteLine("Press Ctrl+C to stop the load balancer");

app.Run("http://localhost:5000");