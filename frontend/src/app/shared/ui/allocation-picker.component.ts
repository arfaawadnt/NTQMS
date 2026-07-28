import { ChangeDetectionStrategy, Component, OnInit, inject, input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { I18nService } from '../../core/i18n.service';
import { OrgDataService } from '../../core/org-data.service';
import { Department } from '../../core/models';

/**
 * Organizational allocation picker: cascading branch → department selects
 * bound to two parent-owned FormControls. Department options are filtered by
 * the chosen branch and the department resets whenever the branch changes,
 * so an inconsistent pair cannot be submitted. Both are optional (single-site
 * labs skip allocation).
 */
@Component({
    selector: 'qams-allocation-picker',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule],
    template: `
    <div class="pair">
      <div>
        <label>{{ i18n.t('alloc.branch') }}</label>
        <select [formControl]="branchCtrl()" (change)="onBranchChange()">
          <option value="">{{ i18n.t('alloc.none') }}</option>
          @for (b of org.branches(); track b.id) {
            <option [value]="b.id">{{ b.code }} — {{ b.name }}</option>
          }
        </select>
      </div>
      <div>
        <label>{{ i18n.t('alloc.department') }}</label>
        <select [formControl]="departmentCtrl()" [attr.disabled]="branchCtrl().value ? null : ''">
          <option value="">{{ i18n.t('alloc.none') }}</option>
          @for (d of departmentOptions(); track d.id) {
            <option [value]="d.id">{{ d.code }} — {{ d.name }}</option>
          }
        </select>
      </div>
    </div>
  `,
    styles: [`
    .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    select { width: 100%; }
    label { margin-top: 12px; }
  `]
})
export class AllocationPickerComponent implements OnInit {
  readonly i18n = inject(I18nService);
  readonly org = inject(OrgDataService);

  /** Parent-owned control holding the branch id ('' = unallocated). */
  readonly branchCtrl = input.required<FormControl<string>>();
  /** Parent-owned control holding the department id ('' = unallocated). */
  readonly departmentCtrl = input.required<FormControl<string>>();

  ngOnInit(): void { void this.org.ensureOrg(); }

  /** Departments belonging to the selected branch (FormControl values are not signals — plain getter). */
  departmentOptions(): readonly Department[] {
    const branchId = this.branchCtrl().value;
    return branchId ? this.org.departments().filter((d) => d.branchId === branchId) : [];
  }

  /** Changing the branch invalidates any previously chosen department. */
  onBranchChange(): void { this.departmentCtrl().setValue(''); }
}
