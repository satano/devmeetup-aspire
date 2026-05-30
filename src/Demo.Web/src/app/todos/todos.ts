import { Component, OnInit, inject, signal } from '@angular/core';
import { TodoItem } from '../models';
import { TodoService } from '../todo.service';

@Component({
  selector: 'app-todos',
  templateUrl: './todos.html',
  styleUrl: './todos.css',
})
export class Todos implements OnInit {
  private readonly service = inject(TodoService);

  protected readonly todos = signal<TodoItem[]>([]);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.service.getAll().subscribe({
      next: (items) => this.todos.set(items),
      error: () => this.error.set('Failed to load todos.'),
    });
  }

  protected add(input: HTMLInputElement): void {
    const title = input.value.trim();
    if (!title) {
      return;
    }

    this.service.add(title).subscribe({
      next: (created) => {
        this.todos.update((items) => [...items, created]);
        input.value = '';
      },
      error: () => this.error.set('Failed to add todo.'),
    });
  }

  protected toggle(item: TodoItem): void {
    this.service.update({ ...item, isDone: !item.isDone }).subscribe({
      next: (saved) =>
        this.todos.update((items) => items.map((t) => (t.id === saved.id ? saved : t))),
      error: () => this.error.set('Failed to update todo.'),
    });
  }

  protected remove(item: TodoItem): void {
    this.service.remove(item.id).subscribe({
      next: () => this.todos.update((items) => items.filter((t) => t.id !== item.id)),
      error: () => this.error.set('Failed to delete todo.'),
    });
  }
}
