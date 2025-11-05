using Core;
using Microsoft.AspNetCore.Mvc;
using SearchAPI.Logic;
using System.Diagnostics.Metrics;

namespace SearchAPI.Controllers;

[ApiController]
[Route("api")]
public class SearchController : ControllerBase
{
    private static IDatabase mDatabase = new DatabaseSqlite();
    private static SearchLogic mSearchLogic = new SearchLogic(mDatabase);
    
    // Metrics
    private static readonly Meter _meter = new("SearchAPI.Metrics", "1.0.0");
    private static readonly Counter<int> _searchRequestsCounter = 
        _meter.CreateCounter<int>("search_requests_total", "count", "Total number of search requests");
    private static readonly Counter<int> _searchResultsCounter = 
        _meter.CreateCounter<int>("search_results_total", "count", "Total number of search results returned");
    private static readonly Counter<int> _pingRequestsCounter = 
        _meter.CreateCounter<int>("ping_requests_total", "count", "Total number of ping requests");
    
    [HttpGet]
    [Route("search/{query}/{maxAmount}")]
    public SearchResult Search(string query, int maxAmount)
    {
        _searchRequestsCounter.Add(1);
        var result = mSearchLogic.Search(query.Split(","), maxAmount);
        _searchResultsCounter.Add(result.DocumentHits.Count);
        return result;
    }

    [HttpGet]
    [Route("search/{query}/{maxAmount}/{caseSensitive}")]
    public SearchResult Search(string query, int maxAmount, bool caseSensitive)
    {
        _searchRequestsCounter.Add(1);
        mSearchLogic.SetCaseSensitivity(caseSensitive);
        var result = mSearchLogic.Search(query.Split(","), maxAmount);
        _searchResultsCounter.Add(result.DocumentHits.Count);
        return result;
    }

    [HttpPost]
    [Route("casesensitivity/{enabled}")]
    public IActionResult SetCaseSensitivity(bool enabled)
    {
        mSearchLogic.SetCaseSensitivity(enabled);
        return Ok(new { caseSensitive = enabled, message = $"Case sensitivity is now {(enabled ? "ON" : "OFF")}" });
    }

    [HttpGet]
    [Route("casesensitivity")]
    public IActionResult GetCaseSensitivity()
    {
        return Ok(new { caseSensitive = mSearchLogic.IsCaseSensitive() });
    }

    [HttpGet]
    [Route("ping")]
    public string? Ping()
    {
        _pingRequestsCounter.Add(1);
        return "searchAPI";
    }
    
}
