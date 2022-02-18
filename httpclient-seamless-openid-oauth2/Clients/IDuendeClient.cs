using System.Net.Http;
using System.Threading.Tasks;

namespace httpclient_seamless_openid_oauth2.Clients
{
    public interface IDuendeClient
    {
        public Task<HttpResponseMessage> GetTest();
    }
}