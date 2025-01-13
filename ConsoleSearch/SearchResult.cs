using System;
using System.Collections.Generic;
using Shared.Model;

namespace ConsoleSearch
{
    /*
     * A data class representing the result of a search.
     * Hits is the total number of documents containing at least one word from the query.
     * DocumentHits is the documents and the number of words from the query contained in the document - see
     * the class DocumentHit
     * Ignored contains words from the query not present in the document base.
     * TimeUsed is the timespan used to perform the search.
     */
    public class SearchResult
    {
        public SearchResult(String[] query, int hits, List<DocumentHit> documents, List<string> ignored, TimeSpan timeUsed)
        {
            Query = query;
            Hits = hits;
            DocumentHits = documents;
            Ignored = ignored;
            TimeUsed = timeUsed;
        }

        public String[] Query { get;  }

        public int Hits { get; }

        public List<DocumentHit> DocumentHits { get;  }

        public List<string> Ignored { get; }

        public TimeSpan TimeUsed { get;  }
    }
}
