namespace ProductCatalog.Middleware
{
    public sealed class ApiKeyMiddleware
    {
        private const string ApiKeyHeaderName = "X-API-Key";

        private const string DefaultApiKey = "dev-secret";

        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only protect /api/* endpoints.
            if (!context.Request.Path
                    .StartsWithSegments("/api"))
            {
                await _next(context);

                return;
            }

            var configuredApiKey = Environment.GetEnvironmentVariable("API_KEY") ?? _configuration["API_KEY"] ?? DefaultApiKey;

            if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedApiKey))
            {
                await WriteUnauthorizedResponse(context);

                return;
            }

            if (!string.Equals(providedApiKey.ToString(), configuredApiKey, StringComparison.Ordinal))
            {
                await WriteUnauthorizedResponse(context);

                return;
            }

            await _next(context);
        }

        private static async Task WriteUnauthorizedResponse(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;

            await context.Response.WriteAsJsonAsync(
                new
                {
                    message = "Missing or invalid X-API-Key header."
                }
            );
        }
    }
}
