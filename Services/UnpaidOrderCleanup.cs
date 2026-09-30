namespace e_Commerce_application.Services
{
    // Every few minutes, cancels online orders that were never paid so their stock is freed.
    public class UnpaidOrderCleanup : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UnpaidOrderCleanup> _logger;

        public UnpaidOrderCleanup(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<UnpaidOrderCleanup> logger)
        {
            _scopes = scopes;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var maxAge = TimeSpan.FromMinutes(_configuration.GetValue("Payments:UnpaidOrderMinutes", 60));
            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var cancelled = await scope.ServiceProvider.GetRequiredService<OrderService>().ExpireUnpaidAsync(maxAge);
                    if (cancelled > 0)
                    {
                        _logger.LogInformation("Cancelled {Count} unpaid orders", cancelled);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Unpaid order cleanup failed");
                }
            }
        }
    }
}
