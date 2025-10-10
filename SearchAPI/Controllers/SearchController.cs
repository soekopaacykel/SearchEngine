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
    private static IDatabase mDatabase = BuildDatabase();

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
            // Fallback to legacy single database path
            return new DatabaseSqlite();
        }
    }

    [HttpGet]
    [Route("search/{query}/{maxAmount}")]
    public SearchResult Search(string query, int maxAmount)
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
            result.DatabaseMode = "Single";
            result.ShardsUsed = new List<string> { Core.Paths.SQLITE_DATABASE };
            result.DocShard = new Dictionary<int, string>();
        }
        return result;
    }

    [HttpGet]
    [Route("ping")]
    public string? Ping()
    {
        return "searchAPI";
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
