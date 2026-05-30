export interface TodoItem {
  id: number;
  title: string;
  isDone: boolean;
  createdAt: string;
}

export interface WeatherForecast {
  city: string;
  tempC: number;
  summary: string;
  cached: boolean;
}
