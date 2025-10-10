using System;
using System.Collections.Generic;
using System.Linq;
using Core;

namespace SearchAPI.Logic
{
    // A composite IDatabase that fans out queries to multiple SQLite databases (shards)
    // and merges the results. This enables Y-scaling without changing the indexer.
    public class ShardedDatabase : IDatabase
    {
        private readonly List<IDatabase> _shards;
        private readonly List<string> _labels;
        private Dictionary<int, string> _lastDocShardMap = new Dictionary<int, string>();

        public IReadOnlyDictionary<int, string> LastDocShardMap => _lastDocShardMap;
        public IReadOnlyList<string> Labels => _labels;

        public ShardedDatabase(IEnumerable<IDatabase> shards)
        {
            _shards = shards.ToList();
            _labels = Enumerable.Range(0, _shards.Count).Select(i => $"shard-{i}").ToList();
            if (_shards.Count == 0)
                throw new ArgumentException("At least one shard must be provided");
        }

        public ShardedDatabase(IEnumerable<(string label, IDatabase db)> labeledShards)
        {
            var list = labeledShards.ToList();
            _shards = list.Select(t => t.db).ToList();
            _labels = list.Select(t => string.IsNullOrWhiteSpace(t.label) ? "shard" : t.label).ToList();
            if (_shards.Count == 0)
                throw new ArgumentException("At least one shard must be provided");
        }

        public List<int> GetWordIds(string[] query, out List<string> outIgnored)
        {
            // Strategy: Use first shard as the dictionary authority. Assumes identical schema and dictionary across shards.
            // Alternatively, we could union word dictionaries across shards.
            var ids = _shards[0].GetWordIds(query, out outIgnored);
            return ids;
        }

        public BEDocument GetDocDetails(int docId)
        {
            // Try each shard until found
            foreach (var shard in _shards)
            {
                var doc = shard.GetDocDetails(docId);
                if (doc != null) return doc;
            }
            return null;
        }

        public List<KeyValuePair<int, int>> GetDocuments(List<int> wordIds)
        {
            // Fan out to all shards, aggregate by docId and sum counts
            _lastDocShardMap = new Dictionary<int, string>();
            var agg = new Dictionary<int, int>();
            for (int i = 0; i < _shards.Count; i++)
            {
                var shard = _shards[i];
                var label = _labels[i];
                try
                {
                    var docs = shard.GetDocuments(wordIds);
                    foreach (var kv in docs)
                    {
                        agg.TryGetValue(kv.Key, out var existing);
                        agg[kv.Key] = existing + kv.Value;
                        // remember the shard that reported this doc first (arbitrary if appears in multiple)
                        if (!_lastDocShardMap.ContainsKey(kv.Key))
                            _lastDocShardMap[kv.Key] = label;
                    }
                }
                catch
                {
                    // Best-effort: ignore shard failure for now; could log or return partial info.
                }
            }
            // Order by count desc similar to single-shard behavior
            var ordered = agg.OrderByDescending(p => p.Value)
                             .Select(p => new KeyValuePair<int, int>(p.Key, p.Value))
                             .ToList();
            return ordered;
        }

        public List<int> getMissing(int docId, List<int> wordIds)
        {
            // Find which shard has the document and delegate
            foreach (var shard in _shards)
            {
                var doc = shard.GetDocDetails(docId);
                if (doc != null)
                    return shard.getMissing(docId, wordIds);
            }
            return new List<int>();
        }

        public List<string> WordsFromIds(List<int> wordIds)
        {
            // Use first shard as the dictionary authority
            return _shards[0].WordsFromIds(wordIds);
        }
    }
}
