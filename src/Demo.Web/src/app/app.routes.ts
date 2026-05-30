import { Routes } from '@angular/router';
import { Todos } from './todos/todos';
import { Weather } from './weather/weather';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'todos' },
  { path: 'todos', component: Todos },
  { path: 'weather', component: Weather },
];
