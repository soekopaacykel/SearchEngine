namespace Core;

public class Paths
{
    // Database locations are configurable via environment variables to work both locally and in containers.
    // - SEARCH_SQLITE_DB: single database file path. Supports absolute Windows paths (e.g., C:\\...\\oleDB.db),
    //   absolute Unix paths (/data/oleDB.db), or relative paths. Defaults:
    //     • Inside containers: /data/oleDB.db
    //     • Local host (no env set): .\\data\\oleDB.db
    // - SEARCH_SQLITE_SHARDS: comma-separated list of shard DB file paths.
    // - POSTGRES_CONNECTION: optional Postgres connection string.

    private static string GetEnv(string name, string? fallback = null)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(v) ? (fallback ?? string.Empty) : v;
    }

    private static bool IsWindows() => OperatingSystem.IsWindows();

    private static string DefaultDataPath(string fileName)
    {
        // Prefer container-friendly /data path, but if running on Windows host and /data doesn't exist,
        // fall back to .\\data for local development.
        var containerPath = $"/data/{fileName}";
        try
        {
            if (IsWindows() && !System.IO.Directory.Exists("/data") && !System.IO.File.Exists(containerPath))
            {
                return $".\\data\\{fileName}";
            }
        }
        catch { /* best-effort fallback */ }
        return containerPath;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        // Expand ~ to user profile on non-Windows
        if (path.StartsWith("~"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = System.IO.Path.Combine(home, path.TrimStart('~', '/', '\\'));
        }
        // If Windows absolute path with forward slashes, normalize to backslashes
        if (IsWindows() && path.Contains('/'))
        {
            path = path.Replace('/', '\\');
        }
        return path;
    }

    private static string[] NormalizePathsCsv(string csv)
    {
        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizePath)
            .ToArray();
    }

    public static readonly string SQLITE_DATABASE = NormalizePath(
        GetEnv("SEARCH_SQLITE_DB", DefaultDataPath("oleDB.db"))
    );

    public static readonly string[] SQLITE_SHARD_DATABASES = NormalizePathsCsv(
        GetEnv("SEARCH_SQLITE_SHARDS",
            string.Join(',', new[] { DefaultDataPath("oleDB_shard1.db"), DefaultDataPath("oleDB_shard2.db") }))
    );

    public static readonly string POSTGRES_DATABASE = GetEnv(
        "POSTGRES_CONNECTION",
        "Server=127.0.0.1:5432;User Id=oleeriksen;Password=1234;database=search"
    );
}