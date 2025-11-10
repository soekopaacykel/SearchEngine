using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;

namespace Core
{
    public enum DatabaseShard
    {
        Words,
        Documents,
        Occurrences
    }

    public class DatabaseShardManager
    {
        private readonly Dictionary<DatabaseShard, string> _connectionStrings;

        public DatabaseShardManager()
        {
            _connectionStrings = new Dictionary<DatabaseShard, string>
            {
                { DatabaseShard.Words, Paths.POSTGRES_WORDS_DATABASE },
                { DatabaseShard.Documents, Paths.POSTGRES_DOCUMENTS_DATABASE },
                { DatabaseShard.Occurrences, Paths.POSTGRES_OCCURRENCES_DATABASE }
            };
        }

        /// <summary>
        /// Gets a connection to the specified database shard
        /// </summary>
        public NpgsqlConnection GetConnection(DatabaseShard shard)
        {
            if (!_connectionStrings.TryGetValue(shard, out string? connectionString))
            {
                throw new ArgumentException($"No connection string configured for shard: {shard}");
            }

            var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Gets connections to multiple shards
        /// </summary>
        public Dictionary<DatabaseShard, NpgsqlConnection> GetConnections(params DatabaseShard[] shards)
        {
            var connections = new Dictionary<DatabaseShard, NpgsqlConnection>();
            
            foreach (var shard in shards)
            {
                connections[shard] = GetConnection(shard);
            }
            
            return connections;
        }

        /// <summary>
        /// Gets connections to all shards
        /// </summary>
        public Dictionary<DatabaseShard, NpgsqlConnection> GetAllConnections()
        {
            return GetConnections(DatabaseShard.Words, DatabaseShard.Documents, DatabaseShard.Occurrences);
        }

        /// <summary>
        /// Initialize database schemas for each shard
        /// </summary>
        public void InitializeSchemas()
        {
            InitializeWordsSchema();
            InitializeDocumentsSchema();
            InitializeOccurrencesSchema();
        }

        private void InitializeWordsSchema()
        {
            using var connection = GetConnection(DatabaseShard.Words);
            
            var commands = new[]
            {
                "DROP TABLE IF EXISTS word",
                "CREATE TABLE word(id INTEGER PRIMARY KEY, name TEXT UNIQUE)"
            };

            foreach (var sql in commands)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        private void InitializeDocumentsSchema()
        {
            using var connection = GetConnection(DatabaseShard.Documents);
            
            var commands = new[]
            {
                "DROP TABLE IF EXISTS document",
                "CREATE TABLE document(id INTEGER PRIMARY KEY, url TEXT, idxTime TEXT, creationTime TEXT)"
            };

            foreach (var sql in commands)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        private void InitializeOccurrencesSchema()
        {
            using var connection = GetConnection(DatabaseShard.Occurrences);
            
            var commands = new[]
            {
                "DROP TABLE IF EXISTS occ",
                "CREATE TABLE occ(wordId INTEGER, docId INTEGER)",
                "CREATE INDEX word_index ON occ (wordId)",
                "CREATE INDEX doc_index ON occ (docId)"
            };

            foreach (var sql in commands)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Test connectivity to all database shards
        /// </summary>
        public DatabaseShardHealthStatus GetHealthStatus()
        {
            var status = new DatabaseShardHealthStatus();

            foreach (var shard in Enum.GetValues<DatabaseShard>())
            {
                try
                {
                    using var connection = GetConnection(shard);
                    // Simple connectivity test
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT 1";
                    cmd.ExecuteScalar();
                    
                    status.AvailableShards[shard] = true;
                }
                catch (Exception ex)
                {
                    status.AvailableShards[shard] = false;
                    status.ShardErrors[shard] = ex.Message;
                }
            }

            return status;
        }

        /// <summary>
        /// Execute a command on a specific shard
        /// </summary>
        public void ExecuteCommand(DatabaseShard shard, string sql, Dictionary<string, object>? parameters = null)
        {
            using var connection = GetConnection(shard);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    var npgsqlParam = cmd.CreateParameter();
                    npgsqlParam.ParameterName = param.Key;
                    npgsqlParam.Value = param.Value ?? DBNull.Value;
                    cmd.Parameters.Add(npgsqlParam);
                }
            }

            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Execute a query on a specific shard and return results
        /// </summary>
        public List<Dictionary<string, object>> ExecuteQuery(DatabaseShard shard, string sql, Dictionary<string, object>? parameters = null)
        {
            var results = new List<Dictionary<string, object>>();

            using var connection = GetConnection(shard);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    var npgsqlParam = cmd.CreateParameter();
                    npgsqlParam.ParameterName = param.Key;
                    npgsqlParam.Value = param.Value ?? DBNull.Value;
                    cmd.Parameters.Add(npgsqlParam);
                }
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
                }
                results.Add(row);
            }

            return results;
        }
    }

    public class DatabaseShardHealthStatus
    {
        public Dictionary<DatabaseShard, bool> AvailableShards { get; set; } = new();
        public Dictionary<DatabaseShard, string> ShardErrors { get; set; } = new();
        
        public bool IsHealthy => AvailableShards.Values.All(x => x);
        public int HealthyShardCount => AvailableShards.Values.Count(x => x);
        public int TotalShardCount => AvailableShards.Count;
    }
}