import { TestBed } from '@angular/core/testing';
import { I18nService } from './i18n.service';

describe('I18nService', () => {
  let service: I18nService;

  beforeEach(() => {
    localStorage.removeItem('qams.lang');
    TestBed.configureTestingModule({});
    service = TestBed.inject(I18nService);
  });

  it('defaults to English', () => {
    expect(service.lang()).toBe('en');
    expect(service.isRtl()).toBeFalse();
  });

  it('translates a known key in every language', () => {
    expect(service.t('nav.complaints')).toBe('Complaints');
    service.setLang('ar');
    expect(service.t('nav.complaints')).toBe('الشكاوى');
    service.setLang('fr');
    expect(service.t('nav.complaints')).toBe('Réclamations');
  });

  it('falls back to the key for unknown entries instead of blanking the UI', () => {
    expect(service.t('no.such.key')).toBe('no.such.key');
  });

  it('flags RTL only for Arabic and persists the choice', () => {
    service.setLang('ar');
    expect(service.isRtl()).toBeTrue();
    expect(localStorage.getItem('qams.lang')).toBe('ar');
    service.setLang('fr');
    expect(service.isRtl()).toBeFalse();
  });
});
