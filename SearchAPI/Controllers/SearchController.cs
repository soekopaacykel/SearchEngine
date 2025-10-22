using Core;
using Microsoft.AspNetCore.Mvc;
using SearchAPI.Logic;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace SearchAPI.Controllers;

[ApiController]
[Route("api")]
public class SearchController : ControllerBase
{
    // Use lazy initialization to avoid throwing during type initialization when DB files are missing
    private static Lazy<IDatabase> _databaseLazy = new Lazy<IDatabase>(() =>
    {
        try
        {
            return BuildDatabase();
        }
        catch (Exception ex)
        {
            // Return a harmless no-op database that yields empty results but allows the controller to load
            Console.WriteLine($"[SearchAPI] Database initialization failed: {ex.Message}\n{ex}");
            return new NullDatabase();
        }
    }, isThreadSafe: true);

    private static IDatabase mDatabase => _databaseLazy.Value;

    private static IDatabase BuildDatabase()
    {
        // If shard databases are configured and exist, build a sharded database; otherwise, single DB
        var shardPaths = Core.Paths.SQLITE_SHARD_DATABASES;
        var validShardPaths = shardPaths?.Where(p => !string.IsNullOrWhiteSpace(p) && System.IO.File.Exists(p)).ToList() ?? new List<string>();
        if (validShardPaths.Count >= 2)
        {
            var labeled = validShardPaths.Select(p => (label: p, db: (IDatabase)new DatabaseSqlite(p))).ToList();
            return new ShardedDatabase(labeled);
        }
        else
        {
            // Single DB mode: only use if the file actually exists; otherwise return NullDatabase to avoid runtime errors
            var singlePath = Core.Paths.SQLITE_DATABASE;
            if (!System.IO.File.Exists(singlePath))
            {
                Console.WriteLine($"[SearchAPI] SQLite DB file not found: {singlePath}. Returning NullDatabase.");
                return new NullDatabase();
            }
            return new DatabaseSqlite(singlePath);
        }
    }

    [HttpGet]
    [Route("search/{query}/{maxAmount}")]
    public SearchResult Search(string query, int maxAmount)
    {
        try
        {
            var logic = new SearchLogic(mDatabase);
            var result = logic.Search(query.Split(","), maxAmount);

            // Attach diagnostics for API and shard usage
            result.ApiInstance = $"SearchAPI@{Environment.MachineName}";
            if (mDatabase is ShardedDatabase sh)
            {
                result.DatabaseMode = "Sharded";
                result.ShardsUsed = sh.Labels.ToList();
                // Provide mapping only for returned documents
                var map = new Dictionary<int, string>();
                foreach (var hit in result.DocumentHits)
                {
                    if (hit?.Document != null && sh.LastDocShardMap.TryGetValue(hit.Document.mId, out var label))
                    {
                        map[hit.Document.mId] = label;
                    }
                }
                result.DocShard = map;
            }
            else
            {
                if (mDatabase is NullDatabase)
                {
                    result.DatabaseMode = "Unavailable";
                    result.ShardsUsed = new List<string>();
                }
                else
                {
                    result.DatabaseMode = "Single";
                    result.ShardsUsed = new List<string> { Core.Paths.SQLITE_DATABASE };
                }
                result.DocShard = new Dictionary<int, string>();
            }
            return result;
        }
        catch (Exception ex)
        {
            // Prevent 500: return an empty result with diagnostic info
            Console.WriteLine($"[SearchAPI] Error during search: {ex.Message}\n{ex}");
            return new SearchResult
            {
                Query = query.Split(","),
                Hits = 0,
                DocumentHits = new List<DocumentHit>(),
                Ignored = new List<string>
                {
                    "Search backend error: " + ex.Message,
                    "Ensure database files are available inside the container and paths are configured via env: SEARCH_SQLITE_DB or SEARCH_SQLITE_SHARDS",
                    $"Configured single DB: {Core.Paths.SQLITE_DATABASE}",
                    $"Configured shards: {string.Join(", ", Core.Paths.SQLITE_SHARD_DATABASES)}",
                    "If running with docker-compose, ensure your ./data folder contains the .db files and is mounted to /data"
                },
                TimeUsed = TimeSpan.Zero,
                ApiInstance = $"SearchAPI@{Environment.MachineName}",
                DatabaseMode = "Unavailable",
                ShardsUsed = new List<string>(),
                DocShard = new Dictionary<int, string>()
            };
        }
    }

    [HttpGet]
    [Route("ping")]
    public string? Ping()
    {
        string dbMode = mDatabase is SearchAPI.Logic.ShardedDatabase ? "Sharded" : (mDatabase is SearchAPI.Logic.NullDatabase ? "Unavailable" : "Single");
        return $"searchAPI|DB:{dbMode}";
    }

    [HttpGet]
    [Route("file-content")]
    public IActionResult GetFileContent([FromQuery] string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return BadRequest("File path is required");
            }

            // Check if file exists
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound($"File not found: {filePath}");
            }

            // Read the file content
            string content = System.IO.File.ReadAllText(filePath);

            return Ok(new
            {
                content = content,
                filePath = filePath,
                fileName = Path.GetFileName(filePath)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error reading file: {ex.Message}");
        }
    }
}
