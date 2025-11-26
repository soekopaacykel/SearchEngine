using System;
using System.Collections.Generic;
using Core;
using Npgsql;

namespace Indexer
{
    public class DatabasePostgresMono : IDatabase
    {
        private readonly string _connectionString;

        public DatabasePostgresMono()
        {
            // Use the legacy connection string for mono setup
            _connectionString = "Server=postgres;Port=5432;User Id=ameliavalentin;Password=DBkonger1234;Database=search;";
            
            // Initialize database schema
            InitializeSchema();
        }

        private void InitializeSchema()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            
            var commands = new[]
            {
                "DROP TABLE IF EXISTS occ",
                "DROP TABLE IF EXISTS document", 
                "DROP TABLE IF EXISTS word",
                "CREATE TABLE word(id INTEGER PRIMARY KEY, name TEXT UNIQUE)",
                "CREATE INDEX word_name_index ON word (name)",
                "CREATE TABLE document(id INTEGER PRIMARY KEY, url TEXT, idxTime TEXT, creationTime TEXT)",
                "CREATE INDEX document_url_index ON document (url)",
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

        public void InsertAllWords(Dictionary<string, int> res)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
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
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            
            var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO occ(wordId, docId) VALUES(@wordId,@docId)";

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
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            
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
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            
            var insertCmd = new NpgsqlCommand(
                "INSERT INTO document(id, url, idxTime, creationTime) VALUES(@id,@url, @idxTime, @creationTime)"
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

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
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
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();
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
}