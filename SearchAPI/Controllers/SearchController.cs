using Core;
using Microsoft.AspNetCore.Mvc;
using SearchAPI.Logic;

namespace SearchAPI.Controllers;

[ApiController]
[Route("api")]
public class SearchController : ControllerBase
{
    private static IDatabase mDatabase = new DatabaseSqlite();
    private static SearchLogic mSearchLogic = new SearchLogic(mDatabase);
    
    [HttpGet]
    [Route("search/{query}/{maxAmount}")]
    public SearchResult Search(string query, int maxAmount)
    {
        return mSearchLogic.Search(query.Split(","), maxAmount);
    }

    [HttpGet]
    [Route("search/{query}/{maxAmount}/{caseSensitive}")]
    public SearchResult Search(string query, int maxAmount, bool caseSensitive)
    {
        mSearchLogic.SetCaseSensitivity(caseSensitive);
        return mSearchLogic.Search(query.Split(","), maxAmount);
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
        return "searchAPI";
    }
    
}
