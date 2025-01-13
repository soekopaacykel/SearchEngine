using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Indexer
{
    class Program
    {
        static void Main(string[] args)
        {
            
            new App().Run();

            //new Renamer().Crawl(new DirectoryInfo(@"/Users/ole/data"));


        }

        
        
    }
}