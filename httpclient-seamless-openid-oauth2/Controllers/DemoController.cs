using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace httpclient_seamless_openid_oauth2.Controllers
{
    [Route("[controller]")]
    public class DemoController : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public DemoController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("demo");
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var response = await _httpClient.GetAsync("test");
            var responseContent = response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest(responseContent);
            }

            return Ok(responseContent);
        }
    }
}