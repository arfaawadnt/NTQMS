import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./shell/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'nonconformances',
        loadComponent: () => import('./features/nc/nc-list.component').then((m) => m.NcListComponent),
      },
      {
        path: 'nonconformances/:id',
        loadComponent: () => import('./features/nc/nc-detail.component').then((m) => m.NcDetailComponent),
      },
      {
        path: 'notifications',
        loadComponent: () => import('./features/notifications/notifications.component').then((m) => m.NotificationsComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
