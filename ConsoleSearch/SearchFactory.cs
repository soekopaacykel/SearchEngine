namespace ConsoleSearch;

public class SearchFactory
{
    public static ISearchLogic GetSearchLogic(IDatabase db)
    {
        return new SearchLogic(db);
    }
}