using System;
using Core;

class Program
{
    static void Main()
    {
        try
        {
            var shardManager = new DatabaseShardManager();
            var health = shardManager.GetHealthStatus();
            
            Console.WriteLine($"IsHealthy: {health.IsHealthy}");
            Console.WriteLine($"HealthyShardCount: {health.HealthyShardCount}");
            Console.WriteLine($"TotalShardCount: {health.TotalShardCount}");
            
            foreach (var shard in health.AvailableShards)
            {
                Console.WriteLine($"Shard {shard.Key}: {(shard.Value ? "Healthy" : "Unhealthy")}");
                if (health.ShardErrors.ContainsKey(shard.Key))
                {
                    Console.WriteLine($"  Error: {health.ShardErrors[shard.Key]}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex}");
        }
    }
}