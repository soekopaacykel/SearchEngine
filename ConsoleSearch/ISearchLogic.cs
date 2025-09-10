using System;
using System.Threading.Tasks;
using Core;

namespace ConsoleSearch;

public interface ISearchLogic
{
    Task<SearchResult> Search(String[] query, int maxAmount);
}