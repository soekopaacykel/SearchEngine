using System;

namespace ConsoleSearch;

public interface ISearchLogic
{
    SearchResult Search(String[] query, int maxAmount);
}