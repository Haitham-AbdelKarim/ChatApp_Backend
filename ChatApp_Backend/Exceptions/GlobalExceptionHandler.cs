using ChatApp_Backend.DTO;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ChatApp_Backend.Exceptions
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext context,
            Exception exception, CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Unhandled exception occurred");

            var (statusCode, message) = exception switch
            {
                KeyNotFoundException => (404, exception.Message),
                UnauthorizedAccessException => (401, exception.Message),
                ArgumentException => (400, exception.Message),
                _ => (500, "An unexpected error occurred")
            };

            var response = ApiResponse<object>.FailResponse(message);

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength = bytes.Length;

            await context.Response.Body.WriteAsync(bytes, cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);

            return true; // ← true means exception is handled
        }
    }
}
