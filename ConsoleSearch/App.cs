using System;

namespace ConsoleSearch
{
    public class App
    {

        public void Run()
        {
            IDatabase db = GetDatabase();
            SearchLogic mSearchLogic = new SearchLogic(db);
            Console.WriteLine("Console Search");

            while (true)
            {
                Console.WriteLine("enter search terms or command - q for quit");
                string input = Console.ReadLine();
                if (input.Equals("q", StringComparison.OrdinalIgnoreCase))
                    break;

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                // håndter case sensitivity toggle:
                if (input.StartsWith("/casesensitive=", StringComparison.OrdinalIgnoreCase))
                {
                    var setting = input.Substring("/casesensitive=".Length).Trim().ToLowerInvariant();
                    if (setting == "on")
                        mSearchLogic.SetCaseSensitivity(true);
                    else if (setting == "off")
                        mSearchLogic.SetCaseSensitivity(false);
                    else
                        Console.WriteLine("Unknown setting. Use '/casesensitive=on' or '/casesensitive=off'.");
                    continue; // spring søgning over, da det var en kommando
                }

                var query = input.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                var result = mSearchLogic.Search(query, 10);

                if (result.Ignored.Count > 0)
                {
                    Console.WriteLine($"Ignored: {string.Join(',', result.Ignored)}");
                }

                int idx = 1;
                foreach (var doc in result.DocumentHits)
                {
                    Console.WriteLine($"{idx} : {doc.Document.mUrl} -- contains {doc.NoOfHits} search terms");
                    Console.WriteLine("Index time: " + doc.Document.mIdxTime);
                    Console.WriteLine($"Missing: {ArrayAsString(doc.Missing.ToArray())}");
                    idx++;
                }
                Console.WriteLine("Documents: " + result.Hits + ". Time: " + result.TimeUsed.TotalMilliseconds);
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

        string ArrayAsString(string[] s) => s.Length == 0 ? "[]" : $"[{String.Join(',', s)}]";
    }
}
