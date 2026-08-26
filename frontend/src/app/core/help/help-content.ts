/**
 * Per-page workflow help — the single source of truth shared by the in-page
 * help popup (opened from the header ? icon) and the standalone User Manual
 * module. Each topic carries a plain-language description, the page's workflow
 * as ordered stages (rendered as a diagram + progress bar), and step-by-step
 * usage guidance, all in the three supported languages.
 */

import { Lang } from '../i18n.service';

/** A short piece of text in each supported language. */
export interface LocalizedText {
  en: string;
  ar: string;
  fr: string;
}

/** One stage of a page's workflow — a labelled node on the diagram/progress bar. */
export interface HelpStep {
  /** Stage name (short). */
  label: LocalizedText;
  /** One-line explanation of what happens at this stage. */
  detail: LocalizedText;
}

/** The complete help entry for one page. */
export interface HelpTopic {
  /** Top-level route this topic documents, e.g. '/nonconformances'. */
  route: string;
  /** Existing i18n key for the page title (reuses the sidebar label). */
  titleKey: string;
  /** Existing i18n group key (matches the sidebar group). */
  groupKey: string;
  /** Icon name from NAV_ICONS. */
  icon: string;
  /** Plain-language description of the page and its purpose. */
  summary: LocalizedText;
  /** Ordered workflow stages; empty for pages without a state machine. */
  steps: HelpStep[];
  /** Step-by-step "how to use this page" guidance. */
  usage: LocalizedText[];
}

/** Resolve a localized string for the active language. */
export function tr(text: LocalizedText, lang: Lang): string {
  return text[lang];
}

// Convenience builders keep the (large) table below readable.
const L = (en: string, ar: string, fr: string): LocalizedText => ({ en, ar, fr });
const S = (label: LocalizedText, detail: LocalizedText): HelpStep => ({ label, detail });

/** Workflow shared by every CLSI analytical study (immutable evidence → derive → freeze). */
const ANALYTICAL_FLOW: HelpStep[] = [
  S(
    L('Data Entry', 'إدخال البيانات', 'Saisie des données'),
    L('Register the study, then add measurement evidence. Entries are immutable — removing one and editing re-opens the results.',
      'سجّل الدراسة ثم أضف بيّنات القياس. الإدخالات غير قابلة للتعديل — حذف إدخال يعيد فتح النتائج.',
      'Enregistrez l’étude puis ajoutez les preuves de mesure. Les saisies sont immuables — en retirer une rouvre les résultats.'),
  ),
  S(
    L('Calculated', 'محسوبة', 'Calculée'),
    L('Run the calculation — the server derives every statistic and the pass/fail verdict from the entered data.',
      'شغّل الحساب — يشتق الخادم كل الإحصاءات وحكم النجاح/الفشل من البيانات المُدخلة.',
      'Lancez le calcul — le serveur dérive chaque statistique et le verdict conforme/non conforme à partir des données.'),
  ),
  S(
    L('Signed Off', 'موقّعة', 'Validée'),
    L('An authorized reviewer signs off; the evidence and results freeze and become an audit-ready record.',
      'يوقّع مراجع مخوّل؛ تُجمّد البيّنات والنتائج وتصبح سجلاً جاهزًا للتدقيق.',
      'Un réviseur autorisé valide ; les preuves et résultats sont gelés et deviennent un enregistrement auditable.'),
  ),
];

const analyticalUsage = (whatToEnter: LocalizedText): LocalizedText[] => [
  L('Click New to register the study with its analyte, unit and acceptance limit.',
    'انقر «جديد» لتسجيل الدراسة مع المُحلَّل والوحدة وحد القبول.',
    'Cliquez sur Nouveau pour enregistrer l’étude avec son analyte, son unité et sa limite d’acceptation.'),
  whatToEnter,
  L('Press Calculate once enough data is entered; review the derived statistics and verdict.',
    'اضغط «حساب» بعد إدخال بيانات كافية؛ راجع الإحصاءات المشتقة والحكم.',
    'Appuyez sur Calculer une fois assez de données saisies ; examinez les statistiques dérivées et le verdict.'),
  L('A Quality Manager signs off to freeze the record for audit; a signed record can no longer be edited.',
    'يوقّع مدير الجودة لتجميد السجل للتدقيق؛ لا يمكن تعديل سجل موقّع.',
    'Un responsable qualité valide pour geler l’enregistrement ; un enregistrement validé ne peut plus être modifié.'),
];

/**
 * The help table. Order follows the sidebar so the User Manual reads top-to-bottom
 * like the navigation.
 */
export const HELP_TOPICS: HelpTopic[] = [
  // ── Overview ──────────────────────────────────────────────────────────────
  {
    route: '/dashboard', titleKey: 'nav.dashboard', groupKey: 'nav.groupOverview', icon: 'dashboard',
    summary: L(
      'Your laboratory at a glance: live quality KPIs, an open-nonconformance Pareto, SLA compliance and a 90-day trend, all scoped to your tenant.',
      'مختبرك في لمحة: مؤشرات أداء جودة حيّة، وتحليل باريتو لحالات عدم المطابقة المفتوحة، والالتزام بمستوى الخدمة، واتجاه 90 يومًا، ضمن نطاق مؤسستك.',
      'Votre laboratoire en un coup d’œil : indicateurs qualité en direct, Pareto des non-conformités ouvertes, respect des SLA et tendance sur 90 jours, limités à votre locataire.'),
    steps: [],
    usage: [
      L('Read the KPI tiles for the current quality posture; figures refresh automatically.',
        'اقرأ بطاقات المؤشرات لمعرفة وضع الجودة الحالي؛ تتحدّث الأرقام تلقائيًا.',
        'Lisez les tuiles KPI pour la posture qualité actuelle ; les chiffres se rafraîchissent automatiquement.'),
      L('Use the Pareto and trend charts to spot where to focus improvement effort.',
        'استخدم مخططات باريتو والاتجاه لتحديد أين تركّز جهد التحسين.',
        'Utilisez les graphiques Pareto et de tendance pour cibler l’effort d’amélioration.'),
      L('Open the sidebar to drill into any module behind a headline number.',
        'افتح الشريط الجانبي للتعمّق في أي وحدة خلف رقم رئيسي.',
        'Ouvrez la barre latérale pour explorer le module derrière chaque chiffre clé.'),
    ],
  },
  {
    route: '/quality-analytics', titleKey: 'nav.qualityAnalytics', groupKey: 'nav.groupOverview', icon: 'analytics',
    summary: L(
      'Quality analytics: the nine quality sub-systems measured from live records, a configurable composite health score, and the same figures framed as ISO/IEC 17025 \u00a78.9.2 management-review inputs.',
      '\u062a\u062d\u0644\u064a\u0644\u0627\u062a \u0627\u0644\u062c\u0648\u062f\u0629: \u0623\u0646\u0638\u0645\u0629 \u0627\u0644\u062c\u0648\u062f\u0629 \u0627\u0644\u062a\u0633\u0639\u0629 \u0645\u0642\u0627\u0633\u0629 \u0645\u0646 \u0627\u0644\u0633\u062c\u0644\u0627\u062a \u0627\u0644\u062d\u064a\u0629\u060c \u0648\u0645\u0624\u0634\u0631 \u0635\u062d\u0629 \u0645\u0631\u0643\u0628 \u0642\u0627\u0628\u0644 \u0644\u0644\u0636\u0628\u0637\u060c \u0648\u0627\u0644\u0623\u0631\u0642\u0627\u0645 \u0646\u0641\u0633\u0647\u0627 \u0645\u0639\u0631\u0648\u0636\u0629 \u0643\u0645\u062f\u062e\u0644\u0627\u062a \u0645\u0631\u0627\u062c\u0639\u0629 \u0627\u0644\u0625\u062f\u0627\u0631\u0629 \u0648\u0641\u0642 ISO/IEC 17025 \u00a78.9.2.',
      'Analytique qualit\u00e9 : les neuf sous-syst\u00e8mes mesur\u00e9s depuis les enregistrements en direct, un indice de sant\u00e9 composite configurable, et les m\u00eames chiffres pr\u00e9sent\u00e9s comme entr\u00e9es de revue de direction ISO/IEC 17025 \u00a78.9.2.'),
    steps: [],
    usage: [
      L('Filter by branch or department; sections whose records carry no attribution say so instead of pretending to narrow.',
        '\u0635\u0641\u0650 \u062d\u0633\u0628 \u0627\u0644\u0641\u0631\u0639 \u0623\u0648 \u0627\u0644\u0642\u0633\u0645\u061b \u0627\u0644\u0623\u0642\u0633\u0627\u0645 \u0627\u0644\u062a\u064a \u0644\u0627 \u062a\u062d\u0645\u0644 \u0633\u062c\u0644\u0627\u062a\u0647\u0627 \u0625\u0633\u0646\u0627\u062f\u0627\u064b \u062a\u0635\u0631\u0651\u062d \u0628\u0630\u0644\u0643 \u0628\u062f\u0644 \u0627\u062f\u0639\u0627\u0621 \u0627\u0644\u062a\u0636\u064a\u064a\u0642.',
        'Filtrez par site ou service ; les sections sans rattachement le signalent au lieu de simuler un filtrage.'),
      L('Switch between the Statistics and Management Review tabs \u2014 both render the same computation, so the figures cannot disagree.',
        '\u0628\u062f\u0651\u0644 \u0628\u064a\u0646 \u062a\u0628\u0648\u064a\u0628\u064a \u0627\u0644\u0625\u062d\u0635\u0627\u0621\u0627\u062a \u0648\u0645\u0631\u0627\u062c\u0639\u0629 \u0627\u0644\u0625\u062f\u0627\u0631\u0629 \u2014 \u0643\u0644\u0627\u0647\u0645\u0627 \u064a\u0639\u0631\u0636 \u0627\u0644\u062d\u0633\u0627\u0628 \u0646\u0641\u0633\u0647 \u0641\u0644\u0627 \u064a\u0645\u0643\u0646 \u0623\u0646 \u062a\u062a\u0639\u0627\u0631\u0636 \u0627\u0644\u0623\u0631\u0642\u0627\u0645.',
        'Basculez entre Statistiques et Revue de direction \u2014 les deux affichent le m\u00eame calcul, les chiffres ne peuvent diverger.'),
      L('With reports.manage you can tune the health-score weighting; every change requires a reason and is recorded in the audit trail.',
        '\u0645\u0639 \u0635\u0644\u0627\u062d\u064a\u0629 reports.manage \u064a\u0645\u0643\u0646\u0643 \u0636\u0628\u0637 \u0623\u0648\u0632\u0627\u0646 \u0627\u0644\u0645\u0624\u0634\u0631\u061b \u0643\u0644 \u062a\u063a\u064a\u064a\u0631 \u064a\u062a\u0637\u0644\u0628 \u0633\u0628\u0628\u0627\u064b \u0648\u064a\u0633\u062c\u0644 \u0641\u064a \u0633\u062c\u0644 \u0627\u0644\u062a\u062f\u0642\u064a\u0642.',
        'Avec reports.manage vous pouvez ajuster la pond\u00e9ration ; chaque modification exige un motif et est trac\u00e9e.'),
      L('A dash means \u201cno population to measure\u201d \u2014 it is deliberately not shown as 0%.',
        '\u0627\u0644\u0634\u0631\u0637\u0629 \u062a\u0639\u0646\u064a \u0644\u0627 \u0645\u062c\u062a\u0645\u0639 \u0644\u0644\u0642\u064a\u0627\u0633 \u2014 \u0648\u0644\u0627 \u062a\u064f\u0639\u0631\u0636 \u0639\u0645\u062f\u0627\u064b \u0643\u0640 0\u066a.',
        'Un tiret signifie \u00ab aucune population \u00e0 mesurer \u00bb \u2014 volontairement distinct de 0 %.'),
    ],
  },
  {
    route: '/tasks', titleKey: 'nav.tasks', groupKey: 'nav.groupOverview', icon: 'tasks',
    summary: L(
      'Your personal work queue: every action assigned to you across the system — CAPAs, reviews, verifications and approvals — with due dates and escalation.',
      'قائمة عملك الشخصية: كل إجراء مُسند إليك عبر النظام — إجراءات تصحيحية ومراجعات وتحقّقات واعتمادات — مع تواريخ الاستحقاق والتصعيد.',
      'Votre file de travail personnelle : chaque action qui vous est assignée — CAPA, revues, vérifications et approbations — avec échéances et escalade.'),
    steps: [
      S(L('Assigned', 'مُسند', 'Assignée'), L('A task lands in your queue with a due date.', 'تصل المهمة إلى قائمتك مع تاريخ استحقاق.', 'Une tâche arrive dans votre file avec une échéance.')),
      S(L('In Progress', 'قيد التنفيذ', 'En cours'), L('Open the linked record and do the work.', 'افتح السجل المرتبط ونفّذ العمل.', 'Ouvrez l’enregistrement lié et effectuez le travail.')),
      S(L('Done', 'منجزة', 'Terminée'), L('Completing the underlying action clears the task.', 'إتمام الإجراء الأساسي يُنهي المهمة.', 'L’achèvement de l’action sous-jacente clôt la tâche.')),
    ],
    usage: [
      L('Sort by due date; overdue items are flagged and may escalate to your manager.',
        'رتّب حسب تاريخ الاستحقاق؛ العناصر المتأخرة مُعلَّمة وقد تُصعَّد إلى مديرك.',
        'Triez par échéance ; les éléments en retard sont signalés et peuvent être escaladés à votre responsable.'),
      L('Click a task to jump straight to the record that needs your action.',
        'انقر مهمة للانتقال مباشرة إلى السجل الذي يحتاج إجراءك.',
        'Cliquez une tâche pour accéder directement à l’enregistrement concerné.'),
    ],
  },
  {
    route: '/notifications', titleKey: 'nav.notifications', groupKey: 'nav.groupOverview', icon: 'bell',
    summary: L(
      'System alerts addressed to you: assignments, approvals awaited, due-date reminders and escalations, kept for 90 days.',
      'تنبيهات النظام الموجّهة إليك: إسنادات، واعتمادات منتظَرة، وتذكيرات استحقاق، وتصعيدات، محفوظة 90 يومًا.',
      'Alertes système qui vous sont adressées : affectations, approbations attendues, rappels d’échéance et escalades, conservées 90 jours.'),
    steps: [],
    usage: [
      L('Click a notification to open the related record; mark items read to clear the badge.',
        'انقر إشعارًا لفتح السجل المرتبط؛ ضع العناصر كمقروءة لإزالة الشارة.',
        'Cliquez une notification pour ouvrir l’enregistrement lié ; marquez comme lu pour retirer le badge.'),
    ],
  },
  {
    route: '/integration', titleKey: 'nav.integration', groupKey: 'nav.groupAdmin', icon: 'integration',
    summary: L(
      'Integration & interoperability: register interface endpoints (HIS/LIS/pharmacy/HR over HL7 v2, FHIR R4 or file/DB extract), watch their health, inspect the inbound message inbox including failures, and read the ADT-derived patient census that supplies rate denominators.',
      'التكامل والتشغيل البيني: سجّل واجهات (HIS/LIS/الصيدلية/الموارد البشرية عبر HL7 v2 وFHIR R4 أو استخراج ملف/قاعدة بيانات)، وراقب حالتها، وافحص صندوق الرسائل الواردة بما فيها الفاشلة، واقرأ تعداد المرضى المشتق من ADT.',
      'Intégration et interopérabilité : enregistrez des points d’accès (SIH/SIL/pharmacie/RH via HL7 v2, FHIR R4 ou extraction), surveillez leur santé, inspectez la boîte de réception (y compris les échecs) et lisez le recensement patient issu de l’ADT.'),
    steps: [],
    usage: [
      L('Register an endpoint per source system and protocol; the adapter feeds messages into its inbox.',
        'سجّل واجهة لكل نظام مصدر وبروتوكول؛ يغذّي المحوّل الرسائل في صندوقها.',
        'Enregistrez un point d’accès par système et protocole ; l’adaptateur alimente sa boîte de réception.'),
      L('Watch the health dashboard and failed-message list; suspend an endpoint to stop ingestion, resume to clear the failure streak.',
        'راقب لوحة الحالة وقائمة الرسائل الفاشلة؛ علّق واجهة لإيقاف الاستقبال، واستأنف لمسح سلسلة الإخفاقات.',
        'Surveillez le tableau de santé et les messages en échec ; suspendez pour arrêter l’ingestion, reprenez pour réinitialiser.'),
    ],
  },

  // ── Improvement ───────────────────────────────────────────────────────────
  {
    route: '/audit-programs', titleKey: 'nav.auditPrograms', groupKey: 'nav.groupImprovement', icon: 'auditPrograms',
    summary: L(
      'The annual audit programme: plan the audits for the cycle by scope, department, standards chapter and risk-based priority, link each plan line to the audit that fulfils it, and track coverage so no area goes unaudited.',
      'برنامج التدقيق السنوي: خطّط تدقيقات الدورة حسب النطاق والقسم وفصل المعيار والأولوية المبنية على المخاطر، واربط كل بند بالتدقيق المنفّذ له، وتابع التغطية.',
      'Le programme d’audit annuel : planifiez les audits du cycle par périmètre, département, chapitre de norme et priorité, liez chaque ligne à l’audit qui la réalise et suivez la couverture.'),
    steps: [
      S(L('Draft', 'مسودة', 'Brouillon'), L('Build the plan lines for the year.', 'ابنِ بنود خطة السنة.', 'Constituez les lignes du plan.')),
      S(L('Active', 'نشط', 'Actif'), L('Activate, then schedule and complete each line.', 'فعّل، ثم جدول وأكمل كل بند.', 'Activez, puis planifiez et terminez chaque ligne.')),
      S(L('Closed', 'مغلق', 'Clôturé'), L('Close the programme at cycle end.', 'أغلق البرنامج في نهاية الدورة.', 'Clôturez le programme en fin de cycle.')),
    ],
    usage: [
      L('Create a programme for the year, then add plan lines with scope, priority and target quarter.',
        'أنشئ برنامجًا للسنة، ثم أضف بنودًا بالنطاق والأولوية والربع المستهدف.',
        'Créez un programme pour l’année, puis ajoutez des lignes avec périmètre, priorité et trimestre.'),
      L('Activate the programme, link each plan line to a scheduled audit, and mark it complete when done.',
        'فعّل البرنامج، واربط كل بند بتدقيق مجدول، وضع علامة مكتمل عند الانتهاء.',
        'Activez le programme, liez chaque ligne à un audit et marquez-la terminée.'),
      L('Watch the coverage figure — completed vs planned — to keep the hospital survey-ready all year.',
        'راقب نسبة التغطية — المنجز مقابل المخطط — للحفاظ على الجاهزية طوال العام.',
        'Surveillez la couverture — terminés vs planifiés — pour rester prêt toute l’année.'),
    ],
  },
  {
    route: '/surveys', titleKey: 'nav.surveys', groupKey: 'nav.groupImprovement', icon: 'surveys',
    summary: L(
      'Patient satisfaction surveys: define a survey with questions grouped by domain, open it for responses, capture responses by department, and read scored results (overall, by domain and by department) on a 1–5 scale.',
      'استبيانات رضا المرضى: عرّف استبيانًا بأسئلة مجمّعة حسب المجال، وافتحه للردود، والتقط الردود حسب القسم، واقرأ النتائج المسجّلة (إجمالي، وحسب المجال والقسم) على مقياس 1-5.',
      'Enquêtes de satisfaction : définissez une enquête avec des questions par domaine, ouvrez-la, saisissez les réponses par département et lisez les scores (global, par domaine et par département) sur une échelle 1–5.'),
    steps: [
      S(L('Draft', 'مسودة', 'Brouillon'), L('Add the questions grouped by domain.', 'أضف الأسئلة مجمّعة حسب المجال.', 'Ajoutez les questions par domaine.')),
      S(L('Open', 'مفتوحة', 'Ouverte'), L('Open and collect responses.', 'افتح واجمع الردود.', 'Ouvrez et collectez les réponses.')),
      S(L('Closed', 'مغلقة', 'Clôturée'), L('Close and read the final scores.', 'أغلق واقرأ النتائج.', 'Clôturez et lisez les scores.')),
    ],
    usage: [
      L('Create a survey, add questions with a domain (e.g. Communication, Waiting time), then open it.',
        'أنشئ استبيانًا، وأضف أسئلة بمجال (مثل التواصل، وقت الانتظار)، ثم افتحه.',
        'Créez une enquête, ajoutez des questions avec un domaine, puis ouvrez-la.'),
      L('Capture responses by department; read the results scored overall, by domain and by department.',
        'التقط الردود حسب القسم؛ اقرأ النتائج مسجّلة إجمالًا وحسب المجال والقسم.',
        'Saisissez les réponses par département ; lisez les scores global, par domaine et par département.'),
    ],
  },
  {
    route: '/committees', titleKey: 'nav.committees', groupKey: 'nav.groupImprovement', icon: 'committees',
    summary: L(
      'Committees & governance: define each committee (terms of reference, membership, quorum, frequency), run its meetings with an agenda, attendance and quorum, capture decisions and action items, and track those actions across meetings until closed — with minutes approved as the governance evidence.',
      'اللجان والحوكمة: عرّف كل لجنة (الاختصاصات والأعضاء والنصاب والتكرار)، وأدر اجتماعاتها بجدول أعمال وحضور ونصاب، وسجّل القرارات والإجراءات، وتابعها عبر الاجتماعات حتى الإغلاق — مع اعتماد المحضر كدليل حوكمة.',
      'Comités et gouvernance : définissez chaque comité (mandat, membres, quorum, fréquence), tenez ses réunions avec ordre du jour, présence et quorum, consignez décisions et actions, et suivez-les jusqu’à clôture — le PV approuvé faisant foi.'),
    steps: [
      S(L('Committee', 'اللجنة', 'Comité'), L('Define the committee and its members.', 'عرّف اللجنة وأعضاءها.', 'Définissez le comité et ses membres.')),
      S(L('Meeting', 'الاجتماع', 'Réunion'), L('Schedule a meeting and build the agenda.', 'جدول اجتماعًا وابنِ جدول الأعمال.', 'Planifiez une réunion et l’ordre du jour.')),
      S(L('Held', 'مُنعقد', 'Tenue'), L('Record attendance; hold once quorate.', 'سجّل الحضور؛ اعقد عند اكتمال النصاب.', 'Enregistrez la présence ; tenez si quorum.')),
      S(L('Minutes', 'المحضر', 'PV'), L('Capture decisions, record and approve minutes.', 'سجّل القرارات، ودوّن واعتمد المحضر.', 'Consignez décisions et approuvez le PV.')),
    ],
    usage: [
      L('Create a committee with its terms of reference, quorum and members.',
        'أنشئ لجنة باختصاصاتها ونصابها وأعضائها.',
        'Créez un comité avec son mandat, son quorum et ses membres.'),
      L('Schedule a meeting, add agenda items, record attendance, and hold it once the quorum is met.',
        'جدول اجتماعًا، وأضف بنودًا، وسجّل الحضور، واعقده عند اكتمال النصاب.',
        'Planifiez une réunion, ajoutez des points, enregistrez la présence et tenez-la au quorum.'),
      L('Capture decisions with owners and due dates; track open actions across meetings until closed, then approve the minutes.',
        'سجّل القرارات بأصحابها وتواريخها؛ تابع الإجراءات المفتوحة حتى الإغلاق، ثم اعتمد المحضر.',
        'Consignez les décisions avec responsables et échéances ; suivez les actions jusqu’à clôture, puis approuvez le PV.'),
    ],
  },
  {
    route: '/fmea', titleKey: 'nav.fmea', groupKey: 'nav.groupRisk', icon: 'fmea',
    summary: L(
      'Prospective failure-mode analysis (FMEA / HFMEA): map how a process can fail before harm occurs, score each failure mode Severity × Occurrence × Detection = RPN, work the highest-RPN modes first, and re-score after action to show the improvement.',
      'تحليل استباقي لأنماط الفشل (FMEA/HFMEA): حدّد كيف يمكن أن تفشل العملية قبل وقوع الضرر، وقيّم كل نمط بالخطورة × التكرار × الاكتشاف = RPN، وعالج الأعلى أولًا، وأعد التقييم بعد الإجراء.',
      'Analyse prospective des modes de défaillance (AMDEC / HFMEA) : identifiez comment un processus peut échouer, cotez chaque mode Gravité × Occurrence × Détection = RPN, traitez les RPN les plus élevés d’abord, puis re-cotez après action.'),
    steps: [
      S(L('Draft', 'مسودة', 'Brouillon'), L('Add the failure modes and score them.', 'أضف أنماط الفشل وقيّمها.', 'Ajoutez les modes et cotez-les.')),
      S(L('Active', 'نشط', 'Actif'), L('Recommend actions and re-score.', 'أوصِ بإجراءات وأعد التقييم.', 'Recommandez des actions et re-cotez.')),
      S(L('Closed', 'مغلق', 'Clôturé'), L('Close when the analysis cycle ends.', 'أغلق عند انتهاء الدورة.', 'Clôturez en fin de cycle.')),
    ],
    usage: [
      L('Create an FMEA for a process, then add failure modes with Severity, Occurrence and Detection (1–10 each).',
        'أنشئ تحليلًا لعملية، ثم أضف أنماط الفشل بالخطورة والتكرار والاكتشاف (1-10 لكلٍّ).',
        'Créez une AMDEC pour un processus, puis ajoutez des modes avec Gravité, Occurrence et Détection (1–10).'),
      L('Work the highest-RPN modes first: record a recommended action, then re-score to capture the residual RPN.',
        'عالج أنماط RPN الأعلى أولًا: سجّل إجراءً موصى به، ثم أعد التقييم لالتقاط RPN المتبقي.',
        'Traitez d’abord les RPN les plus élevés : enregistrez une action, puis re-cotez le RPN résiduel.'),
    ],
  },
  {
    route: '/standards', titleKey: 'nav.accreditation', groupKey: 'nav.groupRisk', icon: 'accreditation',
    summary: L(
      'The accreditation engine: hold each standard set (GAHAR, JCI, ISO) as measurable elements, self-assess every element, attach any record as evidence, and read a live readiness figure with a prioritised gap list — so compliance status is measured, not assumed.',
      'محرك الاعتماد: احتفظ بكل مجموعة معايير (جهار، JCI، ISO) كعناصر قابلة للقياس، وقيّم كل عنصر ذاتيًا، وأرفق أي سجل كدليل، واقرأ نسبة جاهزية حية مع قائمة فجوات مرتّبة.',
      'Le moteur d’accréditation : gérez chaque référentiel (GAHAR, JCI, ISO) en éléments mesurables, auto-évaluez chaque élément, attachez tout enregistrement comme preuve et lisez un taux de préparation en direct avec une liste d’écarts priorisée.'),
    steps: [
      S(L('Draft', 'مسودة', 'Brouillon'), L('Define the set and add its measurable elements.', 'عرّف المجموعة وأضف عناصرها القابلة للقياس.', 'Définissez le référentiel et ses éléments.')),
      S(L('Active', 'نشط', 'Actif'), L('Activate to begin self-assessment and evidence linking.', 'فعّل لبدء التقييم الذاتي وربط الأدلة.', 'Activez pour l’auto-évaluation et les preuves.')),
      S(L('Assess', 'التقييم', 'Évaluer'), L('Score each element and attach evidence.', 'قيّم كل عنصر وأرفق الأدلة.', 'Évaluez chaque élément et joignez les preuves.')),
      S(L('Readiness', 'الجاهزية', 'Préparation'), L('Track the live readiness % and close the gaps.', 'تابع نسبة الجاهزية الحية وأغلق الفجوات.', 'Suivez le taux de préparation et comblez les écarts.')),
    ],
    usage: [
      L('Define a standard set, add its chapters and measurable elements while it is in draft, then activate it.',
        'عرّف مجموعة معايير، وأضف فصولها وعناصرها أثناء المسودة، ثم فعّلها.',
        'Définissez un référentiel, ajoutez ses chapitres et éléments à l’état brouillon, puis activez-le.'),
      L('Self-assess each element (Compliant / Partial / Non-compliant / Not applicable) and attach any record as evidence.',
        'قيّم كل عنصر ذاتيًا (مطابق / جزئي / غير مطابق / غير منطبق) وأرفق أي سجل كدليل.',
        'Auto-évaluez chaque élément et joignez tout enregistrement comme preuve.'),
      L('Read the readiness dashboard and work the gap list — elements with no evidence, unassessed, or non-compliant, ranked by weight.',
        'اقرأ لوحة الجاهزية وعالج قائمة الفجوات — العناصر بلا أدلة أو غير المُقيَّمة أو غير المطابقة، مرتّبة حسب الوزن.',
        'Consultez le tableau de préparation et traitez la liste des écarts, classée par poids.'),
    ],
  },
  {
    route: '/indicators', titleKey: 'nav.indicators', groupKey: 'nav.groupImprovement', icon: 'indicators',
    summary: L(
      'The quality indicator library: define each indicator with a formal data dictionary, set its target and thresholds, record period values, and read a statistical-process-control chart that separates real signal from noise. Breaching the action threshold opens an analysis task automatically.',
      'مكتبة مؤشرات الجودة: عرّف كل مؤشر بقاموس بيانات رسمي، وحدّد هدفه وحدوده، وسجّل قيم الفترات، واقرأ مخطط ضبط إحصائي يفصل الإشارة الحقيقية عن الضوضاء. تجاوز حد الإجراء يفتح مهمة تحليل تلقائيًا.',
      'La bibliothèque d’indicateurs qualité : définissez chaque indicateur avec un dictionnaire de données, fixez sa cible et ses seuils, saisissez les valeurs par période et lisez une carte de contrôle statistique. Le dépassement du seuil d’action ouvre automatiquement une tâche d’analyse.'),
    steps: [],
    usage: [
      L('Click Define Indicator and complete the data dictionary — numerator, denominator, inclusions/exclusions, source and frequency.',
        'انقر «تعريف مؤشر» وأكمل قاموس البيانات — البسط والمقام والتضمين/الاستثناء والمصدر والتكرار.',
        'Cliquez sur Définir un indicateur et complétez le dictionnaire de données — numérateur, dénominateur, inclusions/exclusions, source et fréquence.'),
      L('Set the target with warning and action thresholds; the direction decides which side counts as a breach.',
        'حدّد الهدف مع حدود التحذير والإجراء؛ يحدّد الاتجاه أي جهة تُعدّ تجاوزًا.',
        'Fixez la cible avec les seuils d’alerte et d’action ; le sens décide quel côté constitue un dépassement.'),
      L('Record each period’s numerator and denominator; the rate, its grade and the control chart update, and an action breach opens an analysis task.',
        'سجّل بسط ومقام كل فترة؛ يتحدّث المعدل ودرجته ومخطط التحكم، ويفتح تجاوز الإجراء مهمة تحليل.',
        'Saisissez le numérateur et le dénominateur de chaque période ; le taux, son classement et la carte se mettent à jour, et un dépassement ouvre une tâche.'),
    ],
  },
  {
    route: '/incidents', titleKey: 'nav.incidents', groupKey: 'nav.groupImprovement', icon: 'incidents',
    summary: L(
      'The incident & occurrence register: report an adverse event, near miss or unsafe condition — openly or anonymously — then triage, investigate its contributing factors, and close it. Severe events auto-escalate and can be declared sentinel.',
      'سجل الحوادث والوقائع: أبلغ عن حدث ضار أو حالة كادت أن تقع أو ظرف غير آمن — علنًا أو بشكل مجهول — ثم صنّفه وحقّق في عوامله المساهمة وأغلقه. تُصعّد الأحداث الجسيمة تلقائيًا ويمكن إعلانها أحداثًا جسيمة.',
      'Le registre des incidents et événements : signalez un événement indésirable, un quasi-incident ou une situation dangereuse — ouvertement ou anonymement — puis triez, investiguez les facteurs contributifs et clôturez. Les événements graves sont escaladés automatiquement et peuvent être déclarés sentinelles.'),
    steps: [
      S(L('Reported', 'مُبلّغ', 'Signalé'), L('Capture what happened, where, when and the degree of harm.', 'سجّل ما حدث وأين ومتى ودرجة الضرر.', 'Consignez ce qui s’est passé, où, quand et le préjudice.')),
      S(L('Triaged', 'مُصنّف', 'Trié'), L('Classify and assign the incident to an owner.', 'صنّف الحادث وأسنده إلى مسؤول.', 'Classez et assignez l’incident à un responsable.')),
      S(L('Investigation', 'التحقيق', 'Investigation'), L('Reconstruct the timeline and identify contributing factors.', 'أعد بناء التسلسل الزمني وحدّد العوامل المساهمة.', 'Reconstituez la chronologie et identifiez les facteurs.')),
      S(L('Review', 'المراجعة', 'Revue'), L('Quality function reviews the completed investigation.', 'تراجع إدارة الجودة التحقيق المكتمل.', 'La fonction qualité examine l’investigation.')),
      S(L('Closed', 'مغلق', 'Clôturé'), L('Closed with a signed outcome; frozen for audit.', 'يُغلق بنتيجة موقّعة ويُجمّد للتدقيق.', 'Clôturé avec un résultat signé ; gelé pour l’audit.')),
    ],
    usage: [
      L('Click Report Incident, or Report Anonymously to submit without recording your identity (you receive a one-time reference).',
        'انقر «الإبلاغ عن حادث»، أو «إبلاغ مجهول» للإرسال دون تسجيل هويتك (تحصل على مرجع لمرة واحدة).',
        'Cliquez sur Signaler un incident, ou Signaler anonymement pour soumettre sans enregistrer votre identité (vous recevez une référence unique).'),
      L('Triage assigns an owner and category; investigation records contributing factors and a timeline.',
        'يُسند التصنيف مسؤولًا وفئة؛ ويسجّل التحقيق العوامل المساهمة وجدولًا زمنيًا.',
        'Le tri assigne un responsable et une catégorie ; l’investigation consigne les facteurs et une chronologie.'),
      L('Closing requires an electronic signature. A severe event can be declared a sentinel event, which triggers the executive protocol.',
        'يتطلب الإغلاق توقيعًا إلكترونيًا. يمكن إعلان الحدث الجسيم حدثًا جسيمًا يُفعّل البروتوكول التنفيذي.',
        'La clôture requiert une signature électronique. Un événement grave peut être déclaré sentinelle, déclenchant le protocole de direction.'),
    ],
  },
  {
    route: '/patient-safety', titleKey: 'nav.patientSafety', groupKey: 'nav.groupClinical', icon: 'patientSafety',
    summary: L(
      'The patient-safety register for falls and pressure injuries. Each event carries its harm level, and pressure injuries also carry a stage and whether they were present on admission or hospital-acquired. Rates are computed per 1,000 patient-days from the ADT-derived census.',
      'سجل سلامة المرضى لحالات السقوط وقرح الفراش. تحمل كل واقعة مستوى الضرر، وتحمل قرح الفراش أيضًا المرحلة وما إذا كانت موجودة عند الدخول أم مكتسبة بالمستشفى. تُحتسب المعدلات لكل 1000 يوم مريض من تعداد ADT.',
      'Le registre de sécurité des patients pour les chutes et les escarres. Chaque événement porte son niveau de préjudice ; les escarres portent aussi un stade et l’origine (présente à l’admission ou acquise à l’hôpital). Les taux sont calculés pour 1 000 jours-patients à partir du recensement ADT.'),
    steps: [
      S(L('Reported', 'مُبلّغ', 'Signalé'), L('Capture the fall or pressure injury, its unit, harm and — for injuries — stage and origin.', 'سجّل السقوط أو قرحة الفراش ووحدتها والضرر — وللقرح المرحلة والمنشأ.', 'Consignez la chute ou l’escarre, son unité, le préjudice et — pour les escarres — le stade et l’origine.')),
      S(L('Reviewed', 'تمت المراجعة', 'Examiné'), L('Clinical review records findings and any immediate action.', 'تسجّل المراجعة السريرية النتائج وأي إجراء فوري.', 'L’examen clinique consigne les constats et toute action immédiate.')),
      S(L('Closed', 'مغلق', 'Clôturé'), L('Closed and frozen once review is complete.', 'يُغلق ويُجمّد بعد اكتمال المراجعة.', 'Clôturé et gelé une fois l’examen terminé.')),
    ],
    usage: [
      L('Use Report fall or Report pressure injury; pressure injuries additionally capture stage and present-on-admission vs hospital-acquired.',
        'استخدم «الإبلاغ عن سقوط» أو «الإبلاغ عن قرحة فراش»؛ تلتقط القرح إضافيًا المرحلة وما إذا كانت موجودة عند الدخول أم مكتسبة بالمستشفى.',
        'Utilisez Signaler une chute ou Signaler une escarre ; les escarres saisissent en plus le stade et l’origine (admission ou hôpital).'),
      L('The tiles show falls, pressure-injury and hospital-acquired-pressure-injury (HAPI) rates per 1,000 patient-days, using the patient-day denominator from the integration census.',
        'تعرض البطاقات معدلات السقوط وقرح الفراش وقرح الفراش المكتسبة بالمستشفى لكل 1000 يوم مريض، باستخدام مقام أيام المرضى من تعداد التكامل.',
        'Les tuiles affichent les taux de chutes, d’escarres et d’escarres acquises à l’hôpital (HAPI) pour 1 000 jours-patients, avec le dénominateur issu du recensement d’intégration.'),
      L('Review records clinical findings; closing freezes the record for audit.',
        'تسجّل المراجعة النتائج السريرية؛ ويُجمّد الإغلاق السجل للتدقيق.',
        'L’examen consigne les constats cliniques ; la clôture gèle l’enregistrement pour l’audit.'),
    ],
  },
  {
    route: '/infection-control', titleKey: 'nav.infectionControl', groupKey: 'nav.groupClinical', icon: 'infectionControl',
    summary: L(
      'Healthcare-associated infection surveillance: CLABSI, CAUTI, VAP and SSI. Device-associated rates are computed per 1,000 device-days from the device-exposure register, and the device-utilisation ratio uses the ADT patient-days as its denominator.',
      'ترصّد العدوى المرتبطة بالرعاية الصحية: عدوى مجرى الدم والمسالك البولية والالتهاب الرئوي المرتبط بالتنفس وعدوى الموقع الجراحي. تُحتسب المعدلات المرتبطة بالأجهزة لكل 1000 يوم جهاز من سجل استخدام الأجهزة، وتستخدم نسبة استخدام الجهاز أيام المرضى مقامًا لها.',
      'Surveillance des infections associées aux soins : CLABSI, CAUTI, PAVM et ISO. Les taux liés aux dispositifs sont calculés pour 1 000 jours-dispositif à partir du registre d’exposition, et le ratio d’utilisation prend les jours-patients ADT comme dénominateur.'),
    steps: [
      S(L('Reported', 'مُبلّغ', 'Signalé'), L('Capture the infection, its type, unit, onset and organism.', 'سجّل العدوى ونوعها والوحدة وتاريخ البدء والكائن المسبب.', 'Consignez l’infection, son type, l’unité, la date de début et le micro-organisme.')),
      S(L('Reviewed', 'تمت المراجعة', 'Examiné'), L('Infection control reviews the case and records findings.', 'تراجع مكافحة العدوى الحالة وتسجّل النتائج.', 'La prévention des infections examine le cas et consigne les constats.')),
      S(L('Closed', 'مغلق', 'Clôturé'), L('Closed and frozen once review is complete.', 'يُغلق ويُجمّد بعد اكتمال المراجعة.', 'Clôturé et gelé une fois l’examen terminé.')),
    ],
    usage: [
      L('Report an HAI case (CLABSI, CAUTI, VAP or SSI); record device exposures — central lines, urinary catheters, ventilators — which accrue the device-days denominator.',
        'أبلغ عن حالة عدوى (CLABSI أو CAUTI أو VAP أو SSI)؛ وسجّل استخدام الأجهزة — القساطر الوريدية المركزية والبولية وأجهزة التنفس — التي تُراكم مقام أيام الأجهزة.',
        'Signalez un cas d’IAS (CLABSI, CAUTI, PAVM ou ISO) ; enregistrez les expositions aux dispositifs — cathéters centraux, sondes urinaires, ventilateurs — qui constituent le dénominateur en jours-dispositif.'),
      L('The tiles show device-associated rates per 1,000 device-days plus the SSI count; the device register shows lines in place and lets you record removal (which stops the device-day clock).',
        'تعرض البطاقات المعدلات المرتبطة بالأجهزة لكل 1000 يوم جهاز إضافةً إلى عدد حالات عدوى الموقع الجراحي؛ ويعرض سجل الأجهزة القساطر قيد الاستخدام ويتيح تسجيل الإزالة التي توقف احتساب أيام الجهاز.',
        'Les tuiles affichent les taux liés aux dispositifs pour 1 000 jours-dispositif ainsi que le nombre d’ISO ; le registre montre les dispositifs en place et permet d’enregistrer le retrait (qui arrête le décompte des jours-dispositif).'),
      L('Review records infection-control findings; closing freezes the case for audit.',
        'تسجّل المراجعة نتائج مكافحة العدوى؛ ويُجمّد الإغلاق الحالة للتدقيق.',
        'L’examen consigne les constats ; la clôture gèle le cas pour l’audit.'),
    ],
  },
  {
    route: '/mortality-review', titleKey: 'nav.mortalityReview', groupKey: 'nav.groupClinical', icon: 'mortalityReview',
    summary: L(
      'Mortality & morbidity peer review: deaths are classified (expected / unexpected / potentially-preventable / preventable); any non-expected death needs an independent second review by a different reviewer and committee discussion before closure. A complication register captures morbidity such as returns to theatre and unplanned ICU admissions. The mortality rate is per 1,000 patient-days from the ADT denominator.',
      'مراجعة الأقران للوفيات والمراضة: تُصنَّف الوفيات (متوقعة/غير متوقعة/يُحتمل تجنبها/يمكن تجنبها)؛ وتتطلب أي وفاة غير متوقعة مراجعة ثانية مستقلة بمراجع مختلف ومناقشة باللجنة قبل الإغلاق. يلتقط سجل المضاعفات المراضة مثل العودة لغرفة العمليات ودخول العناية غير المخطط. معدل الوفيات لكل 1000 يوم مريض من مقام ADT.',
      'Revue par les pairs de la mortalité et de la morbidité : les décès sont classés (attendu / inattendu / potentiellement évitable / évitable) ; tout décès non attendu exige une seconde revue indépendante par un autre relecteur et une discussion en comité avant clôture. Un registre des complications capture la morbidité (reprises au bloc, admissions SI non planifiées). Le taux de mortalité est pour 1 000 jours-patients à partir du dénominateur ADT.'),
    steps: [
      S(L('Reported', 'مُبلّغ', 'Signalé'), L('Capture the death — patient, unit, date and primary diagnosis.', 'سجّل الوفاة — المريض والوحدة والتاريخ والتشخيص الأساسي.', 'Consignez le décès — patient, unité, date et diagnostic principal.')),
      S(L('Classified', 'مُصنّف', 'Classé'), L('First reviewer classifies the death and records findings.', 'يصنّف المراجع الأول الوفاة ويسجّل النتائج.', 'Le premier relecteur classe le décès et consigne les constats.')),
      S(L('Second review', 'مراجعة ثانية', 'Seconde revue'), L('A different reviewer independently reviews a non-expected death.', 'يراجع مراجع مختلف بشكل مستقل الوفاة غير المتوقعة.', 'Un autre relecteur revoit indépendamment un décès non attendu.')),
      S(L('Committee', 'اللجنة', 'Comité'), L('The M&M committee discusses and records learnings.', 'تناقش لجنة الوفيات والمراضة وتسجّل الدروس.', 'Le comité de M&M discute et consigne les enseignements.')),
      S(L('Closed', 'مغلق', 'Clôturé'), L('Closed and frozen for audit.', 'يُغلق ويُجمّد للتدقيق.', 'Clôturé et gelé pour l’audit.')),
    ],
    usage: [
      L('Report a death, then classify it; an expected death may close after classification, while any non-expected death must pass an independent second review and committee discussion.',
        'أبلغ عن وفاة ثم صنّفها؛ يمكن إغلاق الوفاة المتوقعة بعد التصنيف، بينما يجب أن تمر أي وفاة غير متوقعة بمراجعة ثانية مستقلة ومناقشة باللجنة.',
        'Signalez un décès puis classez-le ; un décès attendu peut être clôturé après classification, tandis qu’un décès non attendu doit passer une seconde revue indépendante et une discussion en comité.'),
      L('The second review must be recorded by a reviewer other than the one who classified the case (segregation of duties).',
        'يجب أن تُسجّل المراجعة الثانية بواسطة مراجع غير الذي صنّف الحالة (فصل المهام).',
        'La seconde revue doit être enregistrée par un relecteur autre que celui qui a classé le cas (séparation des tâches).'),
      L('The complication register captures morbidity with a preventability judgement; the tiles show the mortality rate and (potentially) preventable death counts.',
        'يلتقط سجل المضاعفات المراضة مع حكم على إمكانية التجنب؛ تعرض البطاقات معدل الوفيات وأعداد الوفيات (المحتمل) تجنبها.',
        'Le registre des complications capture la morbidité avec un jugement d’évitabilité ; les tuiles affichent le taux de mortalité et le nombre de décès (potentiellement) évitables.'),
    ],
  },
  {
    route: '/eoc', titleKey: 'nav.eoc', groupKey: 'nav.groupClinical', icon: 'eoc',
    summary: L(
      'Environment of care and emergency preparedness: schedule environmental safety rounds, log findings by severity and resolve them, and run emergency drills (fire, evacuation, code blue, …) through a scheduled → executed → evaluated cycle with an effectiveness score. The dashboard tracks round completion, the open-findings backlog and drill effectiveness.',
      'بيئة الرعاية والتأهب للطوارئ: جدول جولات السلامة البيئية، وسجّل الملاحظات حسب الشدة وحلّها، ونفّذ تمارين الطوارئ (حريق، إخلاء، الكود الأزرق، …) عبر دورة مجدولة ← منفّذة ← مُقيّمة بدرجة فعالية. تتابع اللوحة إتمام الجولات وتراكم الملاحظات المفتوحة وفعالية التمارين.',
      'Environnement de soins et préparation aux urgences : planifiez les rondes de sécurité, consignez les constats par gravité et résolvez-les, et menez des exercices d’urgence (incendie, évacuation, code bleu, …) selon un cycle planifié → exécuté → évalué avec un score d’efficacité. Le tableau de bord suit l’achèvement des rondes, l’arriéré de constats ouverts et l’efficacité des exercices.'),
    steps: [
      S(L('Scheduled', 'مجدولة', 'Planifié'), L('Schedule a safety round for an area, or an emergency drill.', 'جدول جولة سلامة لمنطقة، أو تمرين طوارئ.', 'Planifiez une ronde pour une zone, ou un exercice d’urgence.')),
      S(L('In progress / Executed', 'قيد التنفيذ', 'En cours / Exécuté'), L('Walk the round logging findings; or execute the drill with participants.', 'نفّذ الجولة مع تسجيل الملاحظات؛ أو نفّذ التمرين مع المشاركين.', 'Réalisez la ronde en consignant les constats ; ou exécutez l’exercice avec les participants.')),
      S(L('Completed / Evaluated', 'مكتملة / مُقيّمة', 'Terminé / Évalué'), L('Complete the round; evaluate the drill with a score and improvement notes.', 'أكمل الجولة؛ قيّم التمرين بدرجة وملاحظات تحسين.', 'Terminez la ronde ; évaluez l’exercice avec un score et des notes d’amélioration.')),
    ],
    usage: [
      L('Start a scheduled round to log findings (each with a severity), resolve findings with a corrective note, then complete the round.',
        'ابدأ جولة مجدولة لتسجيل الملاحظات (كل منها بشدة)، وحلّ الملاحظات بملاحظة تصحيحية، ثم أكمل الجولة.',
        'Démarrez une ronde planifiée pour consigner les constats (chacun avec une gravité), résolvez-les avec une note corrective, puis terminez la ronde.'),
      L('Execute a drill (recording participants), then evaluate it with a 0–100 score; the score sets the effectiveness tier (Effective / Partially / Ineffective).',
        'نفّذ التمرين (مع تسجيل المشاركين)، ثم قيّمه بدرجة من 0 إلى 100؛ تحدد الدرجة مستوى الفعالية (فعّال / جزئيًا / غير فعّال).',
        'Exécutez un exercice (en enregistrant les participants), puis évaluez-le avec un score de 0 à 100 ; le score définit le niveau d’efficacité (Efficace / Partiel / Inefficace).'),
      L('The dashboard rolls up completed rounds, the open- and critical-findings backlog, and drills evaluated with their mean score.',
        'تلخّص اللوحة الجولات المكتملة وتراكم الملاحظات المفتوحة والحرجة والتمارين المُقيّمة بمتوسط درجاتها.',
        'Le tableau de bord récapitule les rondes terminées, l’arriéré de constats ouverts et critiques, et les exercices évalués avec leur score moyen.'),
    ],
  },
  {
    route: '/training-catalogue', titleKey: 'nav.trainingCatalogue', groupKey: 'nav.groupPeople', icon: 'trainingCatalogue',
    summary: L(
      'The training course catalogue and its delivery: define reusable courses with a pass mark and validity, schedule sessions, register trainees and capture pre/post assessment scores, then read the effectiveness roll-up and the compliance dashboard. This is distinct from the individual training-assignment queue.',
      'فهرس الدورات التدريبية وتنفيذها: عرّف دورات قابلة لإعادة الاستخدام بدرجة نجاح وصلاحية، وجدول الجلسات، وسجّل المتدربين والتقييمات القبلية/البعدية، ثم اطّلع على ملخص الفعالية ولوحة الامتثال. يختلف هذا عن قائمة مهام التدريب الفردية.',
      'Le catalogue des cours et leur déploiement : définissez des cours réutilisables avec un seuil de réussite et une validité, planifiez des sessions, inscrivez les stagiaires et saisissez les scores avant/après, puis consultez la synthèse d’efficacité et le tableau de conformité. Distinct de la file d’attente des affectations individuelles.'),
    steps: [
      S(L('Draft', 'مسودة', 'Brouillon'), L('Define the course — title, category, duration, pass mark and validity.', 'عرّف الدورة — العنوان والفئة والمدة ودرجة النجاح والصلاحية.', 'Définissez le cours — intitulé, catégorie, durée, seuil et validité.')),
      S(L('Active', 'نشط', 'Actif'), L('Activate the course, then schedule and deliver sessions.', 'فعّل الدورة ثم جدول الجلسات ونفّذها.', 'Activez le cours, puis planifiez et animez des sessions.')),
      S(L('Retired', 'متقاعد', 'Retiré'), L('Retire the course when it is superseded.', 'تقاعد الدورة عند استبدالها.', 'Retirez le cours lorsqu’il est remplacé.')),
    ],
    usage: [
      L('Define a course and activate it; each session registers trainees, is held, then attendance and pre/post scores are recorded and the session is closed.',
        'عرّف دورة وفعّلها؛ تُسجّل كل جلسة المتدربين وتُعقد ثم يُسجّل الحضور والتقييمات القبلية/البعدية وتُغلق الجلسة.',
        'Définissez un cours et activez-le ; chaque session inscrit les stagiaires, est tenue, puis la présence et les scores avant/après sont enregistrés et la session clôturée.'),
      L('Passing requires attendance and a post-assessment at or above the pass mark; the course shows the pass rate, mean pre/post scores and the mean gain.',
        'يتطلب النجاح الحضور وتقييمًا بعديًا عند درجة النجاح أو أعلى؛ تعرض الدورة معدل النجاح ومتوسط الدرجات القبلية/البعدية ومتوسط التحسن.',
        'La réussite exige la présence et un score après-formation au moins égal au seuil ; le cours affiche le taux de réussite, les moyennes avant/après et le gain moyen.'),
      L('The compliance summary rolls up, per active course, the sessions held, distinct trainees reached and how many passed.',
        'يلخّص ملخص الامتثال لكل دورة نشطة الجلسات المعقودة وعدد المتدربين المميزين وعدد الناجحين.',
        'La synthèse de conformité récapitule, par cours actif, les sessions tenues, les stagiaires distincts atteints et le nombre de réussites.'),
    ],
  },
  {
    route: '/credentialing', titleKey: 'nav.credentialing', groupKey: 'nav.groupClinical', icon: 'credentialing',
    summary: L(
      'Practitioner credentialing and privileging: hold licences and certifications (each primary-source verified), delineate and grant clinical privileges, appoint and reappoint on a cycle, and suspend when needed. A tiered licence-expiry register chases renewals, and a point-of-care check confirms whether a practitioner holds an active privilege right now.',
      'اعتماد الممارسين ومنح الامتيازات: الاحتفاظ بالتراخيص والشهادات (كل منها موثّق من المصدر)، وتحديد ومنح الامتيازات السريرية، والتعيين وإعادة التعيين دوريًا، والإيقاف عند الحاجة. يتابع سجل انتهاء التراخيص المتدرّج التجديدات، ويؤكد التحقق عند نقطة الرعاية ما إذا كان الممارس يحمل امتيازًا نشطًا الآن.',
      'Accréditation et privilèges des praticiens : détenez licences et certifications (chacune vérifiée à la source), délimitez et accordez les privilèges cliniques, nommez et renouvelez selon un cycle, et suspendez au besoin. Un registre d’expiration à niveaux relance les renouvellements, et une vérification au point de soins confirme si un praticien détient un privilège actif à l’instant.'),
    steps: [
      S(L('Pending', 'قيد الانتظار', 'En attente'), L('Register the practitioner; add licences and request privileges.', 'سجّل الممارس؛ أضف التراخيص واطلب الامتيازات.', 'Enregistrez le praticien ; ajoutez les licences et demandez les privilèges.')),
      S(L('Verified', 'موثّق', 'Vérifié'), L('Primary-source-verify each licence; the committee grants privileges.', 'وثّق كل ترخيص من المصدر؛ تمنح اللجنة الامتيازات.', 'Vérifiez chaque licence à la source ; le comité accorde les privilèges.')),
      S(L('Credentialed', 'معتمد', 'Accrédité'), L('Appoint the practitioner once evidence is in place; reappoint on cycle.', 'عيّن الممارس بعد اكتمال الأدلة؛ أعد التعيين دوريًا.', 'Nommez le praticien une fois les preuves réunies ; renouvelez selon le cycle.')),
    ],
    usage: [
      L('Add a licence, then primary-source-verify it (recording the source checked); request a privilege, which the committee grants (optionally time-limited) or denies with a reason.',
        'أضف ترخيصًا ثم وثّقه من المصدر (مع تسجيل المصدر الذي رُوجع)؛ اطلب امتيازًا تمنحه اللجنة (بمدة اختيارية) أو ترفضه مع ذكر السبب.',
        'Ajoutez une licence puis vérifiez-la à la source (en consignant la source contrôlée) ; demandez un privilège que le comité accorde (éventuellement limité dans le temps) ou refuse avec un motif.'),
      L('A practitioner can be credentialed only with at least one verified licence and one granted privilege; reappointment renews the appointment, and suspension removes all active privileges.',
        'لا يمكن اعتماد الممارس إلا بترخيص موثّق واحد وامتياز ممنوح واحد على الأقل؛ تجدد إعادة التعيين التعيين، ويزيل الإيقاف جميع الامتيازات النشطة.',
        'Un praticien ne peut être accrédité qu’avec au moins une licence vérifiée et un privilège accordé ; le renouvellement prolonge la nomination et la suspension retire tous les privilèges actifs.'),
      L('The expiry register tiers licences (Expired / Critical ≤30d / Warning ≤90d); the point-of-care check answers whether a privilege is active today.',
        'يصنّف سجل الانتهاء التراخيص (منتهٍ / حرج ≤30 يومًا / تحذير ≤90 يومًا)؛ ويجيب التحقق عند نقطة الرعاية عمّا إذا كان الامتياز نشطًا اليوم.',
        'Le registre d’expiration classe les licences (Expiré / Critique ≤30j / Alerte ≤90j) ; la vérification au point de soins indique si un privilège est actif aujourd’hui.'),
    ],
  },
  {
    route: '/nonconformances', titleKey: 'nav.nc', groupKey: 'nav.groupImprovement', icon: 'nc',
    summary: L(
      'The nonconformance & CAPA register: log a problem, find its root cause, plan corrective action, then verify the fix worked and was effective before closing.',
      'سجل حالات عدم المطابقة والإجراءات التصحيحية: سجّل المشكلة، وجد السبب الجذري، وخطّط الإجراء التصحيحي، ثم تحقّق من نجاح الإصلاح وفعاليته قبل الإغلاق.',
      'Le registre des non-conformités et CAPA : enregistrez un problème, trouvez sa cause racine, planifiez l’action corrective, puis vérifiez l’efficacité avant clôture.'),
    steps: [
      S(L('Logged', 'مُسجّلة', 'Enregistrée'), L('Capture what happened, where and how severe.', 'سجّل ما حدث وأين ومدى خطورته.', 'Consignez ce qui s’est passé, où et la gravité.')),
      S(L('Root Cause', 'السبب الجذري', 'Cause racine'), L('Investigate the true cause (e.g. 5-Why, fishbone).', 'حقّق في السبب الحقيقي (مثل 5 لماذا).', 'Recherchez la cause réelle (5 pourquoi, Ishikawa).')),
      S(L('CAPA', 'إجراء تصحيحي', 'CAPA'), L('Plan and implement corrective/preventive action.', 'خطّط ونفّذ الإجراء التصحيحي/الوقائي.', 'Planifiez et mettez en œuvre l’action corrective/préventive.')),
      S(L('Verification', 'التحقّق', 'Vérification'), L('Confirm the action was carried out.', 'أكّد أن الإجراء نُفّذ.', 'Confirmez que l’action a été réalisée.')),
      S(L('Effectiveness', 'الفعالية', 'Efficacité'), L('Later, confirm the problem did not recur.', 'لاحقًا، أكّد عدم تكرار المشكلة.', 'Plus tard, confirmez la non-récurrence.')),
      S(L('Closed', 'مغلقة', 'Clôturée'), L('The record is closed and frozen for audit.', 'يُغلق السجل ويُجمّد للتدقيق.', 'L’enregistrement est clôturé et gelé pour l’audit.')),
    ],
    usage: [
      L('Click New to log the nonconformance with severity and source.',
        'انقر «جديد» لتسجيل عدم المطابقة مع الخطورة والمصدر.',
        'Cliquez sur Nouveau pour enregistrer la non-conformité avec sa gravité et sa source.'),
      L('Record the root-cause analysis, then plan CAPA actions with owners and due dates.',
        'سجّل تحليل السبب الجذري، ثم خطّط إجراءات CAPA بأصحابها وتواريخها.',
        'Consignez l’analyse de cause racine, puis planifiez les CAPA avec responsables et échéances.'),
      L('Verify completion, then run the effectiveness check before the record can close.',
        'تحقّق من الإنجاز، ثم نفّذ فحص الفعالية قبل أن يُغلق السجل.',
        'Vérifiez l’achèvement, puis réalisez le contrôle d’efficacité avant clôture.'),
    ],
  },
  {
    route: '/complaints', titleKey: 'nav.complaints', groupKey: 'nav.groupImprovement', icon: 'complaints',
    summary: L(
      'Customer/patient complaint handling: capture the complaint, assess and investigate it, resolve it and communicate the outcome — raising a nonconformance where needed.',
      'إدارة شكاوى العملاء/المرضى: التقاط الشكوى وتقييمها والتحقيق فيها وحلّها وإبلاغ النتيجة — مع فتح حالة عدم مطابقة عند الحاجة.',
      'Gestion des réclamations clients/patients : saisir, évaluer, investiguer, résoudre et communiquer le résultat — en ouvrant une non-conformité si nécessaire.'),
    steps: [
      S(L('Received', 'مستلمة', 'Reçue'), L('Log the complaint and complainant details.', 'سجّل الشكوى وبيانات مقدّمها.', 'Enregistrez la réclamation et le plaignant.')),
      S(L('Under Review', 'قيد المراجعة', 'En revue'), L('Assess validity and impact.', 'قيّم الصحة والأثر.', 'Évaluez la validité et l’impact.')),
      S(L('Investigation', 'التحقيق', 'Investigation'), L('Investigate; link a nonconformance if warranted.', 'حقّق؛ اربط عدم مطابقة إن لزم.', 'Investiguez ; liez une non-conformité si justifié.')),
      S(L('Resolved', 'محلولة', 'Résolue'), L('Resolve and communicate to the complainant.', 'احلل وأبلغ مقدّم الشكوى.', 'Résolvez et informez le plaignant.')),
      S(L('Closed', 'مغلقة', 'Clôturée'), L('Close the record for audit.', 'أغلق السجل للتدقيق.', 'Clôturez l’enregistrement pour l’audit.')),
    ],
    usage: [
      L('Click New to capture the complaint, its channel and the complainant.',
        'انقر «جديد» لالتقاط الشكوى وقناتها ومقدّمها.',
        'Cliquez sur Nouveau pour saisir la réclamation, son canal et le plaignant.'),
      L('Assess, investigate, then record the resolution and any linked nonconformance.',
        'قيّم وحقّق ثم سجّل الحل وأي عدم مطابقة مرتبطة.',
        'Évaluez, investiguez, puis consignez la résolution et toute non-conformité liée.'),
    ],
  },
  {
    route: '/feedback', titleKey: 'nav.feedback', groupKey: 'nav.groupImprovement', icon: 'feedback',
    summary: L(
      'Customer feedback and satisfaction input: capture feedback, review it, and turn useful signals into improvement actions.',
      'ملاحظات العملاء ومدخلات الرضا: التقاط الملاحظات ومراجعتها وتحويل الإشارات المفيدة إلى إجراءات تحسين.',
      'Retours et satisfaction client : saisir les retours, les examiner et transformer les signaux utiles en actions d’amélioration.'),
    steps: [
      S(L('Captured', 'ملتقَطة', 'Saisie'), L('Record the feedback and its source.', 'سجّل الملاحظة ومصدرها.', 'Enregistrez le retour et sa source.')),
      S(L('Reviewed', 'مُراجَعة', 'Examinée'), L('Review and categorize the feedback.', 'راجع الملاحظة وصنّفها.', 'Examinez et catégorisez le retour.')),
      S(L('Actioned', 'مُعالَجة', 'Traitée'), L('Raise improvement actions where useful.', 'افتح إجراءات تحسين عند الفائدة.', 'Lancez des actions d’amélioration si utile.')),
    ],
    usage: [
      L('Click New to capture feedback; tag its theme and sentiment.',
        'انقر «جديد» لالتقاط الملاحظة؛ حدّد موضوعها وطابعها.',
        'Cliquez sur Nouveau pour saisir un retour ; taguez son thème et son sentiment.'),
      L('Review periodically and feed trends into management review.',
        'راجع دوريًا وادمج الاتجاهات في مراجعة الإدارة.',
        'Examinez périodiquement et alimentez la revue de direction avec les tendances.'),
    ],
  },
  {
    route: '/audits', titleKey: 'nav.audits', groupKey: 'nav.groupImprovement', icon: 'audits',
    summary: L(
      'Internal audit programme: plan and schedule audits, conduct fieldwork, log findings (which can spawn nonconformances), issue the report and close out.',
      'برنامج التدقيق الداخلي: تخطيط الدقيقات وجدولتها، وتنفيذ العمل الميداني، وتسجيل النتائج (التي قد تُنشئ حالات عدم مطابقة)، وإصدار التقرير والإغلاق.',
      'Programme d’audit interne : planifier et programmer, réaliser le terrain, consigner les constats (qui peuvent créer des non-conformités), émettre le rapport et clôturer.'),
    steps: [
      S(L('Planned', 'مخطّط', 'Planifié'), L('Define scope, criteria and auditors.', 'حدّد النطاق والمعايير والمدققين.', 'Définissez périmètre, critères et auditeurs.')),
      S(L('Scheduled', 'مجدول', 'Programmé'), L('Set dates and notify auditees.', 'حدّد المواعيد وبلّغ الجهات.', 'Fixez les dates et informez les audités.')),
      S(L('Fieldwork', 'العمل الميداني', 'Terrain'), L('Conduct the audit and gather evidence.', 'نفّذ التدقيق واجمع الأدلة.', 'Réalisez l’audit et collectez les preuves.')),
      S(L('Findings', 'النتائج', 'Constats'), L('Log findings; raise nonconformances as needed.', 'سجّل النتائج؛ افتح عدم مطابقة عند الحاجة.', 'Consignez les constats ; ouvrez des non-conformités si besoin.')),
      S(L('Reported', 'مُبلَّغ', 'Rapporté'), L('Issue the audit report.', 'أصدر تقرير التدقيق.', 'Émettez le rapport d’audit.')),
      S(L('Closed', 'مغلق', 'Clôturé'), L('Close once all findings are resolved.', 'أغلق بعد حل كل النتائج.', 'Clôturez une fois tous les constats résolus.')),
    ],
    usage: [
      L('Click New to plan an audit with scope, criteria and assigned auditors.',
        'انقر «جديد» لتخطيط تدقيق بنطاقه ومعاييره ومدققيه.',
        'Cliquez sur Nouveau pour planifier un audit avec périmètre, critères et auditeurs.'),
      L('During fieldwork, log each finding — a finding can create a linked nonconformance automatically.',
        'أثناء العمل الميداني، سجّل كل نتيجة — قد تُنشئ النتيجة عدم مطابقة مرتبطة تلقائيًا.',
        'Pendant le terrain, consignez chaque constat — un constat peut créer automatiquement une non-conformité liée.'),
      L('Issue the report and close the audit when findings are cleared.',
        'أصدر التقرير وأغلق التدقيق عند معالجة النتائج.',
        'Émettez le rapport et clôturez l’audit une fois les constats traités.'),
    ],
  },
  {
    route: '/quality-objectives', titleKey: 'nav.objectives', groupKey: 'nav.groupImprovement', icon: 'objectives',
    summary: L(
      'Quality objectives & KPIs: define measurable objectives, track them against targets, and review achievement at management review.',
      'أهداف الجودة ومؤشراتها: تعريف أهداف قابلة للقياس، وتتبّعها مقابل الغايات، ومراجعة تحقيقها في مراجعة الإدارة.',
      'Objectifs qualité et KPI : définir des objectifs mesurables, les suivre par rapport aux cibles et examiner l’atteinte en revue de direction.'),
    steps: [
      S(L('Defined', 'مُعرّف', 'Défini'), L('Set the objective, target and measure.', 'حدّد الهدف والغاية والمقياس.', 'Fixez l’objectif, la cible et la mesure.')),
      S(L('Active', 'نشِط', 'Actif'), L('Track progress against the target.', 'تتبّع التقدّم مقابل الغاية.', 'Suivez la progression vers la cible.')),
      S(L('Measured', 'مُقاس', 'Mesuré'), L('Record the achieved value.', 'سجّل القيمة المحققة.', 'Enregistrez la valeur atteinte.')),
      S(L('Reviewed', 'مُراجَع', 'Revu'), L('Review at management review.', 'راجع في مراجعة الإدارة.', 'Examinez en revue de direction.')),
    ],
    usage: [
      L('Click New to define an objective with its numeric target and measurement period.',
        'انقر «جديد» لتعريف هدف بغايته الرقمية وفترة قياسه.',
        'Cliquez sur Nouveau pour définir un objectif avec sa cible chiffrée et sa période.'),
      L('Update the achieved value over time; status shows on-track or at-risk.',
        'حدّث القيمة المحققة مع الوقت؛ تُظهر الحالة على المسار أو في خطر.',
        'Mettez à jour la valeur atteinte ; le statut indique en bonne voie ou à risque.'),
    ],
  },
  {
    route: '/changes', titleKey: 'nav.changes', groupKey: 'nav.groupImprovement', icon: 'changes',
    summary: L(
      'Change control: request a change, assess its impact and risk, obtain approval, implement it and verify the outcome — keeping a controlled trail.',
      'ضبط التغيير: طلب تغيير، وتقييم أثره ومخاطره، والحصول على الموافقة، وتنفيذه، والتحقق من نتيجته — مع أثر مضبوط.',
      'Maîtrise des modifications : demander un changement, évaluer impact et risque, obtenir l’approbation, mettre en œuvre et vérifier — avec une traçabilité maîtrisée.'),
    steps: [
      S(L('Requested', 'مطلوب', 'Demandé'), L('Raise the change with its rationale.', 'اطرح التغيير مع مبرراته.', 'Proposez le changement avec sa justification.')),
      S(L('Assessed', 'مُقيَّم', 'Évalué'), L('Assess impact, risk and resources.', 'قيّم الأثر والمخاطر والموارد.', 'Évaluez impact, risque et ressources.')),
      S(L('Approved', 'معتمد', 'Approuvé'), L('Authorized decision to proceed.', 'قرار مخوّل بالمضي.', 'Décision autorisée de procéder.')),
      S(L('Implemented', 'مُنفّذ', 'Mis en œuvre'), L('Carry out the change.', 'نفّذ التغيير.', 'Réalisez le changement.')),
      S(L('Verified', 'مُتحقَّق', 'Vérifié'), L('Confirm the intended outcome and close.', 'أكّد النتيجة المقصودة وأغلق.', 'Confirmez le résultat visé et clôturez.')),
    ],
    usage: [
      L('Click New to request a change with scope and justification.',
        'انقر «جديد» لطلب تغيير بنطاقه ومبرره.',
        'Cliquez sur Nouveau pour demander un changement avec périmètre et justification.'),
      L('Assess impact and route for approval before implementation; verify after.',
        'قيّم الأثر ووجّه للاعتماد قبل التنفيذ؛ وتحقّق بعده.',
        'Évaluez l’impact et faites approuver avant mise en œuvre ; vérifiez ensuite.'),
      L('Set the impact level — a high-impact change cannot be approved by its own proposer. For an urgent change already implemented, use Emergency change: it is recorded for retrospective ratification (a signed step) by a deadline, then follows the normal post-implementation review.',
        'حدّد مستوى الأثر — لا يمكن لمقترح التغيير عالي الأثر اعتماده بنفسه. وللتغيير العاجل المُنفّذ بالفعل، استخدم «تغيير طارئ»: يُسجَّل للمصادقة الرجعية (خطوة موقّعة) بحلول موعد نهائي، ثم يتبع المراجعة اللاحقة المعتادة.',
        'Définissez le niveau d’impact — un changement à fort impact ne peut être approuvé par son propre auteur. Pour un changement urgent déjà mis en œuvre, utilisez Changement urgent : il est enregistré pour ratification rétrospective (étape signée) avant une échéance, puis suit la revue post-implémentation habituelle.'),
    ],
  },
  {
    route: '/management-reviews', titleKey: 'nav.reviews', groupKey: 'nav.groupImprovement', icon: 'reviews',
    summary: L(
      'Management review: schedule the review, compile the standard inputs (audits, NCs, objectives, feedback), hold the meeting and record decisions and actions.',
      'مراجعة الإدارة: جدولة المراجعة، وتجميع المدخلات القياسية (تدقيقات، عدم مطابقة، أهداف، ملاحظات)، وعقد الاجتماع، وتسجيل القرارات والإجراءات.',
      'Revue de direction : programmer la revue, compiler les entrées standard (audits, NC, objectifs, retours), tenir la réunion et consigner décisions et actions.'),
    steps: [
      S(L('Scheduled', 'مجدولة', 'Programmée'), L('Set the date and participants.', 'حدّد الموعد والمشاركين.', 'Fixez la date et les participants.')),
      S(L('Inputs Compiled', 'المدخلات مُجمّعة', 'Entrées compilées'), L('Gather the required review inputs.', 'اجمع مدخلات المراجعة المطلوبة.', 'Rassemblez les entrées requises.')),
      S(L('Meeting Held', 'عُقد الاجتماع', 'Réunion tenue'), L('Discuss inputs and reach decisions.', 'ناقش المدخلات واتخذ القرارات.', 'Discutez des entrées et décidez.')),
      S(L('Actions Assigned', 'الإجراءات مُسندة', 'Actions assignées'), L('Assign output actions with owners.', 'أسند إجراءات المخرجات بأصحابها.', 'Attribuez les actions de sortie avec responsables.')),
      S(L('Closed', 'مغلقة', 'Clôturée'), L('Close once outputs are recorded.', 'أغلق بعد تسجيل المخرجات.', 'Clôturez une fois les sorties consignées.')),
    ],
    usage: [
      L('Click New to schedule a review; the standard ISO input agenda is pre-listed.',
        'انقر «جديد» لجدولة مراجعة؛ جدول أعمال المدخلات القياسي مُدرج مسبقًا.',
        'Cliquez sur Nouveau pour programmer une revue ; l’ordre du jour ISO standard est pré-listé.'),
      L('Record decisions and assign output actions, which appear in owners’ task queues.',
        'سجّل القرارات وأسند إجراءات المخرجات، وتظهر في قوائم مهام أصحابها.',
        'Consignez les décisions et assignez les actions, qui apparaissent dans les files des responsables.'),
    ],
  },

  // ── Documents & records ───────────────────────────────────────────────────
  {
    route: '/quality-policy', titleKey: 'nav.qualityPolicy', groupKey: 'nav.groupDocs', icon: 'qualityPolicy',
    summary: L(
      'The controlled quality-policy statement (ISO 9001 \u00a75.2 / ISO 17025 \u00a78.2): versioned, approved by someone other than its author, with exactly one policy in force at a time and the full history retained.',
      '\u0628\u064a\u0627\u0646 \u0633\u064a\u0627\u0633\u0629 \u0627\u0644\u062c\u0648\u062f\u0629 \u0627\u0644\u0645\u0636\u0628\u0648\u0637 (ISO 9001 \u00a75.2 / ISO 17025 \u00a78.2): \u0645\u064f\u0631\u0642\u0651\u0645 \u0627\u0644\u0625\u0635\u062f\u0627\u0631\u0627\u062a\u060c \u064a\u0639\u062a\u0645\u062f\u0647 \u0634\u062e\u0635 \u063a\u064a\u0631 \u0645\u0624\u0644\u0641\u0647\u060c \u0645\u0639 \u0633\u064a\u0627\u0633\u0629 \u0648\u0627\u062d\u062f\u0629 \u0646\u0627\u0641\u0630\u0629 \u0641\u064a \u0643\u0644 \u0648\u0642\u062a \u0648\u0627\u062d\u062a\u0641\u0627\u0638 \u0643\u0627\u0645\u0644 \u0628\u0627\u0644\u062a\u0627\u0631\u064a\u062e.',
      'La d\u00e9claration de politique qualit\u00e9 ma\u00eetris\u00e9e (ISO 9001 \u00a75.2 / ISO 17025 \u00a78.2) : versionn\u00e9e, approuv\u00e9e par une personne autre que son auteur, une seule politique en vigueur \u00e0 la fois et tout l\u2019historique conserv\u00e9.'),
    steps: [
      S(L('Draft', '\u0645\u0633\u0648\u062f\u0629', 'Brouillon'), L('Author the next version of the statement; a draft can still be edited.', '\u062d\u0631\u0651\u0631 \u0627\u0644\u0625\u0635\u062f\u0627\u0631 \u0627\u0644\u062a\u0627\u0644\u064a \u0644\u0644\u0628\u064a\u0627\u0646\u061b \u0627\u0644\u0645\u0633\u0648\u062f\u0629 \u0642\u0627\u0628\u0644\u0629 \u0644\u0644\u062a\u0639\u062f\u064a\u0644.', 'R\u00e9digez la prochaine version ; un brouillon reste modifiable.')),
      S(L('Active', '\u0646\u0627\u0641\u0630\u0629', 'Active'), L('Approved by a second person (segregation of duties) and in force from its effective date.', '\u064a\u0639\u062a\u0645\u062f\u0647\u0627 \u0634\u062e\u0635 \u062b\u0627\u0646 (\u0641\u0635\u0644 \u0627\u0644\u0645\u0647\u0627\u0645) \u0648\u062a\u0633\u0631\u064a \u0645\u0646 \u062a\u0627\u0631\u064a\u062e \u0646\u0641\u0627\u0630\u0647\u0627.', 'Approuv\u00e9e par une seconde personne (s\u00e9paration des t\u00e2ches), en vigueur \u00e0 sa date d\u2019effet.')),
      S(L('Superseded', '\u0645\u0633\u062a\u0628\u062f\u0644\u0629', 'Remplac\u00e9e'), L('Retired automatically when the next version is approved; history stays readable.', '\u062a\u064f\u0633\u062d\u0628 \u062a\u0644\u0642\u0627\u0626\u064a\u0627\u064b \u0639\u0646\u062f \u0627\u0639\u062a\u0645\u0627\u062f \u0627\u0644\u0625\u0635\u062f\u0627\u0631 \u0627\u0644\u062a\u0627\u0644\u064a\u061b \u0648\u064a\u0628\u0642\u0649 \u0627\u0644\u062a\u0627\u0631\u064a\u062e \u0645\u0642\u0631\u0648\u0621\u0627\u064b.', 'Retir\u00e9e automatiquement \u00e0 l\u2019approbation de la version suivante ; l\u2019historique reste lisible.')),
    ],
    usage: [
      L('Draft a new version, then have a different user approve it \u2014 the author cannot approve their own policy (SOD-QP-001).',
        '\u0623\u0646\u0634\u0626 \u0645\u0633\u0648\u062f\u0629 \u062c\u062f\u064a\u062f\u0629 \u062b\u0645 \u062f\u0639 \u0645\u0633\u062a\u062e\u062f\u0645\u0627\u064b \u0622\u062e\u0631 \u064a\u0639\u062a\u0645\u062f\u0647\u0627 \u2014 \u0644\u0627 \u064a\u062c\u0648\u0632 \u0644\u0644\u0645\u0624\u0644\u0641 \u0627\u0639\u062a\u0645\u0627\u062f \u0633\u064a\u0627\u0633\u062a\u0647 (SOD-QP-001).',
        'R\u00e9digez une nouvelle version puis faites-la approuver par un autre utilisateur \u2014 l\u2019auteur ne peut approuver sa propre politique (SOD-QP-001).'),
      L('An approved policy is immutable \u2014 a change is always a new version, never an edit in place.',
        '\u0627\u0644\u0633\u064a\u0627\u0633\u0629 \u0627\u0644\u0645\u0639\u062a\u0645\u062f\u0629 \u063a\u064a\u0631 \u0642\u0627\u0628\u0644\u0629 \u0644\u0644\u062a\u0639\u062f\u064a\u0644 \u2014 \u0627\u0644\u062a\u063a\u064a\u064a\u0631 \u0625\u0635\u062f\u0627\u0631 \u062c\u062f\u064a\u062f \u062f\u0627\u0626\u0645\u0627\u064b \u0648\u0644\u064a\u0633 \u062a\u0639\u062f\u064a\u0644\u0627\u064b \u0641\u064a \u0645\u0643\u0627\u0646\u0647.',
        'Une politique approuv\u00e9e est immuable \u2014 tout changement est une nouvelle version, jamais une modification sur place.'),
      L('Approval is an electronic signature: password and e-signature PIN, recorded with its meaning in the audit trail.',
        '\u0627\u0644\u0627\u0639\u062a\u0645\u0627\u062f \u062a\u0648\u0642\u064a\u0639 \u0625\u0644\u0643\u062a\u0631\u0648\u0646\u064a: \u0643\u0644\u0645\u0629 \u0627\u0644\u0645\u0631\u0648\u0631 \u0648\u0631\u0642\u0645 PIN\u060c \u0648\u064a\u0633\u062c\u0644 \u0628\u0645\u0639\u0646\u0627\u0647 \u0641\u064a \u0633\u062c\u0644 \u0627\u0644\u062a\u062f\u0642\u064a\u0642.',
        'L\u2019approbation est une signature \u00e9lectronique : mot de passe et code PIN, enregistr\u00e9e avec sa signification dans la piste d\u2019audit.'),
    ],
  },
  {
    route: '/documents', titleKey: 'nav.documents', groupKey: 'nav.groupDocs', icon: 'documents',
    summary: L(
      'Controlled document management: author a document, route it through review and approval, publish the effective version, and supersede or retire it over time — with full version history and e-signatures.',
      'إدارة الوثائق المضبوطة: تأليف وثيقة، وتمريرها عبر المراجعة والاعتماد، ونشر النسخة السارية، واستبدالها أو سحبها لاحقًا — مع سجل نسخ كامل وتوقيعات إلكترونية.',
      'Gestion documentaire maîtrisée : rédiger un document, le faire relire et approuver, publier la version en vigueur, puis la remplacer ou la retirer — avec historique complet et signatures électroniques.'),
    steps: [
      S(L('Draft', 'مسودة', 'Brouillon'), L('Author the document and upload the file.', 'ألّف الوثيقة وارفع الملف.', 'Rédigez le document et téléversez le fichier.')),
      S(L('In Review', 'قيد المراجعة', 'En revue'), L('Reviewers check content and comment.', 'يراجع المراجعون المحتوى ويعلّقون.', 'Les relecteurs vérifient et commentent.')),
      S(L('Approved', 'معتمدة', 'Approuvée'), L('An authorized approver e-signs.', 'يوقّع المعتمد المخوّل إلكترونيًا.', 'Un approbateur autorisé signe électroniquement.')),
      S(L('Effective', 'سارية', 'En vigueur'), L('The version becomes the controlled copy.', 'تصبح النسخة النسخة المضبوطة.', 'La version devient la copie maîtrisée.')),
      S(L('Retired', 'مسحوبة', 'Retirée'), L('Superseded by a new version or made obsolete.', 'مُستبدلة بنسخة جديدة أو مُلغاة.', 'Remplacée par une nouvelle version ou rendue obsolète.')),
    ],
    usage: [
      L('Click New to create a document and upload its file; a reference number is assigned.',
        'انقر «جديد» لإنشاء وثيقة ورفع ملفها؛ يُسند رقم مرجعي.',
        'Cliquez sur Nouveau pour créer un document et téléverser son fichier ; un numéro de référence est attribué.'),
      L('Route through review and approval; the approver e-signs to make the version effective.',
        'مرّر عبر المراجعة والاعتماد؛ يوقّع المعتمد لجعل النسخة سارية.',
        'Faites relire et approuver ; l’approbateur signe pour rendre la version en vigueur.'),
      L('Uploading a new version supersedes the old one, which is retained in history.',
        'رفع نسخة جديدة يستبدل القديمة، وتبقى محفوظة في السجل.',
        'Téléverser une nouvelle version remplace l’ancienne, conservée dans l’historique.'),
    ],
  },
  {
    route: '/records', titleKey: 'nav.records', groupKey: 'nav.groupDocs', icon: 'records',
    summary: L(
      'Quality records & retention: registered records are retained for their required period, flagged when due for review, and archived or disposed under control.',
      'السجلات النوعية والاحتفاظ: تُحفظ السجلات المسجّلة لمدتها المطلوبة، وتُعلَّم عند استحقاق المراجعة، وتُؤرشف أو يُتخلّص منها بضبط.',
      'Enregistrements qualité et rétention : les enregistrements sont conservés pour leur durée requise, signalés à l’échéance de revue, puis archivés ou éliminés sous contrôle.'),
    steps: [
      S(L('Captured', 'ملتقَط', 'Saisi'), L('Register the record and its retention class.', 'سجّل السجل وفئة احتفاظه.', 'Enregistrez l’enregistrement et sa classe de rétention.')),
      S(L('Retained', 'محفوظ', 'Conservé'), L('Held for the required retention period.', 'يُحفظ لمدة الاحتفاظ المطلوبة.', 'Conservé pendant la durée requise.')),
      S(L('Due', 'مستحق', 'Échu'), L('Flagged when the retention period elapses.', 'يُعلَّم عند انقضاء مدة الاحتفاظ.', 'Signalé à l’expiration de la rétention.')),
      S(L('Archived / Disposed', 'مؤرشف/مُتلَف', 'Archivé/Éliminé'), L('Archived or disposed under authorization.', 'يُؤرشف أو يُتلف بتخويل.', 'Archivé ou éliminé sur autorisation.')),
    ],
    usage: [
      L('Register a record with its retention class; the due date is derived automatically.',
        'سجّل سجلاً بفئة احتفاظه؛ يُشتق تاريخ الاستحقاق تلقائيًا.',
        'Enregistrez un enregistrement avec sa classe ; l’échéance est dérivée automatiquement.'),
      L('Act on due-for-disposition items; disposal requires authorization and is logged.',
        'تعامل مع العناصر المستحقة للتصرف؛ يتطلب الإتلاف تخويلاً ويُسجَّل.',
        'Traitez les éléments à disposer ; l’élimination requiert une autorisation et est journalisée.'),
    ],
  },

  // ── Risk & governance ─────────────────────────────────────────────────────
  {
    route: '/risks', titleKey: 'nav.risks', groupKey: 'nav.groupRisk', icon: 'risks',
    summary: L(
      'Risk register: identify risks and opportunities, score them (severity × likelihood → RPN), plan treatment, and monitor residual risk.',
      'سجل المخاطر: تحديد المخاطر والفرص، وتقييمها (الخطورة × الاحتمالية ← RPN)، وتخطيط المعالجة، ومراقبة المخاطر المتبقية.',
      'Registre des risques : identifier risques et opportunités, les coter (gravité × probabilité → RPN), planifier le traitement et surveiller le risque résiduel.'),
    steps: [
      S(L('Identified', 'محدّد', 'Identifié'), L('Describe the risk and its context.', 'صِف الخطر وسياقه.', 'Décrivez le risque et son contexte.')),
      S(L('Assessed', 'مُقيَّم', 'Évalué'), L('Score severity and likelihood → RPN.', 'قيّم الخطورة والاحتمالية ← RPN.', 'Cotez gravité et probabilité → RPN.')),
      S(L('Treatment', 'المعالجة', 'Traitement'), L('Plan actions to reduce the risk.', 'خطّط إجراءات لخفض الخطر.', 'Planifiez des actions de réduction.')),
      S(L('Monitored', 'مُراقَب', 'Surveillé'), L('Track residual risk over time.', 'تتبّع الخطر المتبقي مع الوقت.', 'Suivez le risque résiduel dans le temps.')),
    ],
    usage: [
      L('Click New to add a risk; set severity and likelihood to compute the RPN.',
        'انقر «جديد» لإضافة خطر؛ حدّد الخطورة والاحتمالية لحساب RPN.',
        'Cliquez sur Nouveau pour ajouter un risque ; définissez gravité et probabilité pour calculer le RPN.'),
      L('Plan treatment for high-RPN risks and re-score after mitigation.',
        'خطّط معالجة للمخاطر عالية RPN وأعد التقييم بعد التخفيف.',
        'Planifiez le traitement des risques à RPN élevé et recotez après atténuation.'),
    ],
  },
  {
    route: '/conflicts', titleKey: 'nav.coi', groupKey: 'nav.groupRisk', icon: 'coi',
    summary: L(
      'Impartiality & conflict-of-interest register: declare potential conflicts, review them, agree mitigations and monitor — protecting the laboratory’s impartiality.',
      'سجل الحياد وتضارب المصالح: إعلان التضاربات المحتملة، ومراجعتها، والاتفاق على التخفيفات، والمراقبة — لحماية حياد المختبر.',
      'Registre d’impartialité et conflits d’intérêts : déclarer les conflits potentiels, les examiner, convenir des mesures et surveiller — pour protéger l’impartialité.'),
    steps: [
      S(L('Declared', 'مُعلَن', 'Déclaré'), L('Declare the potential conflict.', 'أعلن التضارب المحتمل.', 'Déclarez le conflit potentiel.')),
      S(L('Reviewed', 'مُراجَع', 'Examiné'), L('Assess the threat to impartiality.', 'قيّم التهديد للحياد.', 'Évaluez la menace pour l’impartialité.')),
      S(L('Mitigated', 'مُخفَّف', 'Atténué'), L('Agree and apply mitigations.', 'اتفق على التخفيفات وطبّقها.', 'Convenez et appliquez les mesures.')),
      S(L('Monitored', 'مُراقَب', 'Surveillé'), L('Keep the arrangement under review.', 'أبقِ الترتيب قيد المراجعة.', 'Maintenez le dispositif sous revue.')),
    ],
    usage: [
      L('Click New to declare a conflict; describe the relationship and the risk to impartiality.',
        'انقر «جديد» لإعلان تضارب؛ صِف العلاقة والخطر على الحياد.',
        'Cliquez sur Nouveau pour déclarer un conflit ; décrivez la relation et le risque.'),
      L('Record the agreed mitigation and keep it under periodic review.',
        'سجّل التخفيف المتفق عليه وأبقه قيد المراجعة الدورية.',
        'Consignez la mesure convenue et maintenez-la sous revue périodique.'),
    ],
  },
  {
    route: '/org-context', titleKey: 'nav.ctx', groupKey: 'nav.groupRisk', icon: 'context',
    summary: L(
      'Organizational context: record internal/external issues and interested parties with their needs and expectations — the foundation for the quality management system’s scope and risks.',
      'سياق المنظمة: تسجيل القضايا الداخلية/الخارجية والأطراف المعنية باحتياجاتها وتوقعاتها — أساس نطاق نظام إدارة الجودة ومخاطره.',
      'Contexte de l’organisme : consigner les enjeux internes/externes et les parties intéressées avec leurs besoins — base du périmètre et des risques du système qualité.'),
    steps: [
      S(L('Drafted', 'مُسوّد', 'Rédigé'), L('Capture issues and interested parties.', 'التقط القضايا والأطراف المعنية.', 'Saisissez enjeux et parties intéressées.')),
      S(L('Reviewed', 'مُراجَع', 'Examiné'), L('Review needs, expectations and issues.', 'راجع الاحتياجات والتوقعات والقضايا.', 'Examinez besoins, attentes et enjeux.')),
      S(L('Approved', 'معتمد', 'Approuvé'), L('Approve as the current context baseline.', 'اعتمد كخط أساس للسياق الحالي.', 'Approuvez comme référentiel de contexte.')),
    ],
    usage: [
      L('Record internal and external issues, then list interested parties and their expectations.',
        'سجّل القضايا الداخلية والخارجية، ثم اسرد الأطراف المعنية وتوقعاتها.',
        'Consignez les enjeux internes et externes, puis listez les parties intéressées et leurs attentes.'),
      L('Revisit at management review; changes here inform the risk register.',
        'أعد النظر في مراجعة الإدارة؛ التغييرات هنا تُغذّي سجل المخاطر.',
        'Revisitez en revue de direction ; les changements alimentent le registre des risques.'),
    ],
  },

  // ── Resources ─────────────────────────────────────────────────────────────
  {
    route: '/equipment', titleKey: 'nav.equipment', groupKey: 'nav.groupResources', icon: 'equipment',
    summary: L(
      'Equipment & calibration: register instruments, keep them in service through calibration and maintenance schedules and intermediate checks, and take unfit equipment out of service.',
      'المعدات والمعايرة: تسجيل الأجهزة، وإبقاؤها في الخدمة عبر جداول المعايرة والصيانة والفحوص البينية، وإخراج غير الصالح منها من الخدمة.',
      'Équipements et étalonnage : enregistrer les instruments, les maintenir en service via étalonnages, maintenance et contrôles intermédiaires, et retirer ceux qui sont inaptes.'),
    steps: [
      S(L('Registered', 'مُسجَّل', 'Enregistré'), L('Add the instrument and its details.', 'أضف الجهاز وبياناته.', 'Ajoutez l’instrument et ses détails.')),
      S(L('In Service', 'في الخدمة', 'En service'), L('Available for use; schedules active.', 'متاح للاستخدام؛ الجداول نشطة.', 'Disponible ; échéanciers actifs.')),
      S(L('Calibration / Check', 'معايرة/فحص', 'Étalonnage/Contrôle'), L('Record calibrations and intermediate checks.', 'سجّل المعايرات والفحوص البينية.', 'Enregistrez étalonnages et contrôles intermédiaires.')),
      S(L('Out of Service', 'خارج الخدمة', 'Hors service'), L('Withdraw unfit or failed equipment.', 'اسحب المعدات غير الصالحة أو الفاشلة.', 'Retirez les équipements inaptes ou défaillants.')),
    ],
    usage: [
      L('Click New to register an instrument with its identifier and calibration interval.',
        'انقر «جديد» لتسجيل جهاز بمعرّفه وفترة معايرته.',
        'Cliquez sur Nouveau pour enregistrer un instrument avec son identifiant et son intervalle.'),
      L('Record each calibration and intermediate check; due dates and status update automatically.',
        'سجّل كل معايرة وفحص بيني؛ تُحدَّث تواريخ الاستحقاق والحالة تلقائيًا.',
        'Enregistrez chaque étalonnage et contrôle ; échéances et statut se mettent à jour automatiquement.'),
      L('Set equipment out of service when a check fails; this blocks its use in studies.',
        'اجعل المعدة خارج الخدمة عند فشل الفحص؛ يمنع ذلك استخدامها في الدراسات.',
        'Mettez l’équipement hors service en cas d’échec ; son usage dans les études est bloqué.'),
    ],
  },
  {
    route: '/reference-standards', titleKey: 'nav.standards', groupKey: 'nav.groupResources', icon: 'standards',
    summary: L(
      'Metrological traceability: register reference standards and materials, maintain their traceability chain and certificates, run intermediate checks and track recertification.',
      'التتبّعية القياسية: تسجيل المعايير والمواد المرجعية، والحفاظ على سلسلة تتبّعها وشهاداتها، وإجراء الفحوص البينية، وتتبّع إعادة الاعتماد.',
      'Traçabilité métrologique : enregistrer les étalons et matériaux de référence, maintenir la chaîne de traçabilité et les certificats, réaliser les contrôles et suivre la recertification.'),
    steps: [
      S(L('Registered', 'مُسجَّل', 'Enregistré'), L('Add the standard and its certificate.', 'أضف المعيار وشهادته.', 'Ajoutez l’étalon et son certificat.')),
      S(L('Traceable', 'متتبَّع', 'Traçable'), L('Link the traceability chain to SI/CRM.', 'اربط سلسلة التتبّع بـ SI/CRM.', 'Reliez la chaîne au SI/MRC.')),
      S(L('Intermediate Check', 'فحص بيني', 'Contrôle intermédiaire'), L('Verify stability between certifications.', 'تحقّق من الثبات بين الاعتمادات.', 'Vérifiez la stabilité entre certifications.')),
      S(L('Recertification', 'إعادة اعتماد', 'Recertification'), L('Flagged when the certificate expires.', 'يُعلَّم عند انتهاء الشهادة.', 'Signalé à l’expiration du certificat.')),
    ],
    usage: [
      L('Register a standard with its certificate, traceability source and validity date.',
        'سجّل معيارًا بشهادته ومصدر تتبّعه وتاريخ صلاحيته.',
        'Enregistrez un étalon avec son certificat, sa source de traçabilité et sa validité.'),
      L('Log intermediate checks; recertification reminders appear before expiry.',
        'سجّل الفحوص البينية؛ تظهر تذكيرات إعادة الاعتماد قبل الانتهاء.',
        'Consignez les contrôles ; les rappels de recertification apparaissent avant expiration.'),
    ],
  },
  {
    route: '/monitoring', titleKey: 'nav.env', groupKey: 'nav.groupResources', icon: 'environment',
    summary: L(
      'Environmental & facility monitoring: define monitored locations and their limits, record readings, and handle excursions with corrective action.',
      'مراقبة البيئة والمرافق: تعريف المواقع المراقَبة وحدودها، وتسجيل القراءات، ومعالجة التجاوزات بإجراء تصحيحي.',
      'Surveillance environnementale : définir les emplacements surveillés et leurs limites, enregistrer les relevés et traiter les dépassements par action corrective.'),
    steps: [
      S(L('Limits Set', 'الحدود مُحدّدة', 'Limites définies'), L('Define location, parameter and limits.', 'حدّد الموقع والمعيار والحدود.', 'Définissez emplacement, paramètre et limites.')),
      S(L('Reading Recorded', 'قراءة مُسجّلة', 'Relevé enregistré'), L('Log measurements over time.', 'سجّل القياسات عبر الوقت.', 'Consignez les mesures dans le temps.')),
      S(L('Excursion', 'تجاوز', 'Dépassement'), L('A reading outside limits is flagged.', 'تُعلَّم القراءة خارج الحدود.', 'Un relevé hors limites est signalé.')),
      S(L('Corrective Action', 'إجراء تصحيحي', 'Action corrective'), L('Investigate and act on the excursion.', 'حقّق وتصرّف تجاه التجاوز.', 'Investiguez et agissez sur le dépassement.')),
    ],
    usage: [
      L('Define each monitored location with its parameter and acceptable range.',
        'عرّف كل موقع مراقَب بمعياره ونطاقه المقبول.',
        'Définissez chaque emplacement surveillé avec son paramètre et sa plage acceptable.'),
      L('Record readings; out-of-limit readings flag automatically and prompt corrective action.',
        'سجّل القراءات؛ تُعلَّم القراءات خارج الحدود تلقائيًا وتستدعي إجراءً تصحيحيًا.',
        'Enregistrez les relevés ; les valeurs hors limites sont signalées et déclenchent une action corrective.'),
    ],
  },
  {
    route: '/suppliers', titleKey: 'nav.suppliers', groupKey: 'nav.groupResources', icon: 'suppliers',
    summary: L(
      'Supplier quality: register external providers, evaluate and approve them, monitor performance and re-evaluate periodically.',
      'جودة الموردين: تسجيل المزوّدين الخارجيين، وتقييمهم واعتمادهم، ومراقبة الأداء، وإعادة التقييم دوريًا.',
      'Qualité fournisseurs : enregistrer les prestataires externes, les évaluer et approuver, suivre la performance et réévaluer périodiquement.'),
    steps: [
      S(L('Registered', 'مُسجَّل', 'Enregistré'), L('Add the supplier and scope of supply.', 'أضف المورّد ونطاق التوريد.', 'Ajoutez le fournisseur et le périmètre.')),
      S(L('Evaluated', 'مُقيَّم', 'Évalué'), L('Assess against approval criteria.', 'قيّم مقابل معايير الاعتماد.', 'Évaluez selon les critères d’approbation.')),
      S(L('Approved', 'معتمد', 'Approuvé'), L('Add to the approved supplier list.', 'أضف لقائمة الموردين المعتمدين.', 'Ajoutez à la liste des fournisseurs approuvés.')),
      S(L('Monitored', 'مُراقَب', 'Surveillé'), L('Track performance; re-evaluate periodically.', 'تتبّع الأداء؛ أعد التقييم دوريًا.', 'Suivez la performance ; réévaluez régulièrement.')),
    ],
    usage: [
      L('Click New to register a supplier and the products/services they provide.',
        'انقر «جديد» لتسجيل مورّد والمنتجات/الخدمات التي يقدّمها.',
        'Cliquez sur Nouveau pour enregistrer un fournisseur et ses produits/services.'),
      L('Evaluate and approve; log performance issues, which can raise nonconformances.',
        'قيّم واعتمد؛ سجّل مشكلات الأداء التي قد تفتح عدم مطابقة.',
        'Évaluez et approuvez ; consignez les incidents de performance, qui peuvent créer des non-conformités.'),
    ],
  },

  // ── People ────────────────────────────────────────────────────────────────
  {
    route: '/competencies', titleKey: 'nav.competency', groupKey: 'nav.groupPeople', icon: 'competencies',
    summary: L(
      'Competency management: define required competencies, assess personnel against them, and track reassessment due dates.',
      'إدارة الكفاءة: تعريف الكفاءات المطلوبة، وتقييم الأفراد مقابلها، وتتبّع مواعيد إعادة التقييم.',
      'Gestion des compétences : définir les compétences requises, évaluer le personnel et suivre les échéances de réévaluation.'),
    steps: [
      S(L('Defined', 'مُعرّفة', 'Définie'), L('Define the competency and its criteria.', 'عرّف الكفاءة ومعاييرها.', 'Définissez la compétence et ses critères.')),
      S(L('Assessed', 'مُقيَّمة', 'Évaluée'), L('Assess a person against the criteria.', 'قيّم شخصًا مقابل المعايير.', 'Évaluez une personne selon les critères.')),
      S(L('Competent', 'كفؤ', 'Compétent'), L('Record the competent determination.', 'سجّل قرار الكفاءة.', 'Consignez la détermination de compétence.')),
      S(L('Reassessment', 'إعادة تقييم', 'Réévaluation'), L('Flagged when reassessment falls due.', 'يُعلَّم عند استحقاق إعادة التقييم.', 'Signalée à l’échéance de réévaluation.')),
    ],
    usage: [
      L('Define competencies, then assess each staff member and record the outcome.',
        'عرّف الكفاءات، ثم قيّم كل موظف وسجّل النتيجة.',
        'Définissez les compétences, puis évaluez chaque collaborateur et consignez le résultat.'),
      L('Reassessment reminders keep authorizations valid; see the Authorizations page.',
        'تُبقي تذكيرات إعادة التقييم التخويلات سارية؛ راجع صفحة التخويلات.',
        'Les rappels de réévaluation maintiennent les autorisations valides ; voir la page Autorisations.'),
    ],
  },
  {
    route: '/authorizations', titleKey: 'nav.authz', groupKey: 'nav.groupPeople', icon: 'authorizations',
    summary: L(
      'Personnel authorization matrix: who is authorized to perform, review/release or train for each activity — granted on evidence of competency, and suspended or revoked when needed.',
      'مصفوفة تخويل الأفراد: من المخوّل للأداء أو المراجعة/الإصدار أو التدريب لكل نشاط — يُمنح ببيّنة الكفاءة، ويُعلَّق أو يُلغى عند الحاجة.',
      'Matrice d’autorisation du personnel : qui est autorisé à exécuter, réviser/libérer ou former par activité — accordé sur preuve de compétence, suspendu ou révoqué au besoin.'),
    steps: [
      S(L('Requested', 'مطلوب', 'Demandé'), L('Request an authorization scope for a person.', 'اطلب نطاق تخويل لشخص.', 'Demandez un périmètre d’autorisation pour une personne.')),
      S(L('Granted', 'ممنوح', 'Accordé'), L('Authorized on evidence of competency.', 'يُمنح ببيّنة الكفاءة.', 'Accordé sur preuve de compétence.')),
      S(L('Active', 'نشِط', 'Actif'), L('The person may perform the scope.', 'يجوز للشخص أداء النطاق.', 'La personne peut exercer le périmètre.')),
      S(L('Suspended / Revoked', 'مُعلَّق/مُلغى', 'Suspendu/Révoqué'), L('Withdrawn if competency lapses.', 'يُسحب إذا انقضت الكفاءة.', 'Retiré si la compétence expire.')),
    ],
    usage: [
      L('Grant an authorization scope (Perform / Review & Release / Train) to a qualified person.',
        'امنح نطاق تخويل (أداء / مراجعة وإصدار / تدريب) لشخص مؤهل.',
        'Accordez un périmètre (Exécuter / Réviser & libérer / Former) à une personne qualifiée.'),
      L('Suspend or revoke when competency lapses; the matrix shows current coverage at a glance.',
        'علّق أو ألغِ عند انقضاء الكفاءة؛ تُظهر المصفوفة التغطية الحالية في لمحة.',
        'Suspendez ou révoquez si la compétence expire ; la matrice montre la couverture actuelle.'),
    ],
  },
  {
    route: '/training', titleKey: 'nav.training', groupKey: 'nav.groupPeople', icon: 'training',
    summary: L(
      'Training management: plan training, assign it to personnel, record completion, and check that the training was effective.',
      'إدارة التدريب: تخطيط التدريب، وإسناده للأفراد، وتسجيل الإتمام، والتحقق من فعالية التدريب.',
      'Gestion de la formation : planifier, affecter au personnel, enregistrer l’achèvement et vérifier l’efficacité.'),
    steps: [
      S(L('Planned', 'مخطّط', 'Planifiée'), L('Define the training and its objective.', 'عرّف التدريب وهدفه.', 'Définissez la formation et son objectif.')),
      S(L('Assigned', 'مُسند', 'Affectée'), L('Assign to the relevant personnel.', 'أسند للأفراد المعنيين.', 'Affectez au personnel concerné.')),
      S(L('Completed', 'مُنجز', 'Terminée'), L('Record attendance/completion.', 'سجّل الحضور/الإتمام.', 'Enregistrez la présence/l’achèvement.')),
      S(L('Effectiveness', 'الفعالية', 'Efficacité'), L('Confirm the training achieved its aim.', 'أكّد أن التدريب حقّق هدفه.', 'Confirmez que la formation a atteint son but.')),
    ],
    usage: [
      L('Plan a training item and assign it; assignees see it in their task queue.',
        'خطّط عنصر تدريب وأسنده؛ يراه المُسند إليهم في قائمة مهامهم.',
        'Planifiez une formation et affectez-la ; les destinataires la voient dans leur file.'),
      L('Record completion, then run the effectiveness check to close the loop.',
        'سجّل الإتمام، ثم نفّذ فحص الفعالية لإغلاق الدورة.',
        'Enregistrez l’achèvement, puis réalisez le contrôle d’efficacité.'),
    ],
  },

  // ── Analytical quality ────────────────────────────────────────────────────
  {
    route: '/qc', titleKey: 'nav.qc', groupKey: 'nav.groupAnalytical', icon: 'qc',
    summary: L(
      'Internal quality control: configure QC profiles with target mean and SD, record control runs, and let the system apply Westgard rules to accept, warn or reject each run.',
      'مراقبة الجودة الداخلية: تهيئة ملفات QC بالمتوسط والانحراف المعياري المستهدفين، وتسجيل تشغيلات الضبط، وتطبيق قواعد ويستغارد لقبول كل تشغيلة أو تحذيرها أو رفضها.',
      'Contrôle qualité interne : configurer des profils CQ avec moyenne et écart-type cibles, enregistrer les passages de contrôle et appliquer les règles de Westgard pour accepter, alerter ou rejeter.'),
    steps: [
      S(L('Configure Profile', 'تهيئة الملف', 'Configurer le profil'), L('Set analyte, instrument, target mean and SD.', 'حدّد المُحلَّل والجهاز والمتوسط والانحراف المستهدفين.', 'Définissez analyte, instrument, moyenne et écart-type cibles.')),
      S(L('Record Run', 'تسجيل التشغيلة', 'Enregistrer un passage'), L('Enter each control measurement.', 'أدخل كل قياس ضبط.', 'Saisissez chaque mesure de contrôle.')),
      S(L('Westgard Evaluation', 'تقييم ويستغارد', 'Évaluation Westgard'), L('The system flags accept / warn / reject.', 'يُعلِّم النظام قبول/تحذير/رفض.', 'Le système signale accepter / alerter / rejeter.')),
      S(L('Troubleshoot', 'استكشاف الأخطاء', 'Dépannage'), L('Investigate rejected runs and record notes.', 'حقّق في التشغيلات المرفوضة وسجّل الملاحظات.', 'Investiguez les passages rejetés et notez.')),
    ],
    usage: [
      L('Create a QC profile per analyte/instrument with its target mean and SD.',
        'أنشئ ملف QC لكل مُحلَّل/جهاز بمتوسطه وانحرافه المستهدفين.',
        'Créez un profil CQ par analyte/instrument avec sa moyenne et son écart-type cibles.'),
      L('Record each control run; the Levey-Jennings chart and Westgard verdict update live.',
        'سجّل كل تشغيلة ضبط؛ يتحدّث مخطط ليفي-جينينغز وحكم ويستغارد حيًّا.',
        'Enregistrez chaque passage ; le diagramme de Levey-Jennings et le verdict Westgard se mettent à jour.'),
      L('Add a troubleshooting note on rejected runs before accepting corrective action.',
        'أضف ملاحظة استكشاف على التشغيلات المرفوضة قبل قبول الإجراء التصحيحي.',
        'Ajoutez une note de dépannage sur les passages rejetés avant l’action corrective.'),
    ],
  },
  {
    route: '/validation-studies', titleKey: 'nav.validation', groupKey: 'nav.groupAnalytical', icon: 'validation',
    summary: L(
      'Method validation/verification: enter replicate results at each level to derive bias and imprecision against the total allowable error, then sign off.',
      'التحقق من صحة الطريقة: إدخال نتائج مكررة عند كل مستوى لاشتقاق الانحياز وعدم الدقة مقابل الخطأ الكلي المسموح، ثم التوقيع.',
      'Validation/vérification de méthode : saisir des réplicats à chaque niveau pour dériver le biais et l’imprécision face à l’erreur totale admissible, puis valider.'),
    steps: ANALYTICAL_FLOW,
    usage: analyticalUsage(
      L('Enter replicate measurements per level, with reference values where applicable.',
        'أدخل قياسات مكررة لكل مستوى، مع القيم المرجعية حيثما ينطبق.',
        'Saisissez des réplicats par niveau, avec valeurs de référence le cas échéant.'),
    ),
  },
  {
    route: '/method-comparisons', titleKey: 'nav.mc', groupKey: 'nav.groupAnalytical', icon: 'methodcomp',
    summary: L(
      'Method comparison (CLSI EP09): enter paired reference/test results to derive Deming and Passing–Bablok regression, Pearson r and a Bland–Altman bias, then sign off.',
      'مقارنة الطرق (CLSI EP09): إدخال نتائج مرجعية/اختبار مزدوجة لاشتقاق انحدار ديمينغ وباسينغ-بابلوك ومعامل بيرسون وانحياز بلاند-ألتمان، ثم التوقيع.',
      'Comparaison de méthodes (CLSI EP09) : saisir des paires référence/test pour dériver les régressions de Deming et Passing–Bablok, le r de Pearson et un biais Bland–Altman, puis valider.'),
    steps: ANALYTICAL_FLOW,
    usage: analyticalUsage(
      L('Enter paired reference-method and test-method values (or import a CSV of pairs).',
        'أدخل قيم الطريقة المرجعية والاختبار المزدوجة (أو استورد CSV للأزواج).',
        'Saisissez les paires méthode de référence / méthode test (ou importez un CSV).'),
    ),
  },
  {
    route: '/precision-studies', titleKey: 'nav.prc', groupKey: 'nav.groupAnalytical', icon: 'precision',
    summary: L(
      'Imprecision study (CLSI EP05): enter run-grouped replicates so nested ANOVA derives repeatability, between-run and within-laboratory precision against your claims.',
      'دراسة عدم الدقة (CLSI EP05): إدخال مكررات مجمّعة حسب التشغيلة ليشتق تحليل التباين المتداخل التكرارية والدقة بين التشغيلات وداخل المختبر مقابل ادعاءاتك.',
      'Étude d’imprécision (CLSI EP05) : saisir des réplicats groupés par série pour dériver, par ANOVA emboîtée, la répétabilité et la précision inter-série et intra-laboratoire.'),
    steps: ANALYTICAL_FLOW,
    usage: analyticalUsage(
      L('Enter replicates grouped by run label (at least two runs) for one concentration level.',
        'أدخل مكررات مجمّعة حسب اسم التشغيلة (تشغيلتان على الأقل) لمستوى تركيز واحد.',
        'Saisissez des réplicats groupés par série (au moins deux séries) pour un niveau de concentration.'),
    ),
  },
  {
    route: '/linearity-studies', titleKey: 'nav.lin', groupKey: 'nav.groupAnalytical', icon: 'linearity',
    summary: L(
      'Linearity & AMR (CLSI EP06): enter measured values across assigned levels to assess linearity and establish the analytical measurement range, then sign off.',
      'الخطية ونطاق القياس (CLSI EP06): إدخال القيم المقيسة عبر المستويات المعيّنة لتقييم الخطية وتحديد نطاق القياس التحليلي، ثم التوقيع.',
      'Linéarité et AMR (CLSI EP06) : saisir les valeurs mesurées aux niveaux assignés pour évaluer la linéarité et établir la plage de mesure, puis valider.'),
    steps: ANALYTICAL_FLOW,
    usage: analyticalUsage(
      L('Enter the assigned (expected) level and the measured value at each dilution point.',
        'أدخل المستوى المعيّن (المتوقع) والقيمة المقيسة عند كل نقطة تخفيف.',
        'Saisissez le niveau assigné (attendu) et la valeur mesurée à chaque point de dilution.'),
    ),
  },
  {
    route: '/detection-limits', titleKey: 'nav.dl', groupKey: 'nav.groupAnalytical', icon: 'detection',
    summary: L(
      'Detection capability (CLSI EP17): enter blank and low-level replicates to establish the Limit of Blank, Detection and Quantitation, then sign off.',
      'قدرة الكشف (CLSI EP17): إدخال مكررات الفراغ والمستوى المنخفض لتحديد حد الفراغ والكشف والتحديد الكمي، ثم التوقيع.',
      'Capacité de détection (CLSI EP17) : saisir des réplicats de blanc et de bas niveau pour établir les limites de blanc, de détection et de quantification, puis valider.'),
    steps: ANALYTICAL_FLOW,
    usage: analyticalUsage(
      L('Enter blank replicates and low-level sample replicates as required by the protocol.',
        'أدخل مكررات الفراغ ومكررات العينة منخفضة المستوى حسب البروتوكول.',
        'Saisissez les réplicats de blanc et d’échantillons de bas niveau selon le protocole.'),
    ),
  },
  {
    route: '/reference-intervals', titleKey: 'nav.ri', groupKey: 'nav.groupAnalytical', icon: 'refinterval',
    summary: L(
      'Reference-interval verification (CLSI EP28): enter reference-population results to verify a proposed interval by the transference/verification approach, then sign off.',
      'التحقق من الفترة المرجعية (CLSI EP28): إدخال نتائج المجتمع المرجعي للتحقق من فترة مقترحة بأسلوب النقل/التحقق، ثم التوقيع.',
      'Vérification d’intervalle de référence (CLSI EP28) : saisir les résultats d’une population de référence pour vérifier un intervalle proposé, puis valider.'),
    steps: ANALYTICAL_FLOW,
    usage: analyticalUsage(
      L('Enter the reference individuals’ results and the proposed lower/upper limits.',
        'أدخل نتائج أفراد المرجع والحدود الدنيا/العليا المقترحة.',
        'Saisissez les résultats des individus de référence et les limites proposées.'),
    ),
  },
  {
    route: '/sigma-metrics', titleKey: 'nav.sig', groupKey: 'nav.groupAnalytical', icon: 'sigma',
    summary: L(
      'Six-sigma assessment: combine bias, imprecision (CV) and the total allowable error to derive the sigma metric and the QC effort it implies, then sign off.',
      'تقييم ستة سيجما: دمج الانحياز وعدم الدقة (CV) والخطأ الكلي المسموح لاشتقاق مقياس سيجما وجهد QC الذي يستلزمه، ثم التوقيع.',
      'Évaluation Six Sigma : combiner biais, imprécision (CV) et erreur totale admissible pour dériver la métrique sigma et l’effort CQ associé, puis valider.'),
    steps: ANALYTICAL_FLOW,
    usage: analyticalUsage(
      L('Enter the observed bias, CV and the total allowable error for the analyte.',
        'أدخل الانحياز الملاحظ وCV والخطأ الكلي المسموح للمُحلَّل.',
        'Saisissez le biais observé, le CV et l’erreur totale admissible de l’analyte.'),
    ),
  },
  {
    route: '/outlier-screenings', titleKey: 'nav.out', groupKey: 'nav.groupAnalytical', icon: 'outlier',
    summary: L(
      'Outlier screening: add a dataset’s points and let the system flag outliers using Tukey fences (1.5·IQR) and the MAD-based modified z-score before the data is trusted for statistics.',
      'فحص القيم الشاذة: أضف نقاط مجموعة بيانات ودع النظام يُعلِّم الشواذ باستخدام أسوار توكي (1.5·IQR) ودرجة z المعدلة القائمة على MAD قبل الوثوق بالبيانات للإحصاء.',
      'Dépistage d’aberrants : ajoutez les points d’un jeu de données ; le système signale les aberrants via les bornes de Tukey (1,5·IQR) et le score z modifié (MAD) avant toute statistique.'),
    steps: ANALYTICAL_FLOW,
    usage: analyticalUsage(
      L('Add at least four data points (optionally labelled); Calculate flags any outliers.',
        'أضف أربع نقاط على الأقل (بتسمية اختيارية)؛ يُعلِّم الحساب أي شواذ.',
        'Ajoutez au moins quatre points (étiquette facultative) ; Calculer signale les aberrants.'),
    ),
  },
  {
    route: '/carryover-studies', titleKey: 'nav.car', groupKey: 'nav.groupAnalytical', icon: 'carryover',
    summary: L(
      'Carryover study (CLSI EP10): enter a high sample followed by low replicates in sequence; the system computes percentage carryover and the pass/fail verdict against the allowable limit.',
      'دراسة الترحيل (CLSI EP10): إدخال عينة عالية تتبعها مكررات منخفضة بالتسلسل؛ يحسب النظام نسبة الترحيل وحكم النجاح/الفشل مقابل الحد المسموح.',
      'Étude de contamination (CLSI EP10) : saisir un échantillon élevé suivi de réplicats bas en séquence ; le système calcule la contamination en % et le verdict.'),
    steps: ANALYTICAL_FLOW,
    usage: analyticalUsage(
      L('Add one high reading then at least three low readings, ordered by sequence number.',
        'أضف قراءة عالية واحدة ثم ثلاث قراءات منخفضة على الأقل، مرتّبة برقم التسلسل.',
        'Ajoutez une lecture élevée puis au moins trois lectures basses, ordonnées par séquence.'),
    ),
  },
  {
    route: '/lot-comparisons', titleKey: 'nav.lot', groupKey: 'nav.groupAnalytical', icon: 'lotcompare',
    summary: L(
      'Lot-to-lot comparison: enter paired current-lot and new-lot readings; the system computes the mean percentage bias and whether the new lot is acceptable.',
      'مقارنة الدفعات: إدخال قراءات مزدوجة للدفعة الحالية والجديدة؛ يحسب النظام متوسط الانحياز المئوي وقبول الدفعة الجديدة.',
      'Comparaison lot à lot : saisir des paires lot actuel/nouveau lot ; le système calcule le biais moyen en % et l’acceptabilité du nouveau lot.'),
    steps: ANALYTICAL_FLOW,
    usage: analyticalUsage(
      L('Add at least three sample pairs measured on both the current and the new lot.',
        'أضف ثلاثة أزواج عينات على الأقل مقيسة على الدفعتين الحالية والجديدة.',
        'Ajoutez au moins trois paires d’échantillons mesurés sur le lot actuel et le nouveau.'),
    ),
  },
  {
    route: '/interference-studies', titleKey: 'nav.inf', groupKey: 'nav.groupAnalytical', icon: 'interference',
    summary: L(
      'Interference study (CLSI EP07): enter control replicates as the baseline and test replicates for each spiked interferent; the system flags significant interference by percentage bias.',
      'دراسة التداخل (CLSI EP07): إدخال مكررات الضابط كخط أساس ومكررات اختبار لكل متداخل مُضاف؛ يُعلِّم النظام التداخل المؤثر بنسبة الانحياز.',
      'Étude d’interférence (CLSI EP07) : saisir des réplicats témoins comme ligne de base et des réplicats test par interférent dopé ; le système signale l’interférence significative.'),
    steps: ANALYTICAL_FLOW,
    usage: analyticalUsage(
      L('Add at least three control replicates, then test replicates for each named interferent.',
        'أضف ثلاثة مكررات ضابط على الأقل، ثم مكررات اختبار لكل متداخل مُسمّى.',
        'Ajoutez au moins trois réplicats témoins, puis des réplicats test par interférent nommé.'),
    ),
  },
  {
    route: '/instrument-comparabilities', titleKey: 'nav.icp', groupKey: 'nav.groupAnalytical', icon: 'instrumentcompare',
    summary: L(
      'Instrument comparability: enter readings of shared samples on each instrument; every instrument is benchmarked to the reference by mean percentage bias and a comparable verdict.',
      'قابلية مقارنة الأجهزة: إدخال قراءات عينات مشتركة على كل جهاز؛ يُقارن كل جهاز بالمرجع بمتوسط الانحياز المئوي وحكم قابلية المقارنة.',
      'Comparabilité des instruments : saisir les lectures d’échantillons communs sur chaque instrument ; chacun est comparé à la référence par biais moyen en % et un verdict.'),
    steps: ANALYTICAL_FLOW,
    usage: analyticalUsage(
      L('Enter the reference instrument’s readings and each comparison instrument on the same sample IDs.',
        'أدخل قراءات الجهاز المرجعي وكل جهاز مقارنة على نفس معرّفات العينات.',
        'Saisissez les lectures de l’instrument de référence et de chaque instrument comparé sur les mêmes ID.'),
    ),
  },
  {
    route: '/uncertainty', titleKey: 'nav.mu', groupKey: 'nav.groupAnalytical', icon: 'uncertainty',
    summary: L(
      'Measurement uncertainty (ISO 17025 §7.6): combine the uncertainty components (bias, precision, reference) into a combined and expanded uncertainty budget, then sign off.',
      'ارتياب القياس (ISO 17025 §7.6): دمج مكوّنات الارتياب (الانحياز، الدقة، المرجع) في ميزانية ارتياب مركّبة وموسّعة، ثم التوقيع.',
      'Incertitude de mesure (ISO 17025 §7.6) : combiner les composantes (biais, précision, référence) en un budget d’incertitude composée et élargie, puis valider.'),
    steps: ANALYTICAL_FLOW,
    usage: analyticalUsage(
      L('Enter each uncertainty component with its value and type; the budget combines them.',
        'أدخل كل مكوّن ارتياب بقيمته ونوعه؛ تجمعها الميزانية.',
        'Saisissez chaque composante avec sa valeur et son type ; le budget les combine.'),
    ),
  },
  {
    route: '/pt-plans', titleKey: 'nav.ptp', groupKey: 'nav.groupAnalytical', icon: 'ptplan',
    summary: L(
      'Proficiency-testing plan: schedule which analytes are enrolled in which PT scheme and cycle across the year, ensuring external quality-assessment coverage.',
      'خطة اختبار الكفاءة: جدولة أي المُحلّلات مُسجّلة في أي مخطط PT ودورة عبر العام، لضمان تغطية التقييم الخارجي للجودة.',
      'Plan d’essais d’aptitude : programmer quels analytes sont inscrits à quel schéma EA et quel cycle sur l’année, pour assurer la couverture de l’évaluation externe.'),
    steps: [
      S(L('Planned', 'مخطّطة', 'Planifié'), L('Define the analytes, scheme and cycles.', 'حدّد المُحلّلات والمخطط والدورات.', 'Définissez analytes, schéma et cycles.')),
      S(L('Scheduled', 'مجدولة', 'Programmé'), L('Set cycle dates across the year.', 'حدّد مواعيد الدورات عبر العام.', 'Fixez les dates de cycle sur l’année.')),
      S(L('Enrolled', 'مُسجّلة', 'Inscrit'), L('Enrollments feed the Proficiency Tests page.', 'تُغذّي التسجيلات صفحة اختبارات الكفاءة.', 'Les inscriptions alimentent la page Essais d’aptitude.')),
    ],
    usage: [
      L('Create a plan listing each analyte, its PT scheme and the scheduled cycles.',
        'أنشئ خطة تسرد كل مُحلَّل ومخطط PT ودوراته المجدولة.',
        'Créez un plan listant chaque analyte, son schéma EA et les cycles programmés.'),
      L('Record results per cycle on the Proficiency Tests page.',
        'سجّل النتائج لكل دورة في صفحة اختبارات الكفاءة.',
        'Enregistrez les résultats par cycle sur la page Essais d’aptitude.'),
    ],
  },
  {
    route: '/proficiency-tests', titleKey: 'nav.pt', groupKey: 'nav.groupAnalytical', icon: 'pt',
    summary: L(
      'Proficiency testing (external QA): record submitted and assigned values per cycle; the system computes the z-score and performance so poor results can drive investigation.',
      'اختبار الكفاءة (التقييم الخارجي): تسجيل القيم المُقدَّمة والمعيّنة لكل دورة؛ يحسب النظام درجة z والأداء لتحفيز التحقيق عند النتائج الضعيفة.',
      'Essais d’aptitude (EA externe) : enregistrer les valeurs soumises et assignées par cycle ; le système calcule le score z et la performance pour déclencher une investigation si besoin.'),
    steps: [
      S(L('Enrolled', 'مُسجَّل', 'Inscrit'), L('Enroll the analyte in a scheme cycle.', 'سجّل المُحلَّل في دورة مخطط.', 'Inscrivez l’analyte à un cycle.')),
      S(L('Submitted', 'مُقدَّم', 'Soumis'), L('Record your submitted result.', 'سجّل نتيجتك المُقدَّمة.', 'Enregistrez votre résultat soumis.')),
      S(L('Assigned', 'معيّن', 'Assigné'), L('Enter the assigned value and SD.', 'أدخل القيمة المعيّنة والانحراف.', 'Saisissez la valeur assignée et l’écart-type.')),
      S(L('Evaluated', 'مُقيَّم', 'Évalué'), L('z-score and performance are derived.', 'يُشتق z والأداء.', 'Le score z et la performance sont dérivés.')),
    ],
    usage: [
      L('Enroll an analyte, record your submitted value, then enter the assigned value and SD.',
        'سجّل مُحلَّلاً، وسجّل قيمتك المُقدَّمة، ثم أدخل القيمة المعيّنة والانحراف.',
        'Inscrivez un analyte, enregistrez votre valeur soumise, puis la valeur assignée et l’écart-type.'),
      L('A |z| ≥ 3 is unacceptable and should trigger a nonconformance investigation.',
        'قيمة |z| ≥ 3 غير مقبولة وينبغي أن تُطلق تحقيق عدم مطابقة.',
        'Un |z| ≥ 3 est inacceptable et doit déclencher une investigation de non-conformité.'),
    ],
  },

  // ── Administration ────────────────────────────────────────────────────────
  {
    route: '/reference-data', titleKey: 'nav.reference', groupKey: 'nav.groupAdmin', icon: 'reference',
    summary: L(
      'Reference data & lookups: manage the shared list-of-value catalogs (departments, methods, units, categories…) that populate dropdowns across the system.',
      'البيانات المرجعية والقوائم: إدارة كتالوجات قوائم القيم المشتركة (الأقسام، الطرق، الوحدات، الفئات…) التي تملأ القوائم المنسدلة عبر النظام.',
      'Données de référence et listes : gérer les catalogues de valeurs partagés (services, méthodes, unités, catégories…) qui alimentent les listes déroulantes du système.'),
    steps: [
      S(L('Select Category', 'اختيار الفئة', 'Choisir la catégorie'), L('Pick the lookup catalog to manage.', 'اختر الكتالوج المراد إدارته.', 'Choisissez le catalogue à gérer.')),
      S(L('Add / Edit Value', 'إضافة/تعديل قيمة', 'Ajouter/modifier'), L('Maintain the list entries.', 'حافظ على مدخلات القائمة.', 'Maintenez les entrées de la liste.')),
      S(L('Activate / Retire', 'تفعيل/سحب', 'Activer/Retirer'), L('Retire values without deleting history.', 'اسحب القيم دون حذف السجل.', 'Retirez sans supprimer l’historique.')),
    ],
    usage: [
      L('Choose a category, then add or edit its values; starter values ship with each tenant.',
        'اختر فئة، ثم أضف أو عدّل قيمها؛ تُشحن قيم مبدئية مع كل مؤسسة.',
        'Choisissez une catégorie, puis ajoutez/modifiez ses valeurs ; des valeurs initiales sont fournies.'),
      L('Retire an obsolete value instead of deleting it to preserve historical records.',
        'اسحب القيمة المهجورة بدلاً من حذفها للحفاظ على السجلات التاريخية.',
        'Retirez une valeur obsolète au lieu de la supprimer pour préserver l’historique.'),
    ],
  },
  {
    route: '/notification-rules', titleKey: 'nav.notificationRules', groupKey: 'nav.groupAdmin', icon: 'rules',
    summary: L(
      'Notification rules: define which events notify which roles, and the reminder/escalation timing — the engine that fills users’ notification and task queues.',
      'قواعد الإشعارات: تعريف أي الأحداث تُشعر أي الأدوار، وتوقيت التذكير/التصعيد — المحرك الذي يملأ قوائم إشعارات المستخدمين ومهامهم.',
      'Règles de notification : définir quels événements notifient quels rôles, et le calendrier de rappel/escalade — le moteur qui alimente notifications et tâches.'),
    steps: [
      S(L('Define Trigger', 'تعريف المُطلِق', 'Définir le déclencheur'), L('Choose the event to react to.', 'اختر الحدث المُتفاعَل معه.', 'Choisissez l’événement à traiter.')),
      S(L('Set Recipients', 'تحديد المستلمين', 'Définir les destinataires'), L('Pick the roles to notify.', 'اختر الأدوار المُشعَرة.', 'Choisissez les rôles à notifier.')),
      S(L('Activate', 'تفعيل', 'Activer'), L('Enable the rule; set reminder timing.', 'فعّل القاعدة؛ حدّد توقيت التذكير.', 'Activez la règle ; réglez les rappels.')),
    ],
    usage: [
      L('Create a rule linking an event type to recipient roles and escalation timing.',
        'أنشئ قاعدة تربط نوع حدث بأدوار المستلمين وتوقيت التصعيد.',
        'Créez une règle liant un type d’événement à des rôles et un calendrier d’escalade.'),
      L('Only Quality Managers and admins can manage rules.',
        'يمكن لمديري الجودة والمشرفين فقط إدارة القواعد.',
        'Seuls les responsables qualité et administrateurs gèrent les règles.'),
    ],
  },
  {
    route: '/mail-management', titleKey: 'nav.mailManagement', groupKey: 'nav.groupAdmin', icon: 'mail',
    summary: L(
      'Mail Management: set the sender identity (from name, address, reply-to) and the brand accent and footer for the HTML e-mail template. The mail relay itself stays in secure server configuration.',
      'إدارة البريد: اضبط هوية المُرسِل (الاسم والعنوان وعنوان الرد) ولون العلامة وتذييل قالب البريد HTML. يبقى خادم البريد في إعدادات الخادم الآمنة.',
      "Gestion du courrier : définissez l'identité de l'expéditeur (nom, adresse, réponse) et l'accent de marque et le pied de page du modèle HTML. Le relais reste dans la configuration sécurisée du serveur."),
    steps: [
      S(L('Set sender', 'ضبط المُرسِل', "Définir l'expéditeur"), L('Enter the from name and address recipients will see.', 'أدخل اسم وعنوان المُرسِل الظاهر للمستلمين.', "Saisissez le nom et l'adresse d'expéditeur.")),
      S(L('Brand it', 'إضافة العلامة', 'Personnaliser'), L('Optionally set a brand colour and footer note.', 'اختيارياً اضبط لون العلامة وملاحظة التذييل.', 'Définissez éventuellement une couleur et un pied de page.')),
      S(L('Enable', 'تفعيل', 'Activer'), L('Turn e-mail notifications on or off for the tenant.', 'فعّل أو أوقف إشعارات البريد للمستأجر.', "Activez ou désactivez les e-mails pour le locataire.")),
    ],
    usage: [
      L('Save the sender identity used for Mail-type notifications; the HTML template applies your branding.',
        'احفظ هوية المُرسِل المستخدمة لإشعارات البريد؛ يطبّق قالب HTML علامتك.',
        "Enregistrez l'identité d'expéditeur des notifications ; le modèle HTML applique votre marque."),
      L('Transport credentials (SMTP host/user/password) are configured on the server, never here.',
        'تُضبط بيانات النقل (خادم SMTP والمستخدم وكلمة المرور) على الخادم وليس هنا.',
        "Les identifiants SMTP sont configurés sur le serveur, jamais ici."),
    ],
  },
  {
    route: '/compliance', titleKey: 'nav.compliance', groupKey: 'nav.groupAdmin', icon: 'compliance',
    summary: L(
      'Compliance ledger: the tamper-evident, hash-chained record of electronic signatures and controlled actions for 21 CFR Part 11 / ISO 17025 audit readiness.',
      'سجل الامتثال: السجل المقاوم للعبث والمُسلسل بالتجزئة للتوقيعات الإلكترونية والإجراءات المضبوطة لجاهزية تدقيق 21 CFR Part 11 / ISO 17025.',
      'Registre de conformité : l’enregistrement inviolable, chaîné par hachage, des signatures électroniques et actions maîtrisées pour l’audit 21 CFR Part 11 / ISO 17025.'),
    steps: [
      S(L('Event Recorded', 'حدث مُسجَّل', 'Événement enregistré'), L('Each signature/action is appended.', 'يُلحَق كل توقيع/إجراء.', 'Chaque signature/action est ajoutée.')),
      S(L('Hash-Chained', 'مُسلسل بالتجزئة', 'Chaîné par hachage'), L('Entries are linked so tampering shows.', 'تُربط المدخلات ليظهر أي عبث.', 'Les entrées sont liées : toute altération se voit.')),
      S(L('Verified', 'مُتحقَّق', 'Vérifié'), L('The chain can be verified end-to-end.', 'يمكن التحقق من السلسلة من الطرف للطرف.', 'La chaîne se vérifie de bout en bout.')),
    ],
    usage: [
      L('Browse and filter the ledger; each entry shows the actor, action and signature.',
        'تصفّح السجل وصفِّه؛ يُظهر كل مدخل الفاعل والإجراء والتوقيع.',
        'Parcourez et filtrez le registre ; chaque entrée montre l’acteur, l’action et la signature.'),
      L('The ledger is read-only and append-only — it cannot be edited or deleted.',
        'السجل للقراءة فقط ويُلحَق فقط — لا يمكن تعديله أو حذفه.',
        'Le registre est en lecture seule et en ajout seul — ni modifiable ni supprimable.'),
    ],
  },
  {
    route: '/users', titleKey: 'nav.users', groupKey: 'nav.groupAdmin', icon: 'users',
    summary: L(
      'User administration: invite users into your tenant, assign their roles, and activate or deactivate accounts — controlling who can do what across the system.',
      'إدارة المستخدمين: دعوة المستخدمين إلى مؤسستك، وإسناد أدوارهم، وتفعيل الحسابات أو تعطيلها — للتحكم بمن يفعل ماذا عبر النظام.',
      'Administration des utilisateurs : inviter des utilisateurs, attribuer leurs rôles et activer/désactiver les comptes — pour contrôler qui fait quoi.'),
    steps: [
      S(L('Invited', 'مدعو', 'Invité'), L('Invite the user by email.', 'ادعُ المستخدم بالبريد.', 'Invitez l’utilisateur par e-mail.')),
      S(L('Active', 'نشِط', 'Actif'), L('The account can sign in.', 'يمكن للحساب تسجيل الدخول.', 'Le compte peut se connecter.')),
      S(L('Roles Assigned', 'الأدوار مُسندة', 'Rôles attribués'), L('Grant roles that gate features.', 'امنح الأدوار التي تحكم الميزات.', 'Accordez les rôles qui régissent les fonctions.')),
      S(L('Deactivated', 'مُعطَّل', 'Désactivé'), L('Disable access without deleting history.', 'عطّل الوصول دون حذف السجل.', 'Désactivez l’accès sans supprimer l’historique.')),
    ],
    usage: [
      L('Invite a user, then assign roles (e.g. Quality Manager, Department Head, Analyst).',
        'ادعُ مستخدمًا، ثم أسند الأدوار (مثل مدير جودة، رئيس قسم، محلل).',
        'Invitez un utilisateur, puis attribuez des rôles (responsable qualité, chef de service, analyste).'),
      L('Deactivate rather than delete to preserve the audit trail; only tenant admins manage users.',
        'عطّل بدلاً من الحذف للحفاظ على أثر التدقيق؛ يدير المستخدمين مشرفو المؤسسة فقط.',
        'Désactivez plutôt que supprimer pour préserver la piste d’audit ; seuls les administrateurs gèrent les utilisateurs.'),
    ],
  },
  {
    route: '/roles', titleKey: 'nav.roles', groupKey: 'nav.groupAdmin', icon: 'accessReview',
    summary: L(
      'Roles & privileges: tenant-defined roles over the permission catalogue. Each role grants module-action permissions and can be restricted to branches and departments as a hard data filter.',
      '\u0627\u0644\u0623\u062f\u0648\u0627\u0631 \u0648\u0627\u0644\u0635\u0644\u0627\u062d\u064a\u0627\u062a: \u0623\u062f\u0648\u0627\u0631 \u064a\u0639\u0631\u0641\u0647\u0627 \u0627\u0644\u0645\u0633\u062a\u0623\u062c\u0631 \u0641\u0648\u0642 \u0641\u0647\u0631\u0633 \u0627\u0644\u0635\u0644\u0627\u062d\u064a\u0627\u062a. \u0643\u0644 \u062f\u0648\u0631 \u064a\u0645\u0646\u062d \u0635\u0644\u0627\u062d\u064a\u0627\u062a \u0648\u062d\u062f\u0629-\u0625\u062c\u0631\u0627\u0621 \u0648\u064a\u0645\u0643\u0646 \u062a\u0642\u064a\u064a\u062f\u0647 \u0628\u0627\u0644\u0641\u0631\u0648\u0639 \u0648\u0627\u0644\u0623\u0642\u0633\u0627\u0645 \u0643\u0645\u0631\u0634\u062d \u0628\u064a\u0627\u0646\u0627\u062a \u0635\u0627\u0631\u0645.',
      'R\u00f4les et privil\u00e8ges : des r\u00f4les d\u00e9finis par le laboratoire sur le catalogue de permissions. Chaque r\u00f4le accorde des permissions module-action et peut \u00eatre restreint par site et service comme filtre de donn\u00e9es strict.'),
    steps: [],
    usage: [
      L('Create a role, tick the module-action cells it should grant, and assign users to it from the Users page.',
        '\u0623\u0646\u0634\u0626 \u062f\u0648\u0631\u0627\u064b\u060c \u062d\u062f\u062f \u062e\u0644\u0627\u064a\u0627 \u0627\u0644\u0648\u062d\u062f\u0629-\u0627\u0644\u0625\u062c\u0631\u0627\u0621 \u0627\u0644\u062a\u064a \u064a\u0645\u0646\u062d\u0647\u0627\u060c \u062b\u0645 \u0623\u0633\u0646\u062f \u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645\u064a\u0646 \u0625\u0644\u064a\u0647 \u0645\u0646 \u0635\u0641\u062d\u0629 \u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645\u064a\u0646.',
        'Cr\u00e9ez un r\u00f4le, cochez les cellules module-action \u00e0 accorder, puis affectez les utilisateurs depuis la page Utilisateurs.'),
      L('A revoked permission takes effect on the next request \u2014 privileges are resolved from the database per call, not cached in the session token.',
        '\u0627\u0644\u0635\u0644\u0627\u062d\u064a\u0629 \u0627\u0644\u0645\u0644\u063a\u0627\u0629 \u062a\u0633\u0631\u064a \u0645\u0646 \u0627\u0644\u0637\u0644\u0628 \u0627\u0644\u062a\u0627\u0644\u064a \u2014 \u062a\u064f\u062d\u0644 \u0627\u0644\u0635\u0644\u0627\u062d\u064a\u0627\u062a \u0645\u0646 \u0642\u0627\u0639\u062f\u0629 \u0627\u0644\u0628\u064a\u0627\u0646\u0627\u062a \u0639\u0646\u062f \u0643\u0644 \u0646\u062f\u0627\u0621 \u0648\u0644\u0627 \u062a\u064f\u062e\u0632\u0646 \u0641\u064a \u0631\u0645\u0632 \u0627\u0644\u062c\u0644\u0633\u0629.',
        'Une permission r\u00e9voqu\u00e9e prend effet \u00e0 la requ\u00eate suivante \u2014 les privil\u00e8ges sont r\u00e9solus en base \u00e0 chaque appel, jamais mis en cache dans le jeton.'),
      L('Branch/department restrictions are a data filter, not just menu visibility: restricted users cannot read or write records outside their scope.',
        '\u0642\u064a\u0648\u062f \u0627\u0644\u0641\u0631\u0639/\u0627\u0644\u0642\u0633\u0645 \u0645\u0631\u0634\u062d \u0628\u064a\u0627\u0646\u0627\u062a \u0648\u0644\u064a\u0633\u062a \u0645\u062c\u0631\u062f \u0625\u0638\u0647\u0627\u0631 \u0642\u0648\u0627\u0626\u0645: \u0644\u0627 \u064a\u0642\u0631\u0623 \u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645 \u0627\u0644\u0645\u0642\u064a\u062f \u0633\u062c\u0644\u0627\u062a \u062e\u0627\u0631\u062c \u0646\u0637\u0627\u0642\u0647 \u0648\u0644\u0627 \u064a\u0643\u062a\u0628\u0647\u0627.',
        'Les restrictions site/service sont un filtre de donn\u00e9es, pas une simple visibilit\u00e9 de menu : un utilisateur restreint ne lit ni n\u2019\u00e9crit hors de son p\u00e9rim\u00e8tre.'),
    ],
  },
  {
    route: '/access-reviews', titleKey: 'nav.accessReviews', groupKey: 'nav.groupAdmin', icon: 'accessReview',
    summary: L(
      'Periodic user-access reviews (ISO 27001 heritage, Part 11 \u00a711.10(g)): open a review, attest each account is still appropriate, and close it as a signed record.',
      '\u0645\u0631\u0627\u062c\u0639\u0627\u062a \u062f\u0648\u0631\u064a\u0629 \u0644\u0648\u0635\u0648\u0644 \u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645\u064a\u0646: \u0627\u0641\u062a\u062d \u0645\u0631\u0627\u062c\u0639\u0629\u060c \u0648\u0623\u0643\u062f \u0645\u0644\u0627\u0621\u0645\u0629 \u0643\u0644 \u062d\u0633\u0627\u0628\u060c \u062b\u0645 \u0623\u063a\u0644\u0642\u0647\u0627 \u0643\u0633\u062c\u0644 \u0645\u0648\u0642\u0651\u0639.',
      'Revues p\u00e9riodiques des acc\u00e8s utilisateurs : ouvrez une revue, attestez que chaque compte reste appropri\u00e9, puis cl\u00f4turez-la comme enregistrement sign\u00e9.'),
    steps: [
      S(L('Open', '\u0645\u0641\u062a\u0648\u062d\u0629', 'Ouverte'), L('Start a review of every active account and its role.', '\u0627\u0628\u062f\u0623 \u0645\u0631\u0627\u062c\u0639\u0629 \u0643\u0644 \u062d\u0633\u0627\u0628 \u0646\u0634\u0637 \u0648\u062f\u0648\u0631\u0647.', 'Lancez une revue de chaque compte actif et de son r\u00f4le.')),
      S(L('Closed', '\u0645\u063a\u0644\u0642\u0629', 'Cl\u00f4tur\u00e9e'), L('Attested and closed; the outcome is retained as evidence.', '\u0645\u0624\u0643\u062f\u0629 \u0648\u0645\u063a\u0644\u0642\u0629\u061b \u0648\u062a\u062d\u0641\u0638 \u0627\u0644\u0646\u062a\u064a\u062c\u0629 \u0643\u062f\u0644\u064a\u0644.', 'Attest\u00e9e et cl\u00f4tur\u00e9e ; le r\u00e9sultat est conserv\u00e9 comme preuve.')),
    ],
    usage: [
      L('Run a review on a fixed calendar (e.g. quarterly); the register shows when the last one closed.',
        '\u0646\u0641\u0630 \u0627\u0644\u0645\u0631\u0627\u062c\u0639\u0629 \u0648\u0641\u0642 \u062c\u062f\u0648\u0644 \u062b\u0627\u0628\u062a (\u0631\u0628\u0639 \u0633\u0646\u0648\u064a \u0645\u062b\u0644\u0627\u064b)\u061b \u0648\u064a\u0638\u0647\u0631 \u0627\u0644\u0633\u062c\u0644 \u0645\u062a\u0649 \u0623\u064f\u063a\u0644\u0642\u062a \u0627\u0644\u0623\u062e\u064a\u0631\u0629.',
        'Ex\u00e9cutez la revue selon un calendrier fixe (p. ex. trimestriel) ; le registre montre la derni\u00e8re cl\u00f4ture.'),
      L('Revoke or adjust anything inappropriate from the Users and Roles pages before closing the review.',
        '\u0623\u0644\u063a\u0650 \u0623\u0648 \u0639\u062f\u0651\u0644 \u0623\u064a \u0648\u0635\u0648\u0644 \u063a\u064a\u0631 \u0645\u0644\u0627\u0626\u0645 \u0645\u0646 \u0635\u0641\u062d\u062a\u064a \u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645\u064a\u0646 \u0648\u0627\u0644\u0623\u062f\u0648\u0627\u0631 \u0642\u0628\u0644 \u0625\u063a\u0644\u0627\u0642 \u0627\u0644\u0645\u0631\u0627\u062c\u0639\u0629.',
        'R\u00e9voquez ou ajustez tout acc\u00e8s inappropri\u00e9 depuis Utilisateurs et R\u00f4les avant de cl\u00f4turer.'),
    ],
  },
  {
    route: '/settings/security', titleKey: 'nav.security', groupKey: 'nav.groupAdmin', icon: 'security',
    summary: L(
      'Your security settings: multi-factor authentication enrolment, and \u2014 for tenant administrators \u2014 the tenant-wide MFA enforcement policy.',
      '\u0625\u0639\u062f\u0627\u062f\u0627\u062a \u0623\u0645\u0627\u0646\u0643: \u062a\u0641\u0639\u064a\u0644 \u0627\u0644\u0645\u0635\u0627\u062f\u0642\u0629 \u0645\u062a\u0639\u062f\u062f\u0629 \u0627\u0644\u0639\u0648\u0627\u0645\u0644\u060c \u0648\u0644\u0645\u062f\u064a\u0631\u064a \u0627\u0644\u0645\u0633\u062a\u0623\u062c\u0631 \u0633\u064a\u0627\u0633\u0629 \u0641\u0631\u0636 MFA \u0639\u0644\u0649 \u0645\u0633\u062a\u0648\u0649 \u0627\u0644\u0645\u0633\u062a\u0623\u062c\u0631.',
      'Vos param\u00e8tres de s\u00e9curit\u00e9 : enr\u00f4lement MFA et, pour les administrateurs, la politique d\u2019exigence MFA du laboratoire.'),
    steps: [],
    usage: [
      L('Enrol MFA from here; privileged users of an enforcing tenant must enrol before they get a full session.',
        '\u0641\u0639\u0651\u0644 MFA \u0645\u0646 \u0647\u0646\u0627\u061b \u0648\u064a\u062c\u0628 \u0639\u0644\u0649 \u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645\u064a\u0646 \u0630\u0648\u064a \u0627\u0644\u0627\u0645\u062a\u064a\u0627\u0632\u0627\u062a \u0641\u064a \u0645\u0633\u062a\u0623\u062c\u0631 \u0645\u0641\u0639\u0651\u0644 \u0644\u0644\u0625\u0644\u0632\u0627\u0645 \u0627\u0644\u062a\u0641\u0639\u064a\u0644 \u0642\u0628\u0644 \u0627\u0644\u062d\u0635\u0648\u0644 \u0639\u0644\u0649 \u062c\u0644\u0633\u0629 \u0643\u0627\u0645\u0644\u0629.',
        'Enr\u00f4lez le MFA ici ; les utilisateurs privil\u00e9gi\u00e9s d\u2019un laboratoire l\u2019exigeant doivent s\u2019enr\u00f4ler avant d\u2019obtenir une session compl\u00e8te.'),
      L('Your password and e-signature PIN are changed from the account menu in the header (your name, top right).',
        '\u062a\u064f\u063a\u064a\u0651\u0631 \u0643\u0644\u0645\u0629 \u0627\u0644\u0645\u0631\u0648\u0631 \u0648\u0631\u0642\u0645 PIN \u0644\u0644\u062a\u0648\u0642\u064a\u0639 \u0627\u0644\u0625\u0644\u0643\u062a\u0631\u0648\u0646\u064a \u0645\u0646 \u0642\u0627\u0626\u0645\u0629 \u0627\u0644\u062d\u0633\u0627\u0628 \u0641\u064a \u0627\u0644\u062a\u0631\u0648\u064a\u0633\u0629 (\u0627\u0633\u0645\u0643 \u0623\u0639\u0644\u0649 \u0627\u0644\u0635\u0641\u062d\u0629).',
        'Le mot de passe et le code PIN de signature se changent depuis le menu du compte dans l\u2019en-t\u00eate (votre nom, en haut).'),
    ],
  },
  {
    route: '/platform/tenants', titleKey: 'nav.tenants', groupKey: 'nav.groupPlatform', icon: 'tenants',
    summary: L(
      'Platform administration: provision laboratory tenants, activate or suspend them, and monitor provisioning state. Visible to platform administrators only.',
      '\u0625\u062f\u0627\u0631\u0629 \u0627\u0644\u0645\u0646\u0635\u0629: \u062a\u062c\u0647\u064a\u0632 \u0645\u0633\u062a\u0623\u062c\u0631\u064a \u0627\u0644\u0645\u062e\u062a\u0628\u0631\u0627\u062a\u060c \u0648\u062a\u0641\u0639\u064a\u0644\u0647\u0645 \u0623\u0648 \u0625\u064a\u0642\u0627\u0641\u0647\u0645\u060c \u0648\u0645\u0631\u0627\u0642\u0628\u0629 \u062d\u0627\u0644\u0629 \u0627\u0644\u062a\u062c\u0647\u064a\u0632. \u0645\u0631\u0626\u064a\u0629 \u0644\u0645\u062f\u064a\u0631\u064a \u0627\u0644\u0645\u0646\u0635\u0629 \u0641\u0642\u0637.',
      'Administration de la plateforme : provisionnez les laboratoires, activez ou suspendez-les, surveillez l\u2019\u00e9tat du provisionnement. Visible des administrateurs plateforme uniquement.'),
    steps: [],
    usage: [
      L('Provision a tenant with its slug and first administrator; seeding creates the default roles and lists.',
        '\u062c\u0647\u0651\u0632 \u0645\u0633\u062a\u0623\u062c\u0631\u0627\u064b \u0628\u0627\u0644\u0645\u0639\u0631\u0641 \u0648\u0627\u0644\u0645\u062f\u064a\u0631 \u0627\u0644\u0623\u0648\u0644\u061b \u0648\u064a\u0646\u0634\u0626 \u0627\u0644\u062a\u0623\u0633\u064a\u0633 \u0627\u0644\u0623\u062f\u0648\u0627\u0631 \u0648\u0627\u0644\u0642\u0648\u0627\u0626\u0645 \u0627\u0644\u0627\u0641\u062a\u0631\u0627\u0636\u064a\u0629.',
        'Provisionnez un laboratoire avec son identifiant et son premier administrateur ; l\u2019amor\u00e7age cr\u00e9e les r\u00f4les et listes par d\u00e9faut.'),
      L('Suspending a tenant blocks its sign-ins immediately; its data is retained untouched.',
        '\u0625\u064a\u0642\u0627\u0641 \u0627\u0644\u0645\u0633\u062a\u0623\u062c\u0631 \u064a\u0645\u0646\u0639 \u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062f\u062e\u0648\u0644 \u0641\u0648\u0631\u0627\u064b\u061b \u0648\u062a\u0628\u0642\u0649 \u0628\u064a\u0627\u0646\u0627\u062a\u0647 \u062f\u0648\u0646 \u0645\u0633\u0627\u0633.',
        'La suspension bloque imm\u00e9diatement les connexions ; les donn\u00e9es restent intactes.'),
    ],
  },
];

/** Map from top-level route segment → topic, for quick lookup by URL. */
const TOPIC_BY_SEGMENT = new Map(
  HELP_TOPICS.map((t) => [t.route.replace(/^\//, ''), t] as const),
);

/**
 * Resolve the help topic for a router URL. The full cleaned path is tried first
 * so multi-segment pages ('/settings/security', '/platform/tenants') can carry
 * their own topics; it then falls back to the first segment, which is what lets
 * a detail child ('/nonconformances/abc') inherit its list page's topic.
 * Returns undefined for pages without registered help.
 */
export function helpTopicForUrl(url: string): HelpTopic | undefined {
  const clean = url.split('?')[0].split('#')[0].replace(/^\//, '');
  return TOPIC_BY_SEGMENT.get(clean) ?? TOPIC_BY_SEGMENT.get(clean.split('/')[0]);
}

/** Distinct group keys in sidebar order, for the User Manual’s section list. */
export function helpGroupsInOrder(): string[] {
  const seen: string[] = [];
  for (const t of HELP_TOPICS) {
    if (!seen.includes(t.groupKey)) { seen.push(t.groupKey); }
  }
  return seen;
}
