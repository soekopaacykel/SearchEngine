using System;
using System.Collections.Generic;
using Npgsql;
using Core;

namespace DataMigration
{
    public class DatabaseMigrator
    {
        private readonly string _sourceConnectionString;
        private readonly DatabaseShardManager _shardManager;

        public DatabaseMigrator(string sourceConnectionString)
        {
            _sourceConnectionString = sourceConnectionString;
            _shardManager = new DatabaseShardManager();
        }

        public void MigrateData()
        {
            Console.WriteLine("🚀 Starting database migration from single PostgreSQL to sharded setup...");
            
            try
            {
                // Initialize target schemas
                Console.WriteLine("📋 Initializing target database schemas...");
                _shardManager.InitializeSchemas();
                
                // Migrate Words
                Console.WriteLine("📝 Migrating Words table...");
                MigrateWords();
                
                // Migrate Documents
                Console.WriteLine("📄 Migrating Documents table...");
                MigrateDocuments();
                
                // Migrate Occurrences
                Console.WriteLine("🔗 Migrating Occurrences table...");
                MigrateOccurrences();
                
                Console.WriteLine("✅ Migration completed successfully!");
                PrintMigrationSummary();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Migration failed: {ex.Message}");
                Console.WriteLine($"Details: {ex}");
                throw;
            }
        }

        private void MigrateWords()
        {
            using var sourceConn = new NpgsqlConnection(_sourceConnectionString);
            sourceConn.Open();
            
            using var cmd = sourceConn.CreateCommand();
            cmd.CommandText = "SELECT word FROM word";
            
            var words = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                words.Add(reader.GetString(0));
            }
            reader.Close();
            
            Console.WriteLine($"Found {words.Count} words to migrate");
            
            // Insert into words shard
            foreach (var word in words)
            {
                var parameters = new Dictionary<string, object>
                {
                    { "word", word }
                };
                _shardManager.ExecuteCommand(DatabaseShard.Words, 
                    "INSERT INTO word (word) VALUES (@word) ON CONFLICT DO NOTHING", parameters);
            }
            
            Console.WriteLine($"✅ Migrated {words.Count} words");
        }

        private void MigrateDocuments()
        {
            using var sourceConn = new NpgsqlConnection(_sourceConnectionString);
            sourceConn.Open();
            
            using var cmd = sourceConn.CreateCommand();
            cmd.CommandText = "SELECT id, url, idxTime, creationTime FROM document";
            
            var documents = new List<(int id, string url, string idxTime, string creationTime)>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                documents.Add((
                    reader.GetInt32(0), // id
                    reader.GetString(1), // url
                    reader.GetString(2), // idxTime
                    reader.GetString(3) // creationTime
                ));
            }
            reader.Close();
            
            Console.WriteLine($"Found {documents.Count} documents to migrate");
            
            // Insert into documents shard
            foreach (var doc in documents)
            {
                var parameters = new Dictionary<string, object>
                {
                    { "id", doc.id },
                    { "url", doc.url },
                    { "idxTime", doc.idxTime },
                    { "creationTime", doc.creationTime }
                };
                _shardManager.ExecuteCommand(DatabaseShard.Documents,
                    "INSERT INTO document (id, url, idxTime, creationTime) VALUES (@id, @url, @idxTime, @creationTime) ON CONFLICT (id) DO NOTHING", 
                    parameters);
            }
            
            Console.WriteLine($"✅ Migrated {documents.Count} documents");
        }

        private void MigrateOccurrences()
        {
            using var sourceConn = new NpgsqlConnection(_sourceConnectionString);
            sourceConn.Open();
            
            using var cmd = sourceConn.CreateCommand();
            cmd.CommandText = "SELECT wordId, docId FROM occ";
            
            var occurrences = new List<(int wordId, int docId)>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                occurrences.Add((
                    reader.GetInt32(0), // wordId
                    reader.GetInt32(1)  // docId
                ));
            }
            reader.Close();
            
            Console.WriteLine($"Found {occurrences.Count} occurrences to migrate");
            
            // Insert into occurrences shard
            foreach (var occ in occurrences)
            {
                var parameters = new Dictionary<string, object>
                {
                    { "wordId", occ.wordId },
                    { "docId", occ.docId }
                };
                _shardManager.ExecuteCommand(DatabaseShard.Occurrences,
                    "INSERT INTO occ (wordId, docId) VALUES (@wordId, @docId)", parameters);
            }
            
            Console.WriteLine($"✅ Migrated {occurrences.Count} occurrences");
        }

        private void PrintMigrationSummary()
        {
            Console.WriteLine("\n📊 Migration Summary:");
            Console.WriteLine("===================");
            
            try
            {
                // Count words
                var wordsResult = _shardManager.ExecuteQuery(DatabaseShard.Words, "SELECT COUNT(*) as count FROM word");
                var wordCount = Convert.ToInt32(wordsResult[0]["count"]);
                Console.WriteLine($"Words shard: {wordCount} records");
                
                // Count documents
                var docsResult = _shardManager.ExecuteQuery(DatabaseShard.Documents, "SELECT COUNT(*) as count FROM document");
                var docCount = Convert.ToInt32(docsResult[0]["count"]);
                Console.WriteLine($"Documents shard: {docCount} records");
                
                // Count occurrences
                var occResult = _shardManager.ExecuteQuery(DatabaseShard.Occurrences, "SELECT COUNT(*) as count FROM occ");
                var occCount = Convert.ToInt32(occResult[0]["count"]);
                Console.WriteLine($"Occurrences shard: {occCount} records");
                
                Console.WriteLine($"\nTotal records migrated: {wordCount + docCount + occCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not generate summary: {ex.Message}");
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("🗄️  Database Migration Tool");
            Console.WriteLine("===========================");
            
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: dotnet run <source_connection_string>");
                Console.WriteLine("Example: dotnet run \"Server=localhost;Port=5432;User Id=ameliavalentin;Password=DBkonger1234;Database=search;\"");
                return;
            }
            
            string sourceConnectionString = args[0];
            
            Console.WriteLine($"Source database: {ExtractDbInfo(sourceConnectionString)}");
            Console.WriteLine("Target: 3-shard setup (words/documents/occurrences)");
            Console.WriteLine();
            
            Console.Write("Proceed with migration? (y/N): ");
            var confirm = Console.ReadLine();
            
            if (confirm?.ToLower() != "y")
            {
                Console.WriteLine("Migration cancelled.");
                return;
            }
            
            var migrator = new DatabaseMigrator(sourceConnectionString);
            migrator.MigrateData();
        }
        
        private static string ExtractDbInfo(string connectionString)
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString);
                return $"{builder.Host}:{builder.Port}/{builder.Database}";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}