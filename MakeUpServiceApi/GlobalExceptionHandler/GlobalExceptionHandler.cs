using Microsoft.AspNetCore.Diagnostics;

namespace MakeUpServiceApi.GlobalExceptionHandler
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpcontext,
                Exception exception,
                CancellationToken cancellationToken
            )
        {
            _logger.LogError(exception, "An unhandled exception occurred while processing the request.");

            httpcontext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            httpcontext.Response.ContentType = "application/json";
            var response = new
            {
                error = "Internal Server Error",
                message = $"{exception.Message}"
            };
            await httpcontext.Response.WriteAsJsonAsync(response, cancellationToken);
            return true;
        }
    }
}
