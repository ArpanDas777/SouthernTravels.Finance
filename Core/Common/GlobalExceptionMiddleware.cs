namespace SouthernTravels.Core.Common;

/// <summary>
/// Logs unhandled exceptions with a trace id (as in STPLCostSheet). AJAX/JSON calls get a JSON error body;
/// normal page requests are re-thrown so the error page (UseExceptionHandler / developer page) still shows.
/// </summary>
public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;
            logger.LogError(ex, "Unhandled exception. TraceId: {TraceId}", traceId);

            var wantsJson = context.Request.Headers.XRequestedWith == "XMLHttpRequest"
                || context.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
            if (!wantsJson || context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Something went wrong. Please contact support.", traceId });
        }
    }
}
