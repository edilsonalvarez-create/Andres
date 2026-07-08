using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace QAGuardian.Application.Common.Behaviors;

/// <summary>Pipeline de MediatR: registra cada request y alerta sobre ejecuciones lentas (&gt;3s).</summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowRequestThresholdMs = 3000;
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Procesando {RequestName}", requestName);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await next();
            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs)
                _logger.LogWarning("{RequestName} tardó {Elapsed} ms", requestName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando {RequestName}", requestName);
            throw;
        }
    }
}
