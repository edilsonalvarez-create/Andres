using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Services;

namespace QAGuardian.Infrastructure.Caching;

/// <summary>Caché distribuida sobre IDistributedCache (Redis en producción, memoria en desarrollo/pruebas).</summary>
public class DistributedCacheService : ICacheService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);
    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedCacheService> _logger;

    public DistributedCacheService(IDistributedCache cache, ILogger<DistributedCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var json = await _cache.GetStringAsync(key, ct);
            return json is null ? default : JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            // La caché es un optimizador: si Redis no responde, la app sigue funcionando.
            _logger.LogWarning(ex, "Fallo leyendo caché para la clave {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? DefaultTtl
            };
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(value), options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo escribiendo caché para la clave {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _cache.RemoveAsync(key, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo eliminando caché para la clave {Key}", key);
        }
    }
}
