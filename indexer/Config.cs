using System;

namespace Indexer;

public class Config
{
    // the folder to be indexed - all .txt files in that folder (and subfolders)
    // will be indexed
    public static string FOLDER 
    {
        get
        {
            // Check if running in Kubernetes
            string kubernetesServiceHost = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
            
            if (!string.IsNullOrEmpty(kubernetesServiceHost))
            {
                // Running in Kubernetes - use large directory
                string indexPath = Environment.GetEnvironmentVariable("INDEX_PATH") ?? "/app/data/large";
                return indexPath;
            }
            
            // Local development path
            return @"/Users/ameliavalentin/Desktop/Skole/Arkitekturprincipper i praksis/DB/medium";
        }
    }
}