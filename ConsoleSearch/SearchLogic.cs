using System;
using System.Collections.Generic;
using Shared.Model;

namespace ConsoleSearch
{
    public class SearchLogic
    {
        private readonly IDatabase mDatabase;
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
        /// Udfører søgningen baseret på query-ordene.
        /// </summary>
        public SearchResult Search(string[] query, int maxAmount)
        {
            List<string> ignored;
            DateTime start = DateTime.Now;

            // Hvis case sensitivity er slået fra → konverter alle ord til lowercase
            if (!mCaseSensitive)
            {
                for (int i = 0; i < query.Length; i++)
                {
                    query[i] = query[i].ToLowerInvariant();
                }
            }

            // Konverter ord til wordIds
            var wordIds = mDatabase.GetWordIds(query, out ignored);

            if (wordIds.Count == 0) // Ingen kendte ord
            {
                return new SearchResult(query, 0, new List<DocumentHit>(), ignored, DateTime.Now - start);
            }

            // Hent dokumenter
            var docIds = mDatabase.GetDocuments(wordIds);

            // Tag de første maxAmount
            var top = new List<int>();
            foreach (var p in docIds.GetRange(0, Math.Min(maxAmount, docIds.Count)))
                top.Add(p.Key);

            // Sammensæt resultaterne
            List<DocumentHit> docresult = new List<DocumentHit>();
            int idx = 0;
            foreach (var docId in top)
            {
                BEDocument doc = mDatabase.GetDocDetails(docId);
                var missing = mDatabase.WordsFromIds(mDatabase.getMissing(doc.mId, wordIds));
                missing.AddRange(ignored);
                docresult.Add(new DocumentHit(doc, docIds[idx++].Value, missing));
            }

            return new SearchResult(query, docIds.Count, docresult, ignored, DateTime.Now - start);
        }
    }
}
