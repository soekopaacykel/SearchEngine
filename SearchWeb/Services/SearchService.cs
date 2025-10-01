using System.Net.Http;
using System.Threading.Tasks;
using Core;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SearchWeb.Services
{
    public class FileContentResponse
    {
        public string Content { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
    }

    public class SearchService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SearchService> _logger;
        private readonly IConfiguration _configuration;
        private string _baseUrl;
        private bool _apiIsAvailable = true;

        public SearchService(HttpClient httpClient, IConfiguration configuration, ILogger<SearchService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _baseUrl = _configuration["SearchApi:BaseUrl"] ?? "http://localhost:5000";

            // Set a reasonable timeout to avoid long waits for unavailable services
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public bool ApiIsAvailable => _apiIsAvailable;

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

        public async Task<SearchResult> SearchAsync(string query, int maxAmount = 10)
        {
            // Return empty result with error message if we already know the API is unavailable
            if (!_apiIsAvailable)
            {
                return CreateErrorResult(query, "Search API is currently unavailable. Please try again later.");
            }

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
                    return CreateErrorResult(query, $"Search API returned error: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Failed to connect to search API: {ex.Message}");
                _apiIsAvailable = false;
                return CreateErrorResult(query, $"Could not connect to search API. Please verify the API is running and configured correctly.");
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

        public async Task<FileContentResponse> GetFileContentAsync(string filePath)
        {
            if (!_apiIsAvailable)
            {
                _logger.LogWarning("Cannot retrieve file content: API is unavailable");
                return null;
            }

            try
            {
                // Encode the file path properly for a query parameter
                var encodedPath = Uri.EscapeDataString(filePath);
                var requestUrl = $"{_baseUrl}/api/file-content?filePath={encodedPath}";

                _logger.LogInformation($"Requesting file content from: {requestUrl}");
                var response = await _httpClient.GetAsync(requestUrl);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<FileContentResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    _logger.LogInformation($"Successfully retrieved content for file: {filePath}");
                    return result;
                }
                else
                {
                    _logger.LogWarning($"Failed to retrieve file content. Status code: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving file content: {ex.Message}");
                return null;
            }
        }
    }
}