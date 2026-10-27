using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using TVLauncher.App.Models;

namespace TVLauncher.App.Services;

public sealed class WeatherService(IHttpClientFactory factory)
{
    private WeatherSnapshot? _cache;
    private DateTimeOffset _cacheExpires;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<WeatherSnapshot?> GetAsync(CancellationToken cancellation = default)
    {
        if (_cache is not null && _cacheExpires > DateTimeOffset.UtcNow) return _cache;
        await _lock.WaitAsync(cancellation);
        try
        {
            if (_cache is not null && _cacheExpires > DateTimeOffset.UtcNow) return _cache;
            using var client = factory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(12);
            // Cidade inicial de exemplo, configurável em uma versão posterior.
            const string endpoint = "https://api.open-meteo.com/v1/forecast?latitude=-22.074&longitude=-48.740&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max&timezone=America%2FSao_Paulo&forecast_days=5";
            using var stream = await client.GetStreamAsync(endpoint, cancellation);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellation);
            var root = json.RootElement;
            var current = root.GetProperty("current");
            var daily = root.GetProperty("daily");
            var dates = daily.GetProperty("time").EnumerateArray().ToArray();
            var minimums = daily.GetProperty("temperature_2m_min").EnumerateArray().ToArray();
            var maximums = daily.GetProperty("temperature_2m_max").EnumerateArray().ToArray();
            var rain = daily.GetProperty("precipitation_probability_max").EnumerateArray().ToArray();
            var days = new List<DailyForecast>();
            for (var i = 0; i < dates.Length; i++)
            {
                if (DateOnly.TryParse(dates[i].GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    days.Add(new DailyForecast(date,
                        (int)Math.Round(minimums[i].GetDouble()),
                        (int)Math.Round(maximums[i].GetDouble()),
                        rain[i].ValueKind == JsonValueKind.Null ? 0 : rain[i].GetInt32()));
            }
            _cache = new WeatherSnapshot("Bariri, SP",
                (int)Math.Round(current.GetProperty("temperature_2m").GetDouble()),
                (int)Math.Round(current.GetProperty("apparent_temperature").GetDouble()),
                current.GetProperty("relative_humidity_2m").GetInt32(),
                (int)Math.Round(current.GetProperty("wind_speed_10m").GetDouble()),
                current.GetProperty("weather_code").GetInt32(), DateTimeOffset.Now, days);
            _cacheExpires = DateTimeOffset.UtcNow.AddMinutes(30);
            return _cache;
        }
        catch (Exception) when (!cancellation.IsCancellationRequested)
        {
            _cacheExpires = DateTimeOffset.UtcNow.AddMinutes(5);
            return _cache;
        }
        finally { _lock.Release(); }
    }

    public static string Describe(int code) => code switch
    {
        0 => "Céu limpo",
        1 or 2 => "Parcialmente nublado",
        3 => "Nublado",
        45 or 48 => "Neblina",
        51 or 53 or 55 or 56 or 57 => "Garoa",
        61 or 63 or 65 or 66 or 67 => "Chuva",
        71 or 73 or 75 or 77 => "Neve",
        80 or 81 or 82 => "Pancadas de chuva",
        95 or 96 or 99 => "Tempestade",
        _ => "Condições variadas"
    };
}
