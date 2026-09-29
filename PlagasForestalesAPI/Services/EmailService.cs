using System.Net;
using System.Net.Mail;
using System.IO;

namespace PlagasForestalesAPI.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public string CargarPlantillaCorreo()

        {
            var ruta = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Templates",
                "reporte_email.html"
            );

            // VALIDACIÓN
            if (!File.Exists(ruta))
            {
                throw new Exception(
                    $"No existe la plantilla HTML del correo en la ruta: {ruta}"
                );
            }

            return File.ReadAllText(ruta);
        }

        public async Task EnviarCorreo(string destino, string asunto, string html, bool incluirSeguimiento = true)
        {
            var servidor = _config["CorreoInstitucional:Servidor"];
            var puerto = int.Parse(_config["CorreoInstitucional:Puerto"]);
            var usarSSL = bool.Parse(_config["CorreoInstitucional:UsarSSL"]);            
            var fromEmail = _config["CorreoInstitucional:FromEmail"];
            var fromName = _config["CorreoInstitucional:FromName"];

            using var client = new SmtpClient(servidor, puerto)
            {                
                EnableSsl = usarSSL,   // false 
                UseDefaultCredentials = false,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            var mail = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = asunto,
                Body = html,
                IsBodyHtml = true
            };

            mail.To.Add(destino);
            //await client.SendMailAsync(mail);  //se comentó esta parte y se agregó lo de abajo para enviar copias de correos

            var correosSeguimiento =
                _config.GetSection("CorreosSeguimiento").Get<List<string>>();

            if (incluirSeguimiento && correosSeguimiento != null)
            {
                foreach (var correo in correosSeguimiento)
                {
                    mail.CC.Add(correo);
                }
            }

            await client.SendMailAsync(mail);

        }
        


      //  {
     //       var ruta = Path.Combine(
     //           Directory.GetCurrentDirectory(),
     //           "Templates",
     //           "reporte_email.html"
     //       );
     //
     //       return File.ReadAllText(ruta);
     //   }

    }
}
