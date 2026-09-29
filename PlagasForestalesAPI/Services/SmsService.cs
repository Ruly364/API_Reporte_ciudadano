using System.Net.Http.Headers;

namespace PlagasForestalesAPI.Services
{
    /// <summary>Entrega SMS con Twilio cuando las credenciales se configuran en producción.</summary>
    public class SmsService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _http;

        public SmsService(IConfiguration config, HttpClient http)
        {
            _config = config;
            _http = http;
        }

        public async Task<bool> EnviarCodigo(string telefono, string codigo)
        {
            var sid = _config["Sms:Twilio:AccountSid"];
            var token = _config["Sms:Twilio:AuthToken"];
            var desde = _config["Sms:Twilio:From"];
            if (string.IsNullOrWhiteSpace(sid) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(desde))
                return false;

            var credenciales = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{sid}:{token}"));
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://api.twilio.com/2010-04-01/Accounts/{sid}/Messages.json");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credenciales);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = telefono,
                ["From"] = desde,
                ["Body"] = $"Código de recuperación Reporte Ciudadano: {codigo}. Vence en 15 minutos. No lo compartas."
            });
            var respuesta = await _http.SendAsync(request);
            return respuesta.IsSuccessStatusCode;
        }
    }
}
