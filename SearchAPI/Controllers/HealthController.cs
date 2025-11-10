using Microsoft.AspNetCore.Mvc;
using Core;

namespace SearchAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly DatabaseShardManager _shardManager;

        public HealthController()
        {
            _shardManager = new DatabaseShardManager();
        }

        [HttpGet]
        public IActionResult GetHealth()
        {
            try
            {
                var health = _shardManager.GetHealthStatus();
                
                var response = new
                {
                    IsHealthy = health.IsHealthy,
                    HealthyShardCount = health.HealthyShardCount,
                    TotalShardCount = health.TotalShardCount,
                    Shards = health.AvailableShards.Select(kvp => new
                    {
                        Shard = kvp.Key.ToString(),
                        IsHealthy = kvp.Value,
                        Error = health.ShardErrors.ContainsKey(kvp.Key) ? health.ShardErrors[kvp.Key] : null
                    })
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new { status = "OK", timestamp = DateTime.UtcNow });
        }
    }
}