import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { tokenInterceptor } from '@app/auth/token.interceptor';
import { ToastService } from '@app/services/toast/toast.service';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GroupOrdersService } from './group-orders.service';

const BASE_URL = 'https://api.test';

describe('GroupOrdersService · owner authorization on real API', () => {
  let service: GroupOrdersService;
  let http: HttpTestingController;
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error', 'info']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tokenInterceptor])),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: BASE_URL },
        { provide: ToastService, useValue: toast },
        { provide: AuthenticationService, useValue: { getTokens: () => ({ idToken: 'owner-token' }) } },
      ],
    });
    service = TestBed.inject(GroupOrdersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends the owner token with GET /guest/groups/{id}/sessions', () => {
    service.getSessions$('grp-1').subscribe();
    const req = http.expectOne(`${BASE_URL}/guest/groups/grp-1/sessions`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer owner-token');
    req.flush({ participants: [], total: 0 });
  });

  it('sends the owner token with DELETE /guest/groups/{id}/sessions/{id}', () => {
    service.removeSession$('grp-1', 'gs-2').subscribe();
    const req = http.expectOne(`${BASE_URL}/guest/groups/grp-1/sessions/gs-2`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer owner-token');
    req.flush({ participants: [], total: 0 });
  });

  it('silent background request shows no error toast', () => {
    let failed: HttpErrorResponse | undefined;
    service.getSessions$('grp-1', { silent: true }).subscribe({ error: (error) => (failed = error) });
    http.expectOne(`${BASE_URL}/guest/groups/grp-1/sessions`).flush('down', { status: 500, statusText: 'Server Error' });

    expect(failed?.status).toBe(500);
    expect(toast.error).not.toHaveBeenCalled();
  });

  it('guest requests stay untouched: no owner token on /guest/sessions', () => {
    TestBed.inject(HttpClient).post(`${BASE_URL}/guest/sessions`, {}).subscribe();
    const req = http.expectOne(`${BASE_URL}/guest/sessions`);
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });
});
