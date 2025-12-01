using Core;
using Microsoft.AspNetCore.Mvc;
using SearchAPI.Logic;
using System.Diagnostics.Metrics;
using System.Diagnostics;
using System.Text.RegularExpressions;

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
    private static readonly Histogram<double> _searchLatencyMs =
        _meter.CreateHistogram<double>("search_latency_ms", unit: "ms", description: "End-to-end search latency in milliseconds");
    private static readonly Counter<int> _searchErrorsCounter =
        _meter.CreateCounter<int>("search_errors_total", unit: "count", description: "Total number of failed search requests");

    // Bounded-cardinality recent searches table (for Grafana table panel)
    // Emits up to N latest search durations as an observable gauge with fixed slots.
    private const int RecentCapacity = 100;
    private static readonly double[] _recentDurations = new double[RecentCapacity];
    private static readonly string[] _recentUsers = new string[RecentCapacity];
    private static readonly string[] _recentQueries = new string[RecentCapacity];
    private static int _recentIndex = -1;
    private static readonly object _recentLock = new object();

    // Register per-slot observable gauges (distinct metric names) for recent searches
    static SearchController()
    {
        for (int i = 0; i < RecentCapacity; i++)
        {
            int slot = i;
            _meter.CreateObservableGauge<double>(
                name: $"search_recent_ms_{slot}",
                observeValue: () =>
                {
                    double v;
                    string u;
                    string q;
                    lock (_recentLock)
                    {
                        v = _recentDurations[slot];
                        u = _recentUsers[slot] ?? string.Empty;
                        q = _recentQueries[slot] ?? string.Empty;
                    }
                    var tags = new TagList
                    {
                        new KeyValuePair<string, object?>("req_slot", slot.ToString()),
                        new KeyValuePair<string, object?>("user_id", u),
                        new KeyValuePair<string, object?>("query", q),
                        new KeyValuePair<string, object?>("db_mode", DbMode),
                        new KeyValuePair<string, object?>("shards_x", ShardsX),
                        new KeyValuePair<string, object?>("shards_y", ShardsY)
                    };
                    // Always emit a measurement (0 means empty/not yet filled)
                    return new Measurement<double>(v, tags);
                },
                unit: "ms",
                description: "Recent search duration (bounded-cardinality slot)");
        }
    }

    // Labels to compare monolith vs X- and Y-sharded setups
    private static readonly string DbMode = Environment.GetEnvironmentVariable("DATABASE_MODE") ?? "unknown"; // monolith|x-sharded|y-sharded
    private static readonly string ShardsX = Environment.GetEnvironmentVariable("SHARDS_X") ?? "0"; // horizontal shards count
    private static readonly string ShardsY = Environment.GetEnvironmentVariable("SHARDS_Y") ?? "0"; // vertical split parts

    public SearchController(ILogger<SearchController> logger)
    {
        _logger = logger;
    }
    
    private static readonly bool EnableUserLabel =
        (Environment.GetEnvironmentVariable("METRICS_USER_LABEL") ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);

    private string GetUserLabel()
    {
        if (!EnableUserLabel) return "disabled";

        string? userId = null;
        // Prefer explicit header, fall back to query parameter `user`
        if (Request.Headers.TryGetValue("X-User-Id", out var hdr) && !string.IsNullOrWhiteSpace(hdr))
        {
            userId = hdr.ToString();
        }
        else if (Request.Query.TryGetValue("user", out var q) && !string.IsNullOrWhiteSpace(q))
        {
            userId = q.ToString();
        }

        userId = string.IsNullOrWhiteSpace(userId) ? "unknown" : userId.Trim();
        // Sanitize to keep cardinality controlled: allow alphanum and a few safe symbols, cap length
        userId = Regex.Replace(userId, @"[^A-Za-z0-9_.-]+", "_");
        if (userId.Length > 32) userId = userId.Substring(0, 32);
        return userId;
    }
    
    [HttpGet]
    [Route("search/{query}/{maxAmount}")]
    public SearchResult Search(string query, int maxAmount)
    {
        _logger.LogInformation("Search request received: Query={Query}, MaxAmount={MaxAmount}", query, maxAmount);
        var userLabel = GetUserLabel();
        _searchRequestsCounter.Add(1, new KeyValuePair<string, object?>("db_mode", DbMode),
                                      new KeyValuePair<string, object?>("shards_x", ShardsX),
                                      new KeyValuePair<string, object?>("shards_y", ShardsY),
                                      new KeyValuePair<string, object?>("user_id", userLabel));

        var sw = Stopwatch.StartNew();
        try
        {
            var result = mSearchLogic.Search(query.Split(","), maxAmount);
            sw.Stop();

            _searchLatencyMs.Record(sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("db_mode", DbMode),
                new KeyValuePair<string, object?>("shards_x", ShardsX),
                new KeyValuePair<string, object?>("shards_y", ShardsY),
                new KeyValuePair<string, object?>("user_id", userLabel));

            // Save to bounded recent buffer for Grafana table
            var preview = string.Join(',', query.Split(',')).Trim();
            if (preview.Length > 40) preview = preview.Substring(0, 40);
            preview = Regex.Replace(preview, @"[^A-Za-z0-9_.\-]+", "_");
            int slot = Interlocked.Increment(ref _recentIndex) % RecentCapacity;
            if (slot < 0) slot += RecentCapacity;
            lock (_recentLock)
            {
                _recentDurations[slot] = sw.Elapsed.TotalMilliseconds;
                _recentUsers[slot] = userLabel;
                _recentQueries[slot] = preview;
            }

            _logger.LogInformation("Search completed: Query={Query}, ResultCount={ResultCount}, TimeUsed={TimeUsed}ms",
                query, result.DocumentHits.Count, result.TimeUsed.TotalMilliseconds);
            _searchResultsCounter.Add(result.DocumentHits.Count,
                new KeyValuePair<string, object?>("db_mode", DbMode),
                new KeyValuePair<string, object?>("shards_x", ShardsX),
                new KeyValuePair<string, object?>("shards_y", ShardsY),
                new KeyValuePair<string, object?>("user_id", userLabel));

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _searchErrorsCounter.Add(1,
                new KeyValuePair<string, object?>("db_mode", DbMode),
                new KeyValuePair<string, object?>("shards_x", ShardsX),
                new KeyValuePair<string, object?>("shards_y", ShardsY),
                new KeyValuePair<string, object?>("user_id", userLabel));
            _searchLatencyMs.Record(sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("db_mode", DbMode),
                new KeyValuePair<string, object?>("shards_x", ShardsX),
                new KeyValuePair<string, object?>("shards_y", ShardsY),
                new KeyValuePair<string, object?>("user_id", userLabel));
            _logger.LogError(ex, "Search failed for Query={Query}", query);
            throw;
        }
    }

    [HttpGet]
    [Route("search/{query}/{maxAmount}/{caseSensitive}")]
    public SearchResult Search(string query, int maxAmount, bool caseSensitive)
    {
        _logger.LogInformation("Search request received: Query={Query}, MaxAmount={MaxAmount}, CaseSensitive={CaseSensitive}", 
            query, maxAmount, caseSensitive);
        var userLabel = GetUserLabel();
        _searchRequestsCounter.Add(1, new KeyValuePair<string, object?>("db_mode", DbMode),
                                      new KeyValuePair<string, object?>("shards_x", ShardsX),
                                      new KeyValuePair<string, object?>("shards_y", ShardsY),
                                      new KeyValuePair<string, object?>("user_id", userLabel));
        mSearchLogic.SetCaseSensitivity(caseSensitive);
        var sw = Stopwatch.StartNew();
        try
        {
            var result = mSearchLogic.Search(query.Split(","), maxAmount);
            sw.Stop();

            _searchLatencyMs.Record(sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("db_mode", DbMode),
                new KeyValuePair<string, object?>("shards_x", ShardsX),
                new KeyValuePair<string, object?>("shards_y", ShardsY),
                new KeyValuePair<string, object?>("user_id", userLabel));

            _logger.LogInformation("Search completed: Query={Query}, ResultCount={ResultCount}, TimeUsed={TimeUsed}ms",
                query, result.DocumentHits.Count, result.TimeUsed.TotalMilliseconds);
            _searchResultsCounter.Add(result.DocumentHits.Count,
                new KeyValuePair<string, object?>("db_mode", DbMode),
                new KeyValuePair<string, object?>("shards_x", ShardsX),
                new KeyValuePair<string, object?>("shards_y", ShardsY),
                new KeyValuePair<string, object?>("user_id", userLabel));

            // Save to bounded recent buffer for Grafana table
            var preview = string.Join(',', query.Split(',')).Trim();
            if (preview.Length > 40) preview = preview.Substring(0, 40);
            preview = Regex.Replace(preview, @"[^A-Za-z0-9_.\-]+", "_");
            int slot = Interlocked.Increment(ref _recentIndex) % RecentCapacity;
            if (slot < 0) slot += RecentCapacity;
            lock (_recentLock)
            {
                _recentDurations[slot] = sw.Elapsed.TotalMilliseconds;
                _recentUsers[slot] = userLabel;
                _recentQueries[slot] = preview;
            }

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _searchErrorsCounter.Add(1,
                new KeyValuePair<string, object?>("db_mode", DbMode),
                new KeyValuePair<string, object?>("shards_x", ShardsX),
                new KeyValuePair<string, object?>("shards_y", ShardsY),
                new KeyValuePair<string, object?>("user_id", userLabel));
            _searchLatencyMs.Record(sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("db_mode", DbMode),
                new KeyValuePair<string, object?>("shards_x", ShardsX),
                new KeyValuePair<string, object?>("shards_y", ShardsY),
                new KeyValuePair<string, object?>("user_id", userLabel));
            _logger.LogError(ex, "Search failed for Query={Query}", query);
            throw;
        }
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
