import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { platformOnlyGuard, tenantOnlyGuard } from './core/role.guard';

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
      // Control plane — platform administrators only.
      {
        path: 'platform/tenants',
        canActivate: [platformOnlyGuard],
        loadComponent: () => import('./features/platform/tenants.component').then((m) => m.TenantsComponent),
      },
      // Tenant modules — lab users only (platform admins are redirected to the control plane).
      {
        path: '',
        canActivate: [tenantOnlyGuard],
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
          {
            path: 'dashboard',
            loadComponent: () => import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
          },
          {
            path: 'nonconformances',
            loadComponent: () => import('./features/nc/nc-list.component').then((m) => m.NcListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/nc/nc-detail.component').then((m) => m.NcDetailComponent),
              },
            ],
          },
          {
            path: 'documents',
            loadComponent: () => import('./features/documents/document-list.component').then((m) => m.DocumentListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/documents/document-detail.component').then((m) => m.DocumentDetailComponent),
              },
            ],
          },
          {
            path: 'audits',
            loadComponent: () => import('./features/audits/audit-list.component').then((m) => m.AuditListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/audits/audit-detail.component').then((m) => m.AuditDetailComponent),
              },
            ],
          },
          {
            path: 'equipment',
            loadComponent: () => import('./features/equipment/equipment-list.component').then((m) => m.EquipmentListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/equipment/equipment-detail.component').then((m) => m.EquipmentDetailComponent),
              },
            ],
          },
          {
            path: 'competencies',
            loadComponent: () => import('./features/competency/competency-list.component').then((m) => m.CompetencyListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/competency/competency-detail.component').then((m) => m.CompetencyDetailComponent),
              },
            ],
          },
          {
            path: 'training',
            loadComponent: () => import('./features/competency/training-queue.component').then((m) => m.TrainingQueueComponent),
          },
          {
            path: 'risks',
            loadComponent: () => import('./features/risk/risk-list.component').then((m) => m.RiskListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/risk/risk-detail.component').then((m) => m.RiskDetailComponent),
              },
            ],
          },
          {
            path: 'changes',
            loadComponent: () => import('./features/change/change-list.component').then((m) => m.ChangeListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/change/change-detail.component').then((m) => m.ChangeDetailComponent),
              },
            ],
          },
          {
            path: 'management-reviews',
            loadComponent: () => import('./features/review/review-list.component').then((m) => m.ReviewListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/review/review-detail.component').then((m) => m.ReviewDetailComponent),
              },
            ],
          },
          {
            path: 'qc',
            loadComponent: () => import('./features/analytical/qc-profiles.component').then((m) => m.QcProfilesComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/analytical/qc-profile-detail.component').then((m) => m.QcProfileDetailComponent),
              },
            ],
          },
          {
            path: 'validation-studies',
            loadComponent: () => import('./features/analytical/study-list.component').then((m) => m.StudyListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/analytical/study-detail.component').then((m) => m.StudyDetailComponent),
              },
            ],
          },
          {
            path: 'proficiency-tests',
            loadComponent: () => import('./features/analytical/pt-list.component').then((m) => m.PtListComponent),
          },
          {
            path: 'suppliers',
            loadComponent: () => import('./features/supplier/supplier-list.component').then((m) => m.SupplierListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/supplier/supplier-detail.component').then((m) => m.SupplierDetailComponent),
              },
            ],
          },
          {
            path: 'tasks',
            loadComponent: () => import('./features/tasks/tasks.component').then((m) => m.TasksComponent),
          },
          {
            path: 'records',
            loadComponent: () => import('./features/records/records.component').then((m) => m.RecordsComponent),
          },
          {
            path: 'users',
            loadComponent: () => import('./features/users/users.component').then((m) => m.UsersComponent),
          },
          {
            path: 'notifications',
            loadComponent: () => import('./features/notifications/notifications.component').then((m) => m.NotificationsComponent),
          },
          {
            path: 'notification-rules',
            loadComponent: () => import('./features/notifications/notification-admin.component').then((m) => m.NotificationAdminComponent),
          },
        ],
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
