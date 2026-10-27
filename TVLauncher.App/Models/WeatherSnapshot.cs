namespace TVLauncher.App.Models;

public sealed record WeatherSnapshot(
    string City,
    int Temperature,
    int FeelsLike,
    int Humidity,
    int WindKmh,
    int WeatherCode,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<DailyForecast> Days);

public sealed record DailyForecast(DateOnly Date, int Minimum, int Maximum, int RainProbability);
