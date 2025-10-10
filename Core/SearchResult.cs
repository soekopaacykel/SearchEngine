using System;
using System.Collections.Generic;

namespace Core;

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
    public string[] Query { get; set; }

    public int Hits { get; set; }

    public List<DocumentHit> DocumentHits { get; set; }

    public List<string> Ignored { get; set; }

    public TimeSpan TimeUsed { get; set; }

    // Diagnostics: which API instance handled and how DB was used
    public string ApiInstance { get; set; }
    public string DatabaseMode { get; set; } // "Single" or "Sharded"
    public List<string> ShardsUsed { get; set; } // paths or labels
    public Dictionary<int, string> DocShard { get; set; } // docId -> shard label/path
}