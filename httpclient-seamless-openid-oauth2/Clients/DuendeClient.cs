using System.Net.Http;
using System.Threading.Tasks;

namespace httpclient_seamless_openid_oauth2.Clients
{
    public class DuendeClient : IDuendeClient
    {
        private readonly HttpClient _httpClient;

        public DuendeClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public Task<HttpResponseMessage> GetTest()
        {
            return _httpClient.GetAsync("test");
        }
    }
}