namespace Core;

public class Paths
{
    public static readonly string SQLITE_DATABASE =
        @"/Users/ameliavalentin/Desktop/Skole/Arkitekturprincipper i praksis/DB/DB.db";

    // Sharded databases - each handles a specific data type
    public static readonly string POSTGRES_WORDS_DATABASE =
        "Server=postgres-words;Port=5432;User Id=ameliavalentin;Password=DBkonger1234;Database=search_words;";
    public static readonly string POSTGRES_DOCUMENTS_DATABASE =
        "Server=postgres-documents;Port=5432;User Id=ameliavalentin;Password=DBkonger1234;Database=search_documents;";
    public static readonly string POSTGRES_OCCURRENCES_DATABASE =
        "Server=postgres-occurrences;Port=5432;User Id=ameliavalentin;Password=DBkonger1234;Database=search_occurrences;";

    // Legacy connection string for backward compatibility
    [System.Obsolete("Use specific sharded database connections instead")]
    public static readonly string POSTGRES_DATABASE =
        "Server=postgres;Port=5432;User Id=ameliavalentin;Password=DBkonger1234;Database=search;";
}