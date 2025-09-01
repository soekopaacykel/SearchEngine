using System;
using System.Collections.Generic;
using System.IO;
using Shared;
using System.Linq;


namespace Indexer
{
    public class App
    {
        public void Run()
        {
            IDatabase db = GetDatabase();
            Crawler crawler = new Crawler(db);

            var root = new DirectoryInfo(Config.FOLDER);

            DateTime start = DateTime.Now;

            crawler.IndexFilesIn(root, new List<string> { ".txt" });

            TimeSpan used = DateTime.Now - start;
            Console.WriteLine("DONE! used " + used.TotalMilliseconds);

            var all = db.GetAllWords();

            Console.WriteLine($"Indexed {db.DocumentCounts} documents");
            Console.WriteLine($"Number of different words: {all.Count}");

            // spørger til hvor mange "top hits" vi vil have ud:
            long totalOccurrences = 0;
            foreach (var p in all)
            {
                totalOccurrences += p.Value; // summerer alle forekomster
            }
            Console.WriteLine($"Total number of word occurrences: {totalOccurrences}");

            Console.Write("How many top words would you like to see? ");
            if (int.TryParse(Console.ReadLine(), out int topN))
            {
                // Sorter ordene efter hyppighed, faldende
                var sorted = all.OrderByDescending(p => p.Value);

                Console.WriteLine($"The top {topN} words are:");
                int shown = 0;
                foreach (var p in sorted)
                {
                    Console.WriteLine($"<{p.Key}> - {p.Value}");
                    shown++;
                    if (shown >= topN) break;
                }
            }
            else
            {
                Console.WriteLine("Invalid number entered.");
            }
        }
        

        private IDatabase GetDatabase()
        {
            Console.Write("Use SQLite (1) or Postgres (2) database?");
            string input = Console.ReadLine();
            if (input.Equals("1"))
                return new DatabaseSqlite();
            else if (input.Equals("2"))
                return new DatabasePostgres();
            Console.WriteLine("Wrong input - try again...");
            return GetDatabase();
        }
    }
}
