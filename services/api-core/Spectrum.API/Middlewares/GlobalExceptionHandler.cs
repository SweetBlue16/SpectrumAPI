using Microsoft.AspNetCore.Diagnostics;
using Spectrum.API.Exceptions;
using System.Net;

namespace Spectrum.API.Middlewares
{
    /// <summary>
    /// Centralized middleware for catching and processing all unhandled exceptions.
    /// Formats error responses into a standardized ProblemDetails JSON.
    /// </summary>
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        /// <summary>
        /// Initializes the handler with logging capabilities.
        /// </summary>
        /// <param name="logger">Logger instance to record error details.</param>
        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Handles the exception by determining the appropriate HTTP status code and response body.
        /// </summary>
        /// <param name="httpContext">Current HTTP context.</param>
        /// <param name="exception">The intercepted exception.</param>
        /// <param name="cancellationToken">Operation cancellation token.</param>
        /// <returns>True if the exception was handled, false otherwise.</returns>
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var response = new
            {
                Error = exception is SpectrumBusinessException ||
                        exception is SpectrumNotFoundException ||
                        exception is SpectrumUnauthorizedException ||
                        exception is SpectrumServiceUnavailableException
                        ? exception.Message
                        : "An internal server error has occurred."
            };

            httpContext.Response.StatusCode = exception switch
            {
                SpectrumBusinessException => (int)HttpStatusCode.BadRequest,
                SpectrumUnauthorizedException => (int)HttpStatusCode.Unauthorized,
                SpectrumNotFoundException => (int)HttpStatusCode.NotFound,
                SpectrumServiceUnavailableException => (int)HttpStatusCode.ServiceUnavailable,
                _ => (int)HttpStatusCode.InternalServerError
            };

            if (httpContext.Response.StatusCode == (int)HttpStatusCode.InternalServerError)
            {
                _logger.LogError(exception, "Unhandled critical system error.");
            }
            else
            {
                _logger.LogWarning("Violation of a business rule: {Message}", exception.Message);
            }

            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

            return true;
        }
    }
}
