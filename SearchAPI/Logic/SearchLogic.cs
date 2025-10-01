using System;
using System.Collections.Generic;
using Core;

namespace SearchAPI.Logic
{
    public class SearchLogic //: ISearchLogic
    {
        IDatabase mDatabase;
        private bool mCaseSensitive = true; // standard er case sensitive (som nu)

        public SearchLogic(IDatabase database)
        {
            mDatabase = database;
        }

        /// <summary>
        /// Sætter om søgning skal være case sensitive eller ej.
        /// </summary>
        public void SetCaseSensitivity(bool enabled)
        {
            mCaseSensitive = enabled;
            Console.WriteLine("Case sensitivity is now " + (enabled ? "ON" : "OFF"));
        }

        /// <summary>
        /// Returnerer om søgning er case sensitive.
        /// </summary>
        public bool IsCaseSensitive()
        {
            return mCaseSensitive;
        }

        /* Perform search of documents containing words from query. The result will
         * contain details about amost maxAmount of documents.
         */
        public SearchResult Search(String[] query, int maxAmount)
        {
            List<string> ignored;

            DateTime start = DateTime.Now;

            // Database only contains lowercase words, so we always need to convert to lowercase for database lookup
            // Case sensitivity will affect how we present results, not the database query
            string[] processedQuery = new string[query.Length];
            for (int i = 0; i < query.Length; i++)
            {
                processedQuery[i] = query[i].ToLowerInvariant();
            }

            // Convert words to wordids
            var wordIds = mDatabase.GetWordIds(processedQuery, out ignored);

            if (wordIds.Count == 0) // no words know in index
                 return new SearchResult{
                     Query = query, 
                     Hits = 0, 
                     DocumentHits = new List<DocumentHit>(), 
                     Ignored = ignored, 
                     TimeUsed = DateTime.Now - start};
            // perform the search - get all docIds
            var docIds =  mDatabase.GetDocuments(wordIds);

            // get ids for the first maxAmount             
            var top = new List<int>();
            foreach (var p in docIds.GetRange(0, Math.Min(maxAmount, docIds.Count)))
                top.Add(p.Key);

            // compose the result.
            // all the documentHit
            List<DocumentHit> docresult = new List<DocumentHit>();
            int idx = 0;
            foreach (var docId in top)
            {
                BEDocument doc = mDatabase.GetDocDetails(docId);
                var missing = mDatabase.WordsFromIds(mDatabase.getMissing(doc.mId, wordIds));
                missing.AddRange(ignored);
                docresult.Add(new DocumentHit
                {
                    Document = doc, 
                    NoOfHits = docIds[idx++].Value,
                    Missing = missing
                });
            }

            return new SearchResult{
                Query = query, 
                Hits = docIds.Count, 
                DocumentHits = docresult, 
                Ignored = ignored, 
                TimeUsed = DateTime.Now - start};
        }
    }
}
