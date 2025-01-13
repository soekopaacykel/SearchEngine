using System;
using System.Collections.Generic;
using System.IO;
using Shared.Model;

namespace Indexer
{
    public class Crawler
    {
        private readonly char[] separators = " \\\n\t\"$'!,?;.:-_**+=)([]{}<>/@&%€#".ToCharArray();
        /* Will be used to spilt text into words. So a word is a maximal sequence of
         * chars that does not contain any char from separators */

        private Dictionary<string, int> words = new Dictionary<string, int>();
        /* Will contain all words from files during indexing - thet key is the 
         * value of the word and the value is its id in the database */

        private int documentCounter = 0;
        /* Will count the number of documents indexed during indexing */

        IDatabase mdatabase;

        public Crawler(IDatabase db){ mdatabase = db; }

        //Return a dictionary containing all words (as the key)in the file
        // [f] and the value is the number of occurrences of the key in file.
        private ISet<string> ExtractWordsInFile(FileInfo f)
        {
            ISet<string> res = new HashSet<string>();
            var content = File.ReadAllLines(f.FullName);
            foreach (var line in content)
            {
                foreach (var aWord in line.Split(separators, StringSplitOptions.RemoveEmptyEntries))
                {
                    res.Add(aWord);
                }
            }

            return res;
        }

        private ISet<int> GetWordIdFromWords(ISet<string> src) {
            ISet<int> res = new HashSet<int>();

            foreach ( var p in src)
            {
                res.Add(words[p]);
            }
            return res;
        }

        // Return a dictionary of all the words (the key) in the files contained
        // in the directory [dir]. Only files with an extension in
        // [extensions] is read. The value part of the return value is
        // the number of occurrences of the key.
        public void IndexFilesIn(DirectoryInfo dir, List<string> extensions) {
            
            Console.WriteLine($"Crawling {dir.FullName}");

            foreach (var file in dir.EnumerateFiles())
                if (extensions.Contains(file.Extension))
                {
                    documentCounter++;
                    BEDocument newDoc = new BEDocument{
                        mId = documentCounter,
                        mUrl = file.FullName,
                        mIdxTime = DateTime.Now.ToString(),
                        mCreationTime = file.CreationTime.ToString()
                    };
                    
                    mdatabase.InsertDocument(newDoc);
                    Dictionary<string, int> newWords = new Dictionary<string, int>();
                    ISet<string> wordsInFile = ExtractWordsInFile(file);
                    foreach (var aWord in wordsInFile) {
                        if (!words.ContainsKey(aWord)) {
                            words.Add(aWord, words.Count + 1);
                            newWords.Add(aWord, words[aWord]);
                        }
                    }
                    mdatabase.InsertAllWords(newWords);

                    mdatabase.InsertAllOcc(newDoc.mId, GetWordIdFromWords(wordsInFile));


                }
            foreach (var d in dir.EnumerateDirectories())
                IndexFilesIn(d, extensions);
        }

        
    }
}
