using System;
using System.Collections.Generic;
using Core;
using Npgsql;

namespace Indexer;

public class DatabasePostgres : IDatabase
{
    private readonly DatabaseShardManager _shardManager;

    public DatabasePostgres()
    {
        _shardManager = new DatabaseShardManager();
        
        Console.WriteLine("Connecting to database shards...");
        
        // Test connectivity
        var health = _shardManager.GetHealthStatus();
        if (!health.IsHealthy)
        {
            Console.WriteLine($"Warning: Not all database shards are healthy: {health.HealthyShardCount}/{health.TotalShardCount} available");
        }
        else
        {
            Console.WriteLine("All database shards are healthy");
        }
    }

    public void InsertAllWords(Dictionary<string, int> res)
    {
        using var connection = _shardManager.GetConnection(DatabaseShard.Words);
        
        // Ensure table exists BEFORE transaction
        try
        {
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT 1 FROM word LIMIT 1";
            checkCmd.ExecuteScalar();
        }
        catch
        {
            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = "CREATE TABLE IF NOT EXISTS word(id INTEGER PRIMARY KEY, name TEXT UNIQUE)";
            createCmd.ExecuteNonQuery();
        }
        
        using var transaction = connection.BeginTransaction();
        
        var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO word(id, name) VALUES(@id,@name) ON CONFLICT (id) DO NOTHING";

        var paramName = command.CreateParameter();
        paramName.ParameterName = "name";
        command.Parameters.Add(paramName);

        var paramId = command.CreateParameter();
        paramId.ParameterName = "id";
        command.Parameters.Add(paramId);

        // Insert all entries in the res
        foreach (var p in res)
        {
            paramName.Value = p.Key;
            paramId.Value = p.Value;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void InsertAllOcc(int docId, ISet<int> wordIds)
    {
        using var connection = _shardManager.GetConnection(DatabaseShard.Occurrences);
        
        // Ensure table exists BEFORE transaction
        try
        {
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT 1 FROM occ LIMIT 1";
            checkCmd.ExecuteScalar();
        }
        catch
        {
            // Table doesn't exist, create it
            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = "CREATE TABLE IF NOT EXISTS occ(wordId INTEGER, docId INTEGER); CREATE INDEX IF NOT EXISTS word_index ON occ (wordId); CREATE INDEX IF NOT EXISTS doc_index ON occ (docId);";
            createCmd.ExecuteNonQuery();
        }
        
        using var transaction = connection.BeginTransaction();
        
        var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO occ(wordId, docId) VALUES(@wordId,@docId) ON CONFLICT DO NOTHING";

        var paramwordId = command.CreateParameter();
        paramwordId.ParameterName = "wordId";
        command.Parameters.Add(paramwordId);

        var paramDocId = command.CreateParameter();
        paramDocId.ParameterName = "docId";
        paramDocId.Value = docId;
        command.Parameters.Add(paramDocId);

        foreach (var p in wordIds)
        {
            paramwordId.Value = p;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void InsertWord(int id, string value)
    {
        using var connection = _shardManager.GetConnection(DatabaseShard.Words);
        
        var insertCmd = new NpgsqlCommand("INSERT INTO word(id, name) VALUES(@id,@name) ON CONFLICT (id) DO NOTHING");
        insertCmd.Connection = connection;

        var pName = new NpgsqlParameter("name", value);
        insertCmd.Parameters.Add(pName);

        var pCount = new NpgsqlParameter("id", id);
        insertCmd.Parameters.Add(pCount);

        insertCmd.ExecuteNonQuery();
    }

    public void InsertDocument(BEDocument doc)
    {
        using var connection = _shardManager.GetConnection(DatabaseShard.Documents);
        
        // Ensure table exists
        try
        {
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT 1 FROM document LIMIT 1";
            checkCmd.ExecuteScalar();
        }
        catch
        {
            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = "CREATE TABLE IF NOT EXISTS document(id INTEGER PRIMARY KEY, url TEXT, idxTime TEXT, creationTime TEXT)";
            createCmd.ExecuteNonQuery();
        }
        
        var insertCmd = new NpgsqlCommand(
            "INSERT INTO document(id, url, idxTime, creationTime) VALUES(@id,@url, @idxTime, @creationTime) ON CONFLICT (id) DO NOTHING"
        );
        insertCmd.Connection = connection;

        var pId = new NpgsqlParameter("id", doc.mId);
        insertCmd.Parameters.Add(pId);

        var pUrl = new NpgsqlParameter("url", doc.mUrl);
        insertCmd.Parameters.Add(pUrl);

        var pIdxTime = new NpgsqlParameter("idxTime", doc.mIdxTime);
        insertCmd.Parameters.Add(pIdxTime);

        var pCreationTime = new NpgsqlParameter("creationTime", doc.mCreationTime);
        insertCmd.Parameters.Add(pCreationTime);

        insertCmd.ExecuteNonQuery();
    }

    public Dictionary<string, int> GetAllWords()
    {
        Dictionary<string, int> res = new Dictionary<string, int>();

        using var connection = _shardManager.GetConnection(DatabaseShard.Words);
        
        // Ensure table exists
        try
        {
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT 1 FROM word LIMIT 1";
            checkCmd.ExecuteScalar();
        }
        catch
        {
            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = "CREATE TABLE IF NOT EXISTS word(id INTEGER PRIMARY KEY, name TEXT UNIQUE)";
            createCmd.ExecuteNonQuery();
            // Return empty dictionary if table was just created
            return res;
        }
        
        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = "SELECT * FROM word";

        using var reader = selectCmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var w = reader.GetString(1);

            res.Add(w, id);
        }

        return res;
    }

    public int DocumentCounts
    {
        get
        {
            using var connection = _shardManager.GetConnection(DatabaseShard.Documents);
            var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT count(*) FROM document";

            using var reader = selectCmd.ExecuteReader();
            if (reader.Read())
            {
                var count = reader.GetInt32(0);
                return count;
            }

            return -1;
        }
    }
}