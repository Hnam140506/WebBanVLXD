using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace WebBanVLXD.Controllers
{
    public class ChatbotController : Controller
    {
        [HttpPost]
        public async Task<IActionResult> AskAI(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return Json(new { success = false });

            string apiKey = "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";

            using var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var request = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "Bạn là nhân viên tư vấn của BuildSmart. Trả lời lịch sự, ngắn gọn dưới 100 chữ."
                    },
                    new
                    {
                        role = "user",
                        content = message
                    }
                }
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