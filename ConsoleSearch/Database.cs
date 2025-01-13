using System;
using System.Collections.Generic;
using Shared;
using Shared.Model;
using Microsoft.Data.Sqlite;

namespace ConsoleSearch
{
    public class Database : IDatabase
    {
        private SqliteConnection _connection;

        private Dictionary<string, int> mWords = null;

        public Database()
        {
            var connectionStringBuilder = new SqliteConnectionStringBuilder();

            connectionStringBuilder.DataSource = Paths.DATABASE;


            _connection = new SqliteConnection(connectionStringBuilder.ConnectionString);

            _connection.Open();


        }

        private void Execute(string sql)
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
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

            var sql = "SELECT docId, COUNT(wordId) as count FROM Occ where ";
            sql += "wordId in " + AsString(wordIds) + " GROUP BY docId ";
            sql += "ORDER BY count DESC;";

            var selectCmd = _connection.CreateCommand();
            selectCmd.CommandText = sql;

            using (var reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var docId = reader.GetInt32(0);
                    var count = reader.GetInt32(1);

                    res.Add(new KeyValuePair<int, int>(docId, count));
                }
            }

            return res;
        }

        private string AsString(List<int> x) => $"({string.Join(',', x)})";



       

        private Dictionary<string, int> GetAllWords()
        {
            Dictionary<string, int> res = new Dictionary<string, int>();

            var selectCmd = _connection.CreateCommand();
            selectCmd.CommandText = "SELECT * FROM word";

            using (var reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var id = reader.GetInt32(0);
                    var w = reader.GetString(1);

                    res.Add(w, id);
                }
            }
            return res;
        }

        public List<BEDocument> GetDocDetails(List<int> docIds)
        {
            List<BEDocument> res = new List<BEDocument>();

            var selectCmd = _connection.CreateCommand();
            selectCmd.CommandText = "SELECT * FROM document where id in " + AsString(docIds);

            using (var reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var id = reader.GetInt32(0);
                    var url = reader.GetString(1);
                    var idxTime = reader.GetString(2);
                    var creationTime = reader.GetString(3);

                    res.Add(new BEDocument { mId = id, mUrl = url, mIdxTime = idxTime, mCreationTime = creationTime });
                }
            }
            return res;
        }

        /* Return a list of id's for words; all them among wordIds, but not present in the document
         */
        public List<int> getMissing(int docId, List<int> wordIds)
        {
            var sql = "SELECT wordId FROM Occ where ";
            sql += "wordId in " + AsString(wordIds) + " AND docId = " + docId;
            sql += " ORDER BY wordId;";

            var selectCmd = _connection.CreateCommand();
            selectCmd.CommandText = sql;

            List<int> present = new List<int>();

            using (var reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var wordId = reader.GetInt32(0);
                    present.Add(wordId);
                }
            }
            var result = new List<int>(wordIds);
            foreach (var w in present)
                result.Remove(w);


            return result;
        }

        public List<string> WordsFromIds(List<int> wordIds)
        {
            var sql = "SELECT name FROM Word where ";
            sql += "id in " + AsString(wordIds);

            var selectCmd = _connection.CreateCommand();
            selectCmd.CommandText = sql;

            List<string> result = new List<string>();

            using (var reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var wordId = reader.GetString(0);
                    result.Add(wordId);
                }
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
}
