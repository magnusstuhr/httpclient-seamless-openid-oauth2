using System.Threading.Tasks;
using httpclient_seamless_openid_oauth2.Clients;
using Microsoft.AspNetCore.Mvc;

namespace httpclient_seamless_openid_oauth2.Controllers
{
    [Route("[controller]")]
    public class DemoController : ControllerBase
    {
        private readonly IDuendeClient _duendeClient;

        public DemoController(IDuendeClient duendeClient)
        {
            _duendeClient = duendeClient;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var response = await _duendeClient.GetTest();
            var responseContent = response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest(responseContent);
            }

            return Ok(responseContent);
        }
    }
}