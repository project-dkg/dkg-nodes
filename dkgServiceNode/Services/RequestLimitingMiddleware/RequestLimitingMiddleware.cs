// RequestLimitingMiddleware.cs
public class RequestLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SemaphoreSlim _semaphore;

    public RequestLimitingMiddleware(RequestDelegate next, int maxConcurrentRequests)
    {
        _next = next;
        _semaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments($"/api/ops", StringComparison.OrdinalIgnoreCase))
        {
            await _semaphore.WaitAsync();

            try
            {
                await _next(context);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        else
        {
            // If not the specific controller, just pass through
            await _next(context);
        }
    }
}