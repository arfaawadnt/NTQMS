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
        path: 'competencies',
        loadComponent: () => import('./features/competency/competency-list.component').then((m) => m.CompetencyListComponent),
      },
      {
        path: 'competencies/:id',
        loadComponent: () => import('./features/competency/competency-detail.component').then((m) => m.CompetencyDetailComponent),
      },
      {
        path: 'training',
        loadComponent: () => import('./features/competency/training-queue.component').then((m) => m.TrainingQueueComponent),
      },
      {
        path: 'risks',
        loadComponent: () => import('./features/risk/risk-list.component').then((m) => m.RiskListComponent),
      },
      {
        path: 'risks/:id',
        loadComponent: () => import('./features/risk/risk-detail.component').then((m) => m.RiskDetailComponent),
      },
      {
        path: 'changes',
        loadComponent: () => import('./features/change/change-list.component').then((m) => m.ChangeListComponent),
      },
      {
        path: 'changes/:id',
        loadComponent: () => import('./features/change/change-detail.component').then((m) => m.ChangeDetailComponent),
      },
      {
        path: 'management-reviews',
        loadComponent: () => import('./features/review/review-list.component').then((m) => m.ReviewListComponent),
      },
      {
        path: 'management-reviews/:id',
        loadComponent: () => import('./features/review/review-detail.component').then((m) => m.ReviewDetailComponent),
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
