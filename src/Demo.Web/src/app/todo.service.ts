import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { TodoItem } from './models';

// Talks only to the gateway via the relative /api path — the dev-server proxy
// (proxy.conf.json) forwards it, so the SPA is effectively same-origin (no CORS).
@Injectable({ providedIn: 'root' })
export class TodoService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/todos';

  getAll(): Observable<TodoItem[]> {
    return this.http.get<TodoItem[]>(this.baseUrl);
  }

  add(title: string): Observable<TodoItem> {
    return this.http.post<TodoItem>(this.baseUrl, { title });
  }

  update(item: TodoItem): Observable<TodoItem> {
    return this.http.put<TodoItem>(`${this.baseUrl}/${item.id}`, {
      title: item.title,
      isDone: item.isDone,
    });
  }

  remove(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
