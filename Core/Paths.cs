namespace Core;

public class Paths
{
    public static readonly string SQLITE_DATABASE = @"C:\Users\cbjoe\OneDrive\Desktop\skole\oleCaseDB\oleDB.db";

    // Optional: List of shard database file paths for Y-scaling (fan-out search)
    public static readonly string[] SQLITE_SHARD_DATABASES = new string[]
    {
        @"C:\Users\cbjoe\OneDrive\Desktop\skole\oleCaseDB\oleDB_shard1.db",
        @"C:\Users\cbjoe\OneDrive\Desktop\skole\oleCaseDB\oleDB_shard2.db"
    };

    public static readonly string POSTGRES_DATABASE = "Server=127.0.0.1:5432;User Id=oleeriksen;Password=1234;database=search";
}