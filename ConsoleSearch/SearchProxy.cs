using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Core;

namespace ConsoleSearch
{
    public class SearchProxy : ISearchLogic
    {
        private string serverEndPoint = "http://localhost:5154/api/search/";

        private HttpClient mHttp;

        public SearchProxy()
        {
            mHttp = new HttpClient();
        }

        public async Task<SearchResult> Search(string[] query, int maxAmount)
        {
            var completeUrl = $"{serverEndPoint}{String.Join(",", query)}/{maxAmount}";
            return await mHttp.GetFromJsonAsync<SearchResult>(completeUrl);
        }
    }
}