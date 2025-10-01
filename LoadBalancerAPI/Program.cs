using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Load balancer configuration
var searchApiInstances = new List<SearchApiInstance>
{
    new("http://localhost:5154", true),
    new("http://localhost:5155", true),
};

var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
var loadBalancer = new RoundRobinLoadBalancer(searchApiInstances, httpClientFactory);

// Health check endpoint
app.MapGet(
    "/health",
    async () =>
    {
        var healthStatus = await loadBalancer.GetHealthStatusAsync();
        return Results.Ok(healthStatus);
    }
);

// Load balanced search endpoint
app.MapGet(
    "/api/search/{query}/{maxAmount}",
    async (string query, int maxAmount) =>
    {
        try
        {
            var result = await loadBalancer.ForwardSearchRequestAsync(
                $"/api/search/{query}/{maxAmount}"
            );
            return Results.Json(result);
        }
        catch (Exception ex)
        {
            return Results.Problem($"All search API instances are unavailable: {ex.Message}");
        }
    }
);

// Load balanced ping endpoint
app.MapGet(
    "/api/ping",
    async () =>
    {
        try
        {
            var result = await loadBalancer.ForwardPingRequestAsync("/api/ping");
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            return Results.Problem($"All search API instances are unavailable: {ex.Message}");
        }
    }
);

// Start background health checker
var healthChecker = new HealthChecker(loadBalancer);
healthChecker.StartHealthChecking();

app.Run();

// Load balancer classes
public class SearchApiInstance
{
    public string BaseUrl { get; }
    public bool IsHealthy { get; set; }
    public DateTime LastHealthCheck { get; set; }

    public SearchApiInstance(string baseUrl, bool isHealthy = true)
    {
        BaseUrl = baseUrl;
        IsHealthy = isHealthy;
        LastHealthCheck = DateTime.UtcNow;
    }
}

public class RoundRobinLoadBalancer
{
    private readonly List<SearchApiInstance> _instances;
    private readonly IHttpClientFactory _httpClientFactory;
    private int _currentIndex = 0;
    private readonly object _lock = new object();

    public RoundRobinLoadBalancer(List<SearchApiInstance> instances, IHttpClientFactory httpClientFactory)
    {
        _instances = instances;
        _httpClientFactory = httpClientFactory;
    }

    public SearchApiInstance GetNextHealthyInstance()
    {
        lock (_lock)
        {
            var healthyInstances = _instances.Where(i => i.IsHealthy).ToList();
            
            if (!healthyInstances.Any())
            {
                throw new InvalidOperationException("No healthy instances available");
            }

            var instance = healthyInstances[_currentIndex % healthyInstances.Count];
            _currentIndex = (_currentIndex + 1) % healthyInstances.Count;
            
            return instance;
        }
    }

    public async Task<object> ForwardSearchRequestAsync(string path)
    {
        var instance = GetNextHealthyInstance();
        var httpClient = _httpClientFactory.CreateClient();
        
        try
        {
            var response = await httpClient.GetAsync($"{instance.BaseUrl}{path}");
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            
            // Deserialize the JSON string to an object so it's returned as proper JSON
            var searchResult = JsonSerializer.Deserialize<object>(jsonString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            return searchResult;
        }
        catch (Exception)
        {
            // Mark instance as unhealthy and try another one
            instance.IsHealthy = false;
            
            if (_instances.Any(i => i.IsHealthy))
            {
                return await ForwardSearchRequestAsync(path);
            }
            throw;
        }
    }

    public async Task<string> ForwardPingRequestAsync(string path)
    {
        var instance = GetNextHealthyInstance();
        var httpClient = _httpClientFactory.CreateClient();
        
        try
        {
            var response = await httpClient.GetAsync($"{instance.BaseUrl}{path}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return $"Load Balancer -> {content} ({instance.BaseUrl})";
        }
        catch (Exception)
        {
            // Mark instance as unhealthy and try another one
            instance.IsHealthy = false;
            
            if (_instances.Any(i => i.IsHealthy))
            {
                return await ForwardPingRequestAsync(path);
            }
            throw;
        }
    }

    public async Task CheckHealthAsync()
    {
        var httpClient = _httpClientFactory.CreateClient();
        
        foreach (var instance in _instances)
        {
            try
            {
                var response = await httpClient.GetAsync($"{instance.BaseUrl}/api/ping");
                instance.IsHealthy = response.IsSuccessStatusCode;
                instance.LastHealthCheck = DateTime.UtcNow;
            }
            catch
            {
                instance.IsHealthy = false;
                instance.LastHealthCheck = DateTime.UtcNow;
            }
        }
    }

    public async Task<object> GetHealthStatusAsync()
    {
        await CheckHealthAsync();
        
        return new
        {
            TotalInstances = _instances.Count,
            HealthyInstances = _instances.Count(i => i.IsHealthy),
            Instances = _instances.Select(i => new
            {
                BaseUrl = i.BaseUrl,
                IsHealthy = i.IsHealthy,
                LastHealthCheck = i.LastHealthCheck
            }).ToList()
        };
    }

    public List<SearchApiInstance> GetInstances() => _instances;
}

public class HealthChecker
{
    private readonly RoundRobinLoadBalancer _loadBalancer;
    private readonly Timer _timer;

    public HealthChecker(RoundRobinLoadBalancer loadBalancer)
    {
        _loadBalancer = loadBalancer;
        _timer = new Timer(CheckHealth, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void StartHealthChecking()
    {
        // Check health every 30 seconds
        _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    private async void CheckHealth(object? state)
    {
        try
        {
            await _loadBalancer.CheckHealthAsync();
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Health check completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Health check failed: {ex.Message}");
        }
    }
}
