using System.Net.Http;
using System.Threading.Tasks;
using Core;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SearchWeb.Services
{
    public class SearchService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SearchService> _logger;
        private readonly IConfiguration _configuration;
        private string _baseUrl;
        private bool _apiIsAvailable = true;
        private string _loadBalancerUrl;
        private bool _loadBalancerIsAvailable = true;
        private LoadBalancerHealthStatus? _lastLoadBalancerStatus;
        private string _lastUsedEndpoint = string.Empty;

        public SearchService(HttpClient httpClient, IConfiguration configuration, ILogger<SearchService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _baseUrl = _configuration["SearchApi:BaseUrl"] ?? "http://localhost:5000";
            _loadBalancerUrl = _configuration["LoadBalancer:BaseUrl"] ?? "http://localhost:5156";

            // Set a reasonable timeout to avoid long waits for unavailable services
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }

        public bool ApiIsAvailable => _apiIsAvailable;
        public bool LoadBalancerIsAvailable => _loadBalancerIsAvailable;
        public LoadBalancerHealthStatus? LastLoadBalancerStatus => _lastLoadBalancerStatus;
        public string LastUsedEndpoint => _lastUsedEndpoint;

        public void UpdateApiUrl(string url)
        {
            _baseUrl = url;
            _logger.LogInformation($"API URL updated to: {url}");
        }

        public async Task<bool> CheckApiHealthAsync()
        {
            try
            {
                _logger.LogInformation($"Checking API health at: {_baseUrl}/api/ping");
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/ping");
                _apiIsAvailable = response.IsSuccessStatusCode;

                if (_apiIsAvailable)
                {
                    _logger.LogInformation("API is available.");
                }
                else
                {
                    _logger.LogWarning($"API returned non-success status code: {response.StatusCode}");
                }

                return _apiIsAvailable;
            }
            catch (Exception ex)
            {
                _apiIsAvailable = false;
                _logger.LogError(ex, $"API health check failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CheckLoadBalancerHealthAsync()
        {
            try
            {
                _logger.LogInformation($"Checking Load Balancer health at: {_loadBalancerUrl}/health");
                var response = await _httpClient.GetAsync($"{_loadBalancerUrl}/health");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _lastLoadBalancerStatus = JsonSerializer.Deserialize<LoadBalancerHealthStatus>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    _loadBalancerIsAvailable = true;
                    _logger.LogInformation("Load Balancer is available.");
                    return true;
                }
                else
                {
                    _loadBalancerIsAvailable = false;
                    _logger.LogWarning($"Load Balancer returned non-success status code: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _loadBalancerIsAvailable = false;
                _lastLoadBalancerStatus = null;
                _logger.LogError(ex, $"Load Balancer health check failed: {ex.Message}");
                return false;
            }
        }

        public async Task<SearchResult> SearchAsync(string query, int maxAmount = 10)
        {
            // Try load balancer first if available, then fall back to direct API
            string searchUrl;
            if (_loadBalancerIsAvailable)
            {
                searchUrl = _loadBalancerUrl;
                _lastUsedEndpoint = "Load Balancer";
                _logger.LogInformation("Using Load Balancer for search");
            }
            else if (_apiIsAvailable)
            {
                searchUrl = _baseUrl;
                _lastUsedEndpoint = "Direct API";
                _logger.LogInformation("Using direct API for search (Load Balancer unavailable)");
            }
            else
            {
                _lastUsedEndpoint = "None (All unavailable)";
                return CreateErrorResult(query, "Both Load Balancer and Search API are currently unavailable. Please try again later.");
            }

            // Replace spaces with commas as the API expects comma-separated terms
            var formattedQuery = query.Replace(" ", ",");

            try
            {
                _logger.LogInformation($"Searching for '{query}' (formatted as '{formattedQuery}') with max results: {maxAmount}");
                var response = await _httpClient.GetAsync($"{searchUrl}/api/search/{formattedQuery}/{maxAmount}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<SearchResult>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    // If using load balancer, try to get additional info about which instance was used
                    if (searchUrl == _loadBalancerUrl && response.Headers.Contains("X-API-Instance"))
                    {
                        var instanceInfo = response.Headers.GetValues("X-API-Instance").FirstOrDefault();
                        _lastUsedEndpoint = $"Load Balancer → {instanceInfo}";
                    }

                    _logger.LogInformation($"Search completed successfully. Found {result?.Hits ?? 0} hits.");
                    return result ?? CreateEmptyResult(query);
                }
                else
                {
                    _logger.LogWarning($"Search API returned non-success status code: {response.StatusCode}");
                    
                    // Mark the service as unavailable and try the other one if we were using load balancer
                    if (searchUrl == _loadBalancerUrl)
                    {
                        _loadBalancerIsAvailable = false;
                        if (_apiIsAvailable)
                        {
                            return await SearchAsync(query, maxAmount); // Retry with direct API
                        }
                    }
                    else
                    {
                        _apiIsAvailable = false;
                    }
                    
                    return CreateErrorResult(query, $"Search returned error: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Failed to connect to search service: {ex.Message}");
                
                // Mark the service as unavailable and try the other one if we were using load balancer
                if (searchUrl == _loadBalancerUrl)
                {
                    _loadBalancerIsAvailable = false;
                    if (_apiIsAvailable)
                    {
                        return await SearchAsync(query, maxAmount); // Retry with direct API
                    }
                }
                else
                {
                    _apiIsAvailable = false;
                }
                
                return CreateErrorResult(query, $"Could not connect to search service. Please verify the services are running and configured correctly.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error during search: {ex.Message}");
                return CreateErrorResult(query, $"An unexpected error occurred: {ex.Message}");
            }
        }

        private SearchResult CreateEmptyResult(string query)
        {
            return new SearchResult
            {
                Query = new[] { query },
                Hits = 0,
                DocumentHits = new List<DocumentHit>(),
                Ignored = new List<string>(),
                TimeUsed = TimeSpan.Zero
            };
        }

        private SearchResult CreateErrorResult(string query, string errorMessage)
        {
            var result = CreateEmptyResult(query);
            result.Ignored = new List<string> { errorMessage };
            return result;
        }
    }

    public class LoadBalancerHealthStatus
    {
        public int TotalInstances { get; set; }
        public int HealthyInstances { get; set; }
        public List<ApiInstanceStatus> Instances { get; set; } = new();
    }

    public class ApiInstanceStatus
    {
        public string BaseUrl { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public DateTime LastHealthCheck { get; set; }
    }
}