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

  // ── Improvement ───────────────────────────────────────────────────────────
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
];

/** Map from top-level route segment → topic, for quick lookup by URL. */
const TOPIC_BY_SEGMENT = new Map(
  HELP_TOPICS.map((t) => [t.route.replace(/^\//, ''), t] as const),
);

/**
 * Resolve the help topic for a router URL, matching on the first path segment
 * (so '/nonconformances/abc' → the nonconformances topic). Returns undefined
 * for pages without registered help.
 */
export function helpTopicForUrl(url: string): HelpTopic | undefined {
  const clean = url.split('?')[0].split('#')[0].replace(/^\//, '');
  const segment = clean.split('/')[0];
  return TOPIC_BY_SEGMENT.get(segment);
}

/** Distinct group keys in sidebar order, for the User Manual’s section list. */
export function helpGroupsInOrder(): string[] {
  const seen: string[] = [];
  for (const t of HELP_TOPICS) {
    if (!seen.includes(t.groupKey)) { seen.push(t.groupKey); }
  }
  return seen;
}
