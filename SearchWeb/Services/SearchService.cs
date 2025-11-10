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
        private string _lastUsedEndpoint = string.Empty;

        public SearchService(HttpClient httpClient, IConfiguration configuration, ILogger<SearchService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _baseUrl = _configuration["SearchApi:BaseUrl"] ?? "http://localhost:5000";

            // Set a reasonable timeout to avoid long waits for unavailable services
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }

        public bool ApiIsAvailable => _apiIsAvailable;
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
                    
                    // Also check database shard health
                    await CheckDatabaseHealthAsync();
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

        private async Task CheckDatabaseHealthAsync()
        {
            try
            {
                _logger.LogInformation($"Checking database shard health at: {_baseUrl}/api/health");
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/health");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var healthData = JsonSerializer.Deserialize<JsonElement>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (healthData.TryGetProperty("IsHealthy", out var isHealthyProperty))
                    {
                        var isHealthy = isHealthyProperty.GetBoolean();
                        if (isHealthy)
                        {
                            _logger.LogInformation("All database shards are healthy.");
                        }
                        else
                        {
                            _logger.LogWarning("Some database shards are unhealthy.");
                        }
                    }
                }
                else
                {
                    _logger.LogWarning($"Database health check returned status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Database health check failed, but API is still available");
            }
        }

        public async Task<SearchResult> SearchAsync(string query, int maxAmount = 10)
        {
            if (!_apiIsAvailable)
            {
                _lastUsedEndpoint = "None (API unavailable)";
                return CreateErrorResult(query, "Search API is currently unavailable. Please try again later.");
            }

            _lastUsedEndpoint = "SearchAPI";
            _logger.LogInformation("Using SearchAPI for search");

            // Replace spaces with commas as the API expects comma-separated terms
            var formattedQuery = query.Replace(" ", ",");

            try
            {
                _logger.LogInformation($"Searching for '{query}' (formatted as '{formattedQuery}') with max results: {maxAmount}");
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/search/{formattedQuery}/{maxAmount}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<SearchResult>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    _logger.LogInformation($"Search completed successfully. Found {result?.Hits ?? 0} hits.");
                    return result ?? CreateEmptyResult(query);
                }
                else
                {
                    _logger.LogWarning($"Search API returned non-success status code: {response.StatusCode}");
                    _apiIsAvailable = false;
                    return CreateErrorResult(query, $"Search returned error: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Failed to connect to search service: {ex.Message}");
                _apiIsAvailable = false;
                return CreateErrorResult(query, "Could not connect to search service. Please verify the service is running and configured correctly.");
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
}