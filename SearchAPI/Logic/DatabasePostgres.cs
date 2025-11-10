using System;
using System.Collections.Generic;

namespace Core;

using Npgsql;

public class DatabasePostgres : IDatabase
{
    private readonly DatabaseShardManager _shardManager;
    private Dictionary<string, int>? mWords = null;

    public DatabasePostgres()
    {
        _shardManager = new DatabaseShardManager();
        
        // Test connectivity to all shards
        var health = _shardManager.GetHealthStatus();
        if (!health.IsHealthy)
        {
            throw new InvalidOperationException($"Not all database shards are healthy: {health.HealthyShardCount}/{health.TotalShardCount} available");
        }
    }

    // key is the id of the document, the value is number of search words in the document
    public List<KeyValuePair<int, int>> GetDocuments(List<int> wordIds)
    {
        var res = new List<KeyValuePair<int, int>>();

        /* Example sql statement looking for doc id's that
           contain words with id 2 and 3
        
           SELECT docId, COUNT(wordId) as count
             FROM Occ
            WHERE wordId in (2,3)
         GROUP BY docId
         ORDER BY COUNT(wordId) DESC 
         */

        var sql = "SELECT docId, COUNT(wordId) as count FROM occ where ";
        sql += "wordId in " + AsString(wordIds) + " GROUP BY docId ";
        sql += "ORDER BY count DESC;";

        using var connection = _shardManager.GetConnection(DatabaseShard.Occurrences);
        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = sql;

        using var reader = selectCmd.ExecuteReader();
        while (reader.Read())
        {
            var docId = reader.GetInt32(0);
            var count = reader.GetInt32(1);

            res.Add(new KeyValuePair<int, int>(docId, count));
        }

        return res;
    }

    private string AsString(List<int> x) => $"({string.Join(',', x)})";

    private Dictionary<string, int> GetAllWords()
    {
        Dictionary<string, int> res = new Dictionary<string, int>();

        try
        {
            using var connection = _shardManager.GetConnection(DatabaseShard.Words);
            var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT * FROM word";

            Console.WriteLine($"Executing GetAllWords query: {selectCmd.CommandText}");
            using var reader = selectCmd.ExecuteReader();
            int count = 0;
            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                var w = reader.GetString(1);

                res.Add(w, id);
                count++;
            }
            Console.WriteLine($"GetAllWords loaded {count} words from database");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetAllWords: {ex.Message}");
        }
        return res;
    }

    public BEDocument? GetDocDetails(int docId)
    {
        using var connection = _shardManager.GetConnection(DatabaseShard.Documents);
        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = $"SELECT * FROM document where id = {docId}";

        using var reader = selectCmd.ExecuteReader();
        if (reader.Read())
        {
            var id = reader.GetInt32(0);
            var url = reader.GetString(1);
            var idxTime = reader.GetString(2);
            var creationTime = reader.GetString(3);

            return new BEDocument { mId = id, mUrl = url, mIdxTime = idxTime, mCreationTime = creationTime };
        }

        return null;
    }

    /* Return a list of id's for words; all them among wordIds, but not present in the document
     */
    public List<int> getMissing(int docId, List<int> wordIds)
    {
        var sql = "SELECT wordId FROM occ where ";
        sql += "wordId in " + AsString(wordIds) + " AND docId = " + docId;
        sql += " ORDER BY wordId;";

        using var connection = _shardManager.GetConnection(DatabaseShard.Occurrences);
        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = sql;

        List<int> present = new List<int>();

        using var reader = selectCmd.ExecuteReader();
        while (reader.Read())
        {
            var wordId = reader.GetInt32(0);
            present.Add(wordId);
        }

        var result = new List<int>(wordIds);
        foreach (var w in present)
            result.Remove(w);

        return result;
    }

    public List<string> WordsFromIds(List<int> wordIds)
    {
        List<string> result = new List<string>();

        if (wordIds.Count == 0)
            return result;
            
        var sql = "SELECT name FROM word where ";
        sql += "id in " + AsString(wordIds);

        using var connection = _shardManager.GetConnection(DatabaseShard.Words);
        var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = sql;
        
        using var reader = selectCmd.ExecuteReader();
        while (reader.Read())
        {
            var wordName = reader.GetString(0);
            result.Add(wordName);
        }
        
        return result;
    }

    public List<int> GetWordIds(string[] query, out List<string> outIgnored)
    {
        if (mWords == null)
            mWords = GetAllWords();
            
        var res = new List<int>();
        var ignored = new List<string>();

        foreach (var aWord in query)
        {
            if (mWords.ContainsKey(aWord))
                res.Add(mWords[aWord]);
            else
                ignored.Add(aWord);
        }
        outIgnored = ignored;
        return res;
    }
}