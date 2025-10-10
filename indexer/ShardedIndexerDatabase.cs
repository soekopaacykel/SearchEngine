using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core;

namespace Indexer
{
    // Composite indexer database that writes each document to exactly one SQLite shard
    public class ShardedIndexerDatabase : IDatabase
    {
        private readonly List<DatabaseSqlite> _shards;

        public ShardedIndexerDatabase(IEnumerable<string> shardPaths)
        {
            var paths = shardPaths?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new List<string>();
            if (paths.Count == 0)
                throw new ArgumentException("At least one shard path must be provided");

            _shards = paths.Select(p => CreateOrResetShard(p)).ToList();
        }

        private static DatabaseSqlite CreateOrResetShard(string path)
        {
            // Ensure directory exists
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return new DatabaseSqlite(path);
        }

        // Partition function: deterministic hash of doc URL -> shard index
        private int ChooseShard(string docUrl)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in docUrl)
                {
                    hash = hash * 31 + c;
                }
                if (hash < 0) hash = -hash;
                return hash % _shards.Count;
            }
        }

        // IDatabase implementation distributes per-document
        public Dictionary<string, int> GetAllWords()
        {
            // Merge from all shards: union of words; choose smallest id if duplicates
            var res = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var shard in _shards)
            {
                var words = shard.GetAllWords();
                foreach (var kv in words)
                {
                    if (!res.TryGetValue(kv.Key, out var existing))
                        res[kv.Key] = kv.Value;
                    else if (kv.Value < existing)
                        res[kv.Key] = kv.Value;
                }
            }
            return res;
        }

        public int DocumentCounts
        {
            get { return _shards.Sum(s => s.DocumentCounts); }
        }

        private readonly Dictionary<int, int> _docToShard = new Dictionary<int, int>();

        public void InsertDocument(BEDocument doc)
        {
            var idx = ChooseShard(doc.mUrl);
            _docToShard[doc.mId] = idx;
            _shards[idx].InsertDocument(doc);
        }

        public void InsertWord(int id, string value)
        {
            // Not used directly in crawler in bulk; keep for compatibility
            foreach (var shard in _shards)
            {
                shard.InsertWord(id, value);
            }
        }

        public void InsertAllWords(Dictionary<string, int> words)
        {
            // To keep word IDs consistent, insert into all shards
            foreach (var shard in _shards)
            {
                shard.InsertAllWords(words);
            }
        }

        public void InsertAllOcc(int docId, ISet<int> wordIds)
        {
            if (_docToShard.TryGetValue(docId, out var idx))
            {
                _shards[idx].InsertAllOcc(docId, wordIds);
            }
            else
            {
                // Fallback: distribute by docId hash if mapping missing (should not happen)
                var fallbackIdx = Math.Abs(docId.GetHashCode()) % _shards.Count;
                _shards[fallbackIdx].InsertAllOcc(docId, wordIds);
            }
        }
    }
}
