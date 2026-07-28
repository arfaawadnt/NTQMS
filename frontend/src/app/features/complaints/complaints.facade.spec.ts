import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComplaintsFacade } from './complaints.facade';
import { ComplaintListItem } from '../../core/models';
import { environment } from '../../../environments/environment';

describe('ComplaintsFacade', () => {
  let facade: ComplaintsFacade;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/complaints`;

  const listItem: ComplaintListItem = {
    id: 'c1', complaintRef: 'CMP-2026-0001', subject: 'Late report', channel: 'Email',
    status: 'Logged', confidential: false, complainantName: 'Dr. Client',
    loggedAtUtc: '2026-07-25T10:00:00Z', branchId: null, departmentId: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    facade = TestBed.inject(ComplaintsFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the list and clears the loading flag', async () => {
    const done = facade.loadList();
    expect(facade.loading()).toBeTrue();

    http.expectOne(base).flush([listItem]);
    await done;

    expect(facade.list()).toEqual([listItem]);
    expect(facade.loading()).toBeFalse();
    expect(facade.error()).toBe('');
  });

  it('passes the status filter as a query parameter', async () => {
    const done = facade.loadList('Resolved');
    http.expectOne(`${base}?status=Resolved`).flush([]);
    await done;
    expect(facade.list()).toEqual([]);
  });

  it('surfaces the backend problem title on failure (e.g. the CMP-020 close gate)', async () => {
    const done = facade.close('c1');
    http.expectOne(`${base}/c1/close`).flush(
      { title: 'The linked nonconformance must be closed before the complaint.', code: 'CMP-020' },
      { status: 422, statusText: 'Unprocessable Entity' });
    await done;

    expect(facade.error()).toBe('The linked nonconformance must be closed before the complaint.');
  });

  it('refreshes the selected complaint after a workflow mutation', async () => {
    const done = facade.acknowledge('c1');
    http.expectOne(`${base}/c1/acknowledge`).flush(null);
    // The refetch is issued in a microtask after the POST resolves — yield once.
    await new Promise((resolve) => setTimeout(resolve));
    http.expectOne(`${base}/c1`).flush({ ...listItem, status: 'Acknowledged', description: 'D' });
    await done;

    expect(facade.selected()?.status).toBe('Acknowledged');
  });
});
