using System;
using System.Collections.Generic;

namespace Core;

using Npgsql;

public class DatabasePostgres : IDatabase
{
    private readonly DatabaseShardManager _shardManager;
    private readonly Func<DatabaseShard, NpgsqlConnection> _getConnection;
    private readonly bool _monoMode;
    private Dictionary<string, int>? mWords = null;

    public DatabasePostgres()
    {
        _shardManager = new DatabaseShardManager();

        // Detect mono mode WITHOUT touching Core
        var mode = Environment.GetEnvironmentVariable("DATABASE_MODE") ?? string.Empty; // monolith|x-sharded|y-sharded
        var shardsX = Environment.GetEnvironmentVariable("SHARDS_X") ?? "0";
        var shardsY = Environment.GetEnvironmentVariable("SHARDS_Y") ?? "0";
        _monoMode = string.Equals(mode, "monolith", StringComparison.OrdinalIgnoreCase)
                    || (shardsX == "0" && shardsY == "0");

        if (_monoMode)
        {
            // Route all logical shards to the single legacy connection string
            _getConnection = _ =>
            {
                var c = new NpgsqlConnection(Paths.POSTGRES_DATABASE);
                c.Open();
                return c;
            };
            // Do NOT check connectivity in constructor — defer to first use so the app can start
            // even if the database is still coming up. Failures will surface on query execution
            // and be handled by the caller (controller) with appropriate HTTP status and metrics.
        }
        else
        {
            // Sharded path: configure connection factory; avoid failing constructor on transient shard issues.
            _getConnection = shard => _shardManager.GetConnection(shard);
            // Health will be implicitly validated when connections are requested by methods.
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

        using var connection = _getConnection(DatabaseShard.Occurrences);
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
            using var connection = _getConnection(DatabaseShard.Words);
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
        using var connection = _getConnection(DatabaseShard.Documents);
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

        using var connection = _getConnection(DatabaseShard.Occurrences);
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

        using var connection = _getConnection(DatabaseShard.Words);
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