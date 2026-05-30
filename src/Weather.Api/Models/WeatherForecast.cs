namespace Weather.Api.Models;

public record WeatherForecast(
	string City,
	int TempC,
	string Summary,
	bool Cached
);
