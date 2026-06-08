using Microsoft.Extensions.Options;
using Spectrum.API.Configuration;
using Spectrum.API.Exceptions;
using Spectrum.API.Utilities;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace Spectrum.API.Services.Email
{
    public class SmtpEmailService : IEmailService
    {
        private readonly SmtpOptions _options;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IOptions<SmtpOptions> options, ILogger<SmtpEmailService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public Task SendRegistrationVerificationAsync(string email, string code)
        {
            return SendCodeAsync(
                email,
                "Verifica tu cuenta de Spectrum",
                "Verificación de cuenta",
                "Usa este código para activar tu cuenta en Spectrum:",
                code
            );
        }

        public Task SendPasswordResetAsync(string email, string code)
        {
            return SendCodeAsync(
                email,
                "Recupera tu contraseña de Spectrum",
                "Recuperación de contraseña",
                "Usa este código para continuar con la recuperación de tu contraseña:",
                code
            );
        }

        public Task SendPasswordChangeAsync(string email, string code)
        {
            return SendCodeAsync(
                email,
                "Confirma tu cambio de contraseña",
                "Cambio de contraseña",
                "Usa este código para confirmar el cambio de contraseña de tu cuenta:",
                code
            );
        }

        public Task SendRewardAsync(string email, string eventTitle, string rewardCode)
        {
            return SendGiveawayWinnerEmailAsync(email, eventTitle, rewardCode);
        }

        public Task SendGiveawayWinnerEmailAsync(string email, string eventTitle, string rewardCode)
        {
            return SendCustomAsync(
                email,
                "Ganaste una recompensa de SpectrumApp",
                "HAS SIDO EL GANADOR",
                $"Felicidades. Obtuviste una recompensa de SpectrumApp en el sorteo {eventTitle}. Tu codigo ganado es:",
                rewardCode,
                "Este codigo fue asignado automaticamente al reclamar la recompensa. No lo compartas."
            );
        }

        public Task SendReviewDeletedAsync(string email, string reviewTitle)
        {
            return SendNoticeAsync(
                email,
                "Tu reseña fue retirada de Spectrum",
                "Reseña retirada",
                $"Un administrador retiró tu reseña \"{reviewTitle}\". Si crees que fue un error, contacta a soporte."
            );
        }

        public Task SendClipDeletedAsync(string email, string clipTitle)
        {
            return SendNoticeAsync(
                email,
                "Tu clip fue retirado de Spectrum",
                "Clip retirado",
                $"Un administrador retiró tu clip \"{clipTitle}\". Si crees que fue un error, contacta a soporte."
            );
        }

        public Task SendAccountSuspendedAsync(string email)
        {
            return SendNoticeAsync(
                email,
                "Tu cuenta de Spectrum fue suspendida",
                "Cuenta suspendida",
                "Tu cuenta está suspendida temporalmente. Contacta a soporte si crees que es un error."
            );
        }

        public Task SendAccountBannedAsync(string email)
        {
            return SendNoticeAsync(
                email,
                "Tu cuenta de Spectrum fue desactivada",
                "Cuenta desactivada",
                "Un administrador desactivó tu cuenta por incumplimiento de las reglas de la comunidad."
            );
        }

        public Task SendAccountReactivatedAsync(string email)
        {
            return SendNoticeAsync(
                email,
                "Tu cuenta de Spectrum fue reactivada",
                "Cuenta reactivada",
                "Tu cuenta fue reactivada y ya puedes volver a usar Spectrum."
            );
        }

        public Task SendReportActionAsync(string email, string actionSummary)
        {
            return SendNoticeAsync(
                email,
                "Actualización sobre un reporte en Spectrum",
                "Reporte revisado",
                string.IsNullOrWhiteSpace(actionSummary)
                    ? "Un administrador revisó un reporte relacionado con tu cuenta o contenido."
                    : actionSummary
            );
        }

        private Task SendCodeAsync(string email, string subject, string title, string intro, string code)
        {
            return SendCustomAsync(
                email,
                subject,
                title,
                intro,
                code,
                "Este código expira en 10 minutos. Si no solicitaste este correo, puedes ignorarlo."
            );
        }

        private Task SendNoticeAsync(string email, string subject, string title, string body)
        {
            return SendCustomAsync(email, subject, title, body, string.Empty, "Gracias por ayudar a mantener segura la comunidad de Spectrum.");
        }

        private async Task SendCustomAsync(string email, string subject, string title, string intro, string code, string footer)
        {
            EnsureConfigured();

            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromEmail, _options.FromName),
                Subject = subject,
                Body = BuildHtmlTemplate(title, intro, code, footer),
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };
            message.To.Add(email);

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.UseTls,
                Credentials = new NetworkCredential(_options.Username, _options.Password)
            };

            try
            {
                await client.SendMailAsync(message);
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, "SMTP delivery failed for purpose {Subject}", subject);
                throw new SpectrumServiceUnavailableException("emailDeliveryFailed");
            }
        }

        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(_options.Host) ||
                string.IsNullOrWhiteSpace(_options.Username) ||
                string.IsNullOrWhiteSpace(_options.Password) ||
                string.IsNullOrWhiteSpace(_options.FromEmail))
            {
                throw new SpectrumServiceUnavailableException(Constants.ErrorMessages.SmtpConfigurationInvalid);
            }
        }

        private static string BuildHtmlTemplate(string title, string intro, string code, string footer)
        {
            var encodedCode = string.IsNullOrWhiteSpace(code)
                ? string.Empty
                : $"""<p style="font-size:28px;letter-spacing:6px;font-weight:700;color:#a987ff;margin:24px 0;">{WebUtility.HtmlEncode(code)}</p>""";

            return $"""
                <!doctype html>
                <html lang="es">
                <body style="margin:0;padding:0;background:#111;color:#fff;font-family:Arial,sans-serif;">
                  <div style="max-width:560px;margin:0 auto;padding:32px;">
                    <h1 style="color:#ffffff;margin:0 0 16px;">Spectrum</h1>
                    <div style="background:#1b1b1f;border:1px solid #3b2d68;border-radius:8px;padding:24px;">
                      <h2 style="margin:0 0 12px;color:#ffffff;">{WebUtility.HtmlEncode(title)}</h2>
                      <p style="line-height:1.5;color:#d8d8e2;">{WebUtility.HtmlEncode(intro)}</p>
                      {encodedCode}
                      <p style="line-height:1.5;color:#aaa;">{WebUtility.HtmlEncode(footer)}</p>
                    </div>
                  </div>
                </body>
                </html>
                """;
        }
    }
}
