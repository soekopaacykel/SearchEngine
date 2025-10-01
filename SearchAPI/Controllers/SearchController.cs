using Core;
using Microsoft.AspNetCore.Mvc;
using SearchAPI.Logic;
using System.IO;
using System.Text;

namespace SearchAPI.Controllers;

[ApiController]
[Route("api")]
public class SearchController : ControllerBase
{
    private static IDatabase mDatabase = new DatabaseSqlite();

    [HttpGet]
    [Route("search/{query}/{maxAmount}")]
    public SearchResult Search(string query, int maxAmount)
    {
        var logic = new SearchLogic(mDatabase);
        return logic.Search(query.Split(","), maxAmount);
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
