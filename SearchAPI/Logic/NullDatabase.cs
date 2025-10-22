using System.Collections.Generic;
using Core;

namespace SearchAPI.Logic
{
    // A no-op database used when real DB cannot be initialized. Returns empty results and basic fallbacks.
    public class NullDatabase : IDatabase
    {
        public List<int> GetWordIds(string[] query, out List<string> outIgnored)
        {
            // All terms are ignored since we have no dictionary
            outIgnored = new List<string>(query);
            return new List<int>();
        }

        public BEDocument GetDocDetails(int docId)
        {
            return null;
        }

        public List<KeyValuePair<int, int>> GetDocuments(List<int> wordIds)
        {
            return new List<KeyValuePair<int, int>>();
        }

        public List<int> getMissing(int docId, List<int> wordIds)
        {
            // Consider everything missing when no DB
            return new List<int>(wordIds);
        }

        public List<string> WordsFromIds(List<int> wordIds)
        {
            return new List<string>();
        }
    }
}
