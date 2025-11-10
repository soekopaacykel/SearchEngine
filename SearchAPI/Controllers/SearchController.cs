using Core;
using Microsoft.AspNetCore.Mvc;
using SearchAPI.Logic;
using System.Diagnostics.Metrics;

namespace SearchAPI.Controllers;

[ApiController]
[Route("api")]
public class SearchController : ControllerBase
{
    private static IDatabase mDatabase = new DatabasePostgres();
    private static SearchLogic mSearchLogic = new SearchLogic(mDatabase);
    private readonly ILogger<SearchController> _logger;
    
    // Metrics
    private static readonly Meter _meter = new("SearchAPI.Metrics", "1.0.0");
    private static readonly Counter<int> _searchRequestsCounter = 
        _meter.CreateCounter<int>("search_requests_total", "count", "Total number of search requests");
    private static readonly Counter<int> _searchResultsCounter = 
        _meter.CreateCounter<int>("search_results_total", "count", "Total number of search results returned");
    private static readonly Counter<int> _pingRequestsCounter = 
        _meter.CreateCounter<int>("ping_requests_total", "count", "Total number of ping requests");

    public SearchController(ILogger<SearchController> logger)
    {
        _logger = logger;
    }
    
    [HttpGet]
    [Route("search/{query}/{maxAmount}")]
    public SearchResult Search(string query, int maxAmount)
    {
        _logger.LogInformation("Search request received: Query={Query}, MaxAmount={MaxAmount}", query, maxAmount);
        _searchRequestsCounter.Add(1);
        
        var result = mSearchLogic.Search(query.Split(","), maxAmount);
        
        _logger.LogInformation("Search completed: Query={Query}, ResultCount={ResultCount}, TimeUsed={TimeUsed}ms", 
            query, result.DocumentHits.Count, result.TimeUsed.TotalMilliseconds);
        _searchResultsCounter.Add(result.DocumentHits.Count);
        
        return result;
    }

    [HttpGet]
    [Route("search/{query}/{maxAmount}/{caseSensitive}")]
    public SearchResult Search(string query, int maxAmount, bool caseSensitive)
    {
        _logger.LogInformation("Search request received: Query={Query}, MaxAmount={MaxAmount}, CaseSensitive={CaseSensitive}", 
            query, maxAmount, caseSensitive);
        _searchRequestsCounter.Add(1);
        
        mSearchLogic.SetCaseSensitivity(caseSensitive);
        var result = mSearchLogic.Search(query.Split(","), maxAmount);
        
        _logger.LogInformation("Search completed: Query={Query}, ResultCount={ResultCount}, TimeUsed={TimeUsed}ms", 
            query, result.DocumentHits.Count, result.TimeUsed.TotalMilliseconds);
        _searchResultsCounter.Add(result.DocumentHits.Count);
        
        return result;
    }

    [HttpPost]
    [Route("casesensitivity/{enabled}")]
    public IActionResult SetCaseSensitivity(bool enabled)
    {
        _logger.LogInformation("Case sensitivity setting changed to: {CaseSensitive}", enabled);
        mSearchLogic.SetCaseSensitivity(enabled);
        return Ok(new { caseSensitive = enabled, message = $"Case sensitivity is now {(enabled ? "ON" : "OFF")}" });
    }

    [HttpGet]
    [Route("casesensitivity")]
    public IActionResult GetCaseSensitivity()
    {
        var caseSensitive = mSearchLogic.IsCaseSensitive();
        _logger.LogDebug("Case sensitivity status requested: {CaseSensitive}", caseSensitive);
        return Ok(new { caseSensitive = caseSensitive });
    }

    [HttpGet]
    [Route("ping")]
    public string? Ping()
    {
        _logger.LogDebug("Ping request received");
        _pingRequestsCounter.Add(1);
        return "searchAPI";
    }
    
}
