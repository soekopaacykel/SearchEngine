﻿using System.Collections.Generic;

namespace Core
{
    public interface IDatabase
    {
        /// <summary>
        /// Get id's for words in [query]. [outIgnored] contains those word from query that is
        /// not present in any document.
        /// </summary>
        List<int> GetWordIds(string[] query, out List<string> outIgnored);

        /// <summary>
        /// Get document by its id
        /// </summary>
        BEDocument GetDocDetails(int docId);

        /// <summary>
        /// Perform the essential search for documents. It will return
        /// a list of KeyValuePairs - the key is the id of the
        /// document, and value is the number of words from the query
        /// contained in the document.
        /// </summary>
        List<KeyValuePair<int, int>> GetDocuments(List<int> wordIds);

        /// <summary>
        /// Return all id of words, contained in [wordIds], but not
        /// present in the document with id [docId]
        /// </summary>
        List<int> getMissing(int docId, List<int> wordIds);

        /// <summary>
        /// Convert a list of word id's to a list of the value of the
        /// words
        /// </summary>
        List<string> WordsFromIds(List<int> wordIds);
    }
}