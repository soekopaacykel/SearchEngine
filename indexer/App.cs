using System;
using System.Collections.Generic;
using System.IO;
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

            // Check if running in Kubernetes
            string kubernetesServiceHost = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
            
            if (!string.IsNullOrEmpty(kubernetesServiceHost))
            {
                // Non-interactive mode - show top 10 words
                int topWords = 10;
                var sorted = all.OrderByDescending(p => p.Value);
                Console.WriteLine($"The top {topWords} words are:");
                int shown = 0;
                foreach (var p in sorted)
                {
                    Console.WriteLine($"<{p.Key}> - {p.Value}");
                    shown++;
                    if (shown >= topWords) break;
                }
                return; // Exit early in non-interactive mode
            }

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
            // Check if running in Kubernetes (environment variable is typically set)
            string kubernetesServiceHost = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
            
            if (!string.IsNullOrEmpty(kubernetesServiceHost))
            {
                // Check if we're in the mono environment (single database setup)
                string databaseType = Environment.GetEnvironmentVariable("DATABASE_TYPE");
                string kubernetesNamespace = Environment.GetEnvironmentVariable("KUBERNETES_NAMESPACE") ?? "default";
                
                Console.WriteLine($"Database type environment variable: '{databaseType}'");
                Console.WriteLine($"Kubernetes namespace: '{kubernetesNamespace}'");
                
                // Auto-detect based on namespace if DATABASE_TYPE is not set
                if (string.IsNullOrEmpty(databaseType))
                {
                    if (kubernetesNamespace.Contains("mono"))
                    {
                        databaseType = "mono";
                    }
                    else if (kubernetesNamespace.Contains("searchengine"))
                    {
                        databaseType = "sharded";
                    }
                }
                
                if (string.Equals(databaseType, "mono", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Running in Kubernetes mono environment - using PostgreSQL mono database");
                    return new DatabasePostgresMono();
                }
                else
                {
                    Console.WriteLine("Running in Kubernetes sharded environment - using PostgreSQL sharded database");
                    return new DatabasePostgres();
                }
            }
            
            // Interactive mode for local development
            Console.Write("Use SQLite (1) or Postgres (2) database?");
            string input = Console.ReadLine();
            if (input?.Equals("1") == true)
                return new DatabaseSqlite();
            else if (input?.Equals("2") == true)
                return new DatabasePostgres();
            Console.WriteLine("Wrong input - try again...");
            return GetDatabase();
        }
    }
}
