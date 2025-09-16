using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SearchWeb.Services;
using Core;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace SearchWeb.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly SearchService _searchService;
    private readonly IConfiguration _configuration;

    [BindProperty(SupportsGet = true)]
    public string SearchQuery { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    [Range(1, 100, ErrorMessage = "Results must be between 1 and 100.")]
    public int MaxResults { get; set; } = 10;

    [BindProperty]
    public string ApiUrl { get; set; } = string.Empty;

    public SearchResult? SearchResult { get; private set; }

    public bool HasSearched { get; private set; }

    public bool IsApiAvailable => _searchService.ApiIsAvailable;

    public string ErrorMessage { get; private set; } = string.Empty;

    public IndexModel(ILogger<IndexModel> logger, SearchService searchService, IConfiguration configuration)
    {
        _logger = logger;
        _searchService = searchService;
        _configuration = configuration;
    }

    public async Task OnGetAsync()
    {
        // Set the current API URL from configuration
        ApiUrl = _configuration["SearchApi:BaseUrl"] ?? "http://localhost:5154";

        // Check API health if needed
        if (!IsApiAvailable)
        {
            await _searchService.CheckApiHealthAsync();
        }

        // If user submitted a search query
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            HasSearched = true;
            SearchResult = await _searchService.SearchAsync(SearchQuery, MaxResults);

            if (SearchResult != null && SearchResult.Hits > 0)
            {
                _logger.LogInformation($"Search performed for '{SearchQuery}' with {SearchResult.Hits} results");
            }
            else
            {
                _logger.LogInformation($"Search performed for '{SearchQuery}' but no results were found or API is unavailable");
            }
        }
    }

    public async Task<IActionResult> OnPostUpdateApiAsync()
    {
        if (!string.IsNullOrWhiteSpace(ApiUrl))
        {
            try
            {
                // Update configuration with new URL
                _configuration["SearchApi:BaseUrl"] = ApiUrl;

                // Update service with new URL
                _searchService.UpdateApiUrl(ApiUrl);

                // Test the connection
                var isAvailable = await _searchService.CheckApiHealthAsync();

                if (isAvailable)
                {
                    TempData["SuccessMessage"] = "API URL updated and connected successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "API URL updated but could not connect. The application will run in offline mode.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating API URL");
                TempData["ErrorMessage"] = $"Error updating API URL: {ex.Message}";
            }
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCheckHealthAsync()
    {
        var isAvailable = await _searchService.CheckApiHealthAsync();

        if (isAvailable)
        {
            TempData["SuccessMessage"] = "API is available and responding.";
        }
        else
        {
            TempData["ErrorMessage"] = "API is not responding. The application will run in offline mode.";
        }

        return RedirectToPage();
    }
}
