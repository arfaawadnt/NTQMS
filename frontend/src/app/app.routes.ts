import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { platformOnlyGuard, tenantOnlyGuard } from './core/role.guard';

export const routes: Routes = [
  {
    // Tenant front door: /t/{lab} pins the laboratory for this browser.
    path: 't/:tenant',
    loadComponent: () => import('./features/login/tenant-entry.component').then((m) => m.TenantEntryComponent),
  },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    // Standalone MFA enrollment — outside the shell so it works under an
    // enrollment-scoped session that is barred from every other endpoint.
    path: 'security/mfa-setup',
    canActivate: [authGuard],
    loadComponent: () => import('./features/security/mfa-setup.component').then((m) => m.MfaSetupComponent),
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
            path: 'settings/security',
            loadComponent: () => import('./features/security/security-settings.component').then((m) => m.SecuritySettingsComponent),
          },
          {
            path: 'manual',
            loadComponent: () => import('./features/manual/manual.component').then((m) => m.ManualComponent),
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
            path: 'quality-policy',
            loadComponent: () => import('./features/governance/quality-policy.component').then((m) => m.QualityPolicyComponent),
          },
          {
            path: 'quality-objectives',
            loadComponent: () => import('./features/objectives/objective-list.component').then((m) => m.ObjectiveListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/objectives/objective-detail.component').then((m) => m.ObjectiveDetailComponent),
              },
            ],
          },
          {
            path: 'feedback',
            loadComponent: () => import('./features/feedback/feedback-list.component').then((m) => m.FeedbackListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/feedback/feedback-detail.component').then((m) => m.FeedbackDetailComponent),
              },
            ],
          },
          {
            path: 'complaints',
            loadComponent: () => import('./features/complaints/complaint-list.component').then((m) => m.ComplaintListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/complaints/complaint-detail.component').then((m) => m.ComplaintDetailComponent),
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
            path: 'monitoring',
            loadComponent: () => import('./features/facility/monitoring-list.component').then((m) => m.MonitoringListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/facility/monitoring-detail.component').then((m) => m.MonitoringDetailComponent),
              },
            ],
          },
          {
            path: 'reference-standards',
            loadComponent: () => import('./features/equipment/standards-list.component').then((m) => m.StandardsListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/equipment/standards-detail.component').then((m) => m.StandardsDetailComponent),
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
            path: 'authorizations',
            loadComponent: () => import('./features/competency/authorization-matrix.component').then((m) => m.AuthorizationMatrixComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/competency/authorization-detail.component').then((m) => m.AuthorizationDetailComponent),
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
            path: 'conflicts',
            loadComponent: () => import('./features/risk/conflict-list.component').then((m) => m.ConflictListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/risk/conflict-detail.component').then((m) => m.ConflictDetailComponent),
              },
            ],
          },
          {
            path: 'org-context',
            loadComponent: () => import('./features/organization/org-context.component').then((m) => m.OrgContextComponent),
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
            path: 'method-comparisons',
            loadComponent: () => import('./features/analytical/method-comparison-list.component').then((m) => m.MethodComparisonListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/analytical/method-comparison-detail.component').then((m) => m.MethodComparisonDetailComponent),
              },
            ],
          },
          {
            path: 'linearity-studies',
            loadComponent: () => import('./features/analytical/linearity-list.component').then((m) => m.LinearityListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/analytical/linearity-detail.component').then((m) => m.LinearityDetailComponent),
              },
            ],
          },
          {
            path: 'detection-limits',
            loadComponent: () => import('./features/analytical/detection-limit-list.component').then((m) => m.DetectionLimitListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/analytical/detection-limit-detail.component').then((m) => m.DetectionLimitDetailComponent),
              },
            ],
          },
          {
            path: 'reference-intervals',
            loadComponent: () => import('./features/analytical/reference-interval-list.component').then((m) => m.ReferenceIntervalListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/analytical/reference-interval-detail.component').then((m) => m.ReferenceIntervalDetailComponent),
              },
            ],
          },
          {
            path: 'sigma-metrics',
            loadComponent: () => import('./features/analytical/sigma-list.component').then((m) => m.SigmaListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/analytical/sigma-detail.component').then((m) => m.SigmaDetailComponent),
              },
            ],
          },
          {
            path: 'precision-studies',
            loadComponent: () => import('./features/analytical/precision-list.component').then((m) => m.PrecisionListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/analytical/precision-detail.component').then((m) => m.PrecisionDetailComponent),
              },
            ],
          },
          {
            path: 'outlier-screenings',
            loadComponent: () => import('./features/analytical/outlier-list.component').then((m) => m.OutlierListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/analytical/outlier-detail.component').then((m) => m.OutlierDetailComponent),
              },
            ],
          },
          {
            path: 'carryover-studies',
            loadComponent: () => import('./features/analytical/carryover-list.component').then((m) => m.CarryoverListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/analytical/carryover-detail.component').then((m) => m.CarryoverDetailComponent),
              },
            ],
          },
          {
            path: 'lot-comparisons',
            loadComponent: () => import('./features/analytical/lot-comparison-list.component').then((m) => m.LotComparisonListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/analytical/lot-comparison-detail.component').then((m) => m.LotComparisonDetailComponent),
              },
            ],
          },
          {
            path: 'interference-studies',
            loadComponent: () => import('./features/analytical/interference-list.component').then((m) => m.InterferenceListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/analytical/interference-detail.component').then((m) => m.InterferenceDetailComponent),
              },
            ],
          },
          {
            path: 'instrument-comparabilities',
            loadComponent: () => import('./features/analytical/instrument-comparability-list.component').then((m) => m.InstrumentComparabilityListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/analytical/instrument-comparability-detail.component').then((m) => m.InstrumentComparabilityDetailComponent),
              },
            ],
          },
          {
            path: 'uncertainty',
            loadComponent: () => import('./features/analytical/uncertainty-list.component').then((m) => m.UncertaintyListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/analytical/uncertainty-detail.component').then((m) => m.UncertaintyDetailComponent),
              },
            ],
          },
          {
            path: 'pt-plans',
            loadComponent: () => import('./features/analytical/pt-plan-list.component').then((m) => m.PtPlanListComponent),
            children: [
              {
                path: ':id',
                loadComponent: () => import('./features/analytical/pt-plan-detail.component').then((m) => m.PtPlanDetailComponent),
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
            path: 'roles',
            loadComponent: () => import('./features/roles/roles.component').then((m) => m.RolesComponent),
          },
          {
            path: 'access-reviews',
            loadComponent: () => import('./features/security/access-reviews.component').then((m) => m.AccessReviewsComponent),
          },
          {
            path: 'notifications',
            loadComponent: () => import('./features/notifications/notifications.component').then((m) => m.NotificationsComponent),
          },
          {
            path: 'reference-data',
            loadComponent: () => import('./features/reference/reference-data.component').then((m) => m.ReferenceDataComponent),
          },
          {
            path: 'compliance',
            loadComponent: () => import('./features/compliance/compliance.component').then((m) => m.ComplianceComponent),
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
