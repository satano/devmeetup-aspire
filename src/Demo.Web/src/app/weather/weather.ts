import { Component, inject, signal } from '@angular/core';
import { WeatherForecast } from '../models';
import { WeatherService } from '../weather.service';

@Component({
  selector: 'app-weather',
  templateUrl: './weather.html',
  styleUrl: './weather.css',
})
export class Weather {
  private readonly service = inject(WeatherService);

  protected readonly forecast = signal<WeatherForecast | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected search(city: string): void {
    const trimmed = city.trim();
    if (!trimmed) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.service.get(trimmed).subscribe({
      next: (result) => {
        this.forecast.set(result);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to fetch forecast.');
        this.loading.set(false);
      },
    });
  }
}
