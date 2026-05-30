import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { WeatherForecast } from './models';

@Injectable({ providedIn: 'root' })
export class WeatherService {
  private readonly http = inject(HttpClient);

  get(city: string): Observable<WeatherForecast> {
    return this.http.get<WeatherForecast>(`/api/weather/${encodeURIComponent(city)}`);
  }
}
