import { Injectable, computed, signal } from '@angular/core';

export type Lang = 'en' | 'ar' | 'fr';

type Dict = Record<string, { en: string; ar: string; fr: string }>;

/**
 * Lightweight trilingual dictionary + RTL direction, matching the design
 * system's EN/AR/FR requirement. Acronyms (NC, CAPA, SOP, QC, PT, RPN) are
 * left intact across languages. Persisted per browser.
 */
@Injectable({ providedIn: 'root' })
export class I18nService {
  private static readonly KEY = 'qams.lang';

  private readonly dict: Dict = {
    'app.title': { en: 'NT.QAMS', ar: 'NT.QAMS', fr: 'NT.QAMS' },
    'nav.dashboard': { en: 'Dashboard', ar: 'لوحة القيادة', fr: 'Tableau de bord' },
    'nav.nc': { en: 'NC & CAPA', ar: 'حالات عدم المطابقة', fr: 'NC et CAPA' },
    'nav.notifications': { en: 'Notifications', ar: 'الإشعارات', fr: 'Notifications' },
    'nav.signout': { en: 'Sign Out', ar: 'تسجيل الخروج', fr: 'Se déconnecter' },
    'login.title': { en: 'Sign In', ar: 'تسجيل الدخول', fr: 'Connexion' },
    'login.tenant': { en: 'Laboratory Identifier', ar: 'معرّف المختبر', fr: 'Identifiant du laboratoire' },
    'login.tenantHint': { en: 'Leave empty for platform administrators', ar: 'اتركه فارغًا لمسؤولي المنصة', fr: 'Laisser vide pour les administrateurs' },
    'login.email': { en: 'Email', ar: 'البريد الإلكتروني', fr: 'Courriel' },
    'login.password': { en: 'Password', ar: 'كلمة المرور', fr: 'Mot de passe' },
    'login.mfa': { en: 'Authenticator Code', ar: 'رمز المصادقة', fr: "Code d'authentification" },
    'login.submit': { en: 'Sign In', ar: 'دخول', fr: 'Se connecter' },
    'login.mfaPrompt': { en: 'Enter the 6-digit code from your authenticator app.', ar: 'أدخل الرمز المكوّن من ٦ أرقام.', fr: 'Saisissez le code à 6 chiffres.' },
    'dash.title': { en: 'Quality Dashboard', ar: 'لوحة الجودة', fr: 'Tableau de la qualité' },
    'dash.openNc': { en: 'Open Nonconformances', ar: 'حالات عدم المطابقة المفتوحة', fr: 'Non-conformités ouvertes' },
    'dash.highRpn': { en: 'High-RPN Items', ar: 'عناصر عالية الخطورة', fr: 'Éléments à RPN élevé' },
    'dash.unread': { en: 'Unread Notifications', ar: 'إشعارات غير مقروءة', fr: 'Notifications non lues' },
    'nc.title': { en: 'Nonconformances & CAPA', ar: 'حالات عدم المطابقة', fr: 'Non-conformités et CAPA' },
    'nc.new': { en: 'Raise NC', ar: 'فتح حالة', fr: 'Créer une NC' },
    'nc.ref': { en: 'Reference', ar: 'المرجع', fr: 'Référence' },
    'nc.subject': { en: 'Title', ar: 'العنوان', fr: 'Titre' },
    'nc.status': { en: 'Status', ar: 'الحالة', fr: 'Statut' },
    'nc.severity': { en: 'Severity', ar: 'الخطورة', fr: 'Gravité' },
    'nc.rpn': { en: 'RPN', ar: 'RPN', fr: 'RPN' },
    'nc.source': { en: 'Source', ar: 'المصدر', fr: 'Source' },
    'nc.likelihood': { en: 'Likelihood', ar: 'الاحتمالية', fr: 'Probabilité' },
    'nc.description': { en: 'Description', ar: 'الوصف', fr: 'Description' },
    'nc.create': { en: 'Create', ar: 'إنشاء', fr: 'Créer' },
    'nc.cancel': { en: 'Cancel', ar: 'إلغاء', fr: 'Annuler' },
    'nc.submit': { en: 'Submit', ar: 'إرسال', fr: 'Soumettre' },
    'nc.empty': { en: 'No nonconformances yet.', ar: 'لا توجد حالات بعد.', fr: 'Aucune non-conformité.' },
    'common.loading': { en: 'Loading…', ar: 'جارٍ التحميل…', fr: 'Chargement…' },
    'notif.title': { en: 'Notifications', ar: 'الإشعارات', fr: 'Notifications' },
    'notif.empty': { en: 'No notifications.', ar: 'لا توجد إشعارات.', fr: 'Aucune notification.' },
    'notif.markRead': { en: 'Mark read', ar: 'وضع كمقروء', fr: 'Marquer comme lu' },
  };

  readonly lang = signal<Lang>(this.restore());
  readonly isRtl = computed(() => this.lang() === 'ar');

  setLang(lang: Lang): void {
    localStorage.setItem(I18nService.KEY, lang);
    this.lang.set(lang);
  }

  t(key: string): string {
    const entry = this.dict[key];
    return entry ? entry[this.lang()] : key;
  }

  private restore(): Lang {
    const stored = localStorage.getItem(I18nService.KEY);
    return stored === 'ar' || stored === 'fr' ? stored : 'en';
  }
}
