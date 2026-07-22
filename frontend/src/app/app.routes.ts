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
        path: 'documents',
        loadComponent: () => import('./features/documents/document-list.component').then((m) => m.DocumentListComponent),
      },
      {
        path: 'documents/:id',
        loadComponent: () => import('./features/documents/document-detail.component').then((m) => m.DocumentDetailComponent),
      },
      {
        path: 'audits',
        loadComponent: () => import('./features/audits/audit-list.component').then((m) => m.AuditListComponent),
      },
      {
        path: 'audits/:id',
        loadComponent: () => import('./features/audits/audit-detail.component').then((m) => m.AuditDetailComponent),
      },
      {
        path: 'equipment',
        loadComponent: () => import('./features/equipment/equipment-list.component').then((m) => m.EquipmentListComponent),
      },
      {
        path: 'equipment/:id',
        loadComponent: () => import('./features/equipment/equipment-detail.component').then((m) => m.EquipmentDetailComponent),
      },
      {
        path: 'users',
        loadComponent: () => import('./features/users/users.component').then((m) => m.UsersComponent),
      },
      {
        path: 'notifications',
        loadComponent: () => import('./features/notifications/notifications.component').then((m) => m.NotificationsComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
