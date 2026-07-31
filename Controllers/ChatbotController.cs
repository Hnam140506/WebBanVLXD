using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace WebBanVLXD.Controllers
{
    public class ChatbotController : Controller
    {
        private readonly IConfiguration _configuration;

        public ChatbotController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> AskAI(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return Json(new { success = false });

            string apiKey = _configuration["Groq:ApiKey"];

            using var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var request = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "Bạn là nhân viên tư vấn bán vật liệu xây dựng của BuildSmart. Hãy trả lời ngắn gọn, lịch sự, dưới 100 chữ."
                    },
                    new
                    {
                        role = "user",
                        content = message
                    }
                },
                temperature = 0.7,
                max_tokens = 200
            };

            var json = JsonConvert.SerializeObject(request);

            var content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.PostAsync(
                "https://api.groq.com/openai/v1/chat/completions",
                content
            );

            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return Json(new
                {
                    success = false,
                    reply = result
                });
            }

            dynamic data = JsonConvert.DeserializeObject(result);

            return Json(new
            {
                success = true,
                reply = (string)data.choices[0].message.content
            });
        }
    }
}