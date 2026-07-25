using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Organization;
using NT.QAMS.SharedKernel.Localization;

namespace NT.QAMS.Application.Organization;

/// <summary>
/// The starter list-of-values catalog: every LOV category the UI offers ships
/// with professional example entries (en/ar/fr) so a new tenant is usable on
/// day one — users refine the lists instead of facing empty dropdowns.
/// Seeding is per-category and additive: a category in which the tenant has
/// ANY entries (even inactive ones) is left untouched, so curated lists are
/// never polluted by defaults.
/// </summary>
public static class DefaultLovCatalog
{
    private sealed record Entry(string Code, string En, string Ar, string Fr);

    private static readonly IReadOnlyDictionary<string, Entry[]> Catalog = new Dictionary<string, Entry[]>
    {
        ["DOC_CATEGORY"] =
        [
            new("SOP", "Standard Operating Procedure", "إجراء تشغيل قياسي", "Procédure opératoire normalisée"),
            new("POLICY", "Policy", "سياسة", "Politique"),
            new("WORK_INSTRUCTION", "Work Instruction", "تعليمات عمل", "Instruction de travail"),
            new("FORM", "Form / Template", "نموذج / قالب", "Formulaire / Modèle"),
            new("MANUAL", "Quality Manual", "دليل الجودة", "Manuel qualité"),
            new("EXTERNAL", "External Document", "وثيقة خارجية", "Document externe"),
        ],
        ["RISK_CATEGORY"] =
        [
            new("CLINICAL", "Clinical / Patient Safety", "سريري / سلامة المرضى", "Clinique / Sécurité patient"),
            new("OPERATIONAL", "Operational", "تشغيلي", "Opérationnel"),
            new("EQUIPMENT", "Equipment & Infrastructure", "المعدات والبنية التحتية", "Équipements et infrastructure"),
            new("SUPPLY_CHAIN", "Supply Chain", "سلسلة الإمداد", "Chaîne d'approvisionnement"),
            new("INFORMATION", "Information & Data Integrity", "المعلومات وسلامة البيانات", "Information et intégrité des données"),
            new("IMPARTIALITY", "Impartiality", "الحياد", "Impartialité"),
        ],
        ["SUPPLIER_TYPE"] =
        [
            new("REAGENTS", "Reagents & Kits", "كواشف وأطقم", "Réactifs et kits"),
            new("CONSUMABLES", "Consumables", "مستهلكات", "Consommables"),
            new("EQUIPMENT", "Equipment Vendor", "مورد معدات", "Fournisseur d'équipement"),
            new("CALIBRATION", "Calibration Provider", "مزود معايرة", "Prestataire d'étalonnage"),
            new("REFERENCE_LAB", "Referral Laboratory", "مختبر إحالة", "Laboratoire sous-traitant"),
            new("IT_SERVICE", "IT / Software Service", "خدمات تقنية / برمجيات", "Service informatique / logiciel"),
        ],
        ["CERTIFICATE_TYPE"] =
        [
            new("ISO9001", "ISO 9001", "آيزو 9001", "ISO 9001"),
            new("ISO13485", "ISO 13485", "آيزو 13485", "ISO 13485"),
            new("ISO17025", "ISO/IEC 17025", "آيزو/آي إي سي 17025", "ISO/IEC 17025"),
            new("CE_MARK", "CE Marking", "علامة CE", "Marquage CE"),
            new("FDA", "FDA Registration", "تسجيل FDA", "Enregistrement FDA"),
        ],
        ["PT_SCHEME"] =
        [
            new("EQAS_CHEM", "EQAS Clinical Chemistry", "EQAS كيمياء سريرية", "EQAS Chimie clinique"),
            new("EQAS_HEMA", "EQAS Hematology", "EQAS أمراض دم", "EQAS Hématologie"),
            new("RIQAS", "RIQAS", "RIQAS", "RIQAS"),
            new("CAP", "CAP Surveys", "استقصاءات CAP", "Enquêtes CAP"),
            new("UK_NEQAS", "UK NEQAS", "UK NEQAS", "UK NEQAS"),
        ],
        ["EQUIPMENT_LOCATION"] =
        [
            new("MAIN_LAB", "Main Laboratory", "المختبر الرئيسي", "Laboratoire principal"),
            new("CHEMISTRY", "Chemistry Section", "قسم الكيمياء", "Section chimie"),
            new("HEMATOLOGY", "Hematology Section", "قسم أمراض الدم", "Section hématologie"),
            new("MICROBIOLOGY", "Microbiology Section", "قسم الأحياء الدقيقة", "Section microbiologie"),
            new("SAMPLE_RECEPTION", "Sample Reception", "استقبال العينات", "Réception des échantillons"),
            new("COLD_STORAGE", "Cold Storage", "التخزين البارد", "Stockage réfrigéré"),
        ],
        ["INTERMEDIATE_CHECK_TYPE"] =
        [
            new("ZERO_DRIFT", "Zero/drift check", "فحص الصفر/الانحراف", "Vérification zéro/dérive"),
            new("CONTROL_WEIGHT", "Control weight check", "فحص وزن الضبط", "Vérification masse de contrôle"),
            new("CONTROL_SAMPLE", "Control sample run", "تشغيل عينة ضبط", "Passage échantillon de contrôle"),
            new("REF_THERMOMETER", "Reference thermometer comparison", "مقارنة ميزان حرارة مرجعي", "Comparaison thermomètre de référence"),
            new("BALANCE_REPEATABILITY", "Balance repeatability check", "فحص تكرارية الميزان", "Vérification répétabilité balance"),
        ],
        ["ENV_PARAMETER"] =
        [
            new("TEMPERATURE", "Temperature", "درجة الحرارة", "Température"),
            new("HUMIDITY", "Relative humidity", "الرطوبة النسبية", "Humidité relative"),
            new("PRESSURE_DIFF", "Pressure differential", "فرق الضغط", "Différentiel de pression"),
            new("CO2", "CO2 concentration", "تركيز ثاني أكسيد الكربون", "Concentration en CO2"),
        ],
        ["FEEDBACK_SOURCE"] =
        [
            new("CUSTOMER", "Customer", "عميل", "Client"),
            new("PHYSICIAN", "Referring physician", "طبيب محوِّل", "Prescripteur"),
            new("PATIENT", "Patient", "مريض", "Patient"),
            new("STAFF", "Staff", "موظف", "Personnel"),
            new("AUTHORITY", "Regulatory authority", "جهة تنظيمية", "Autorité réglementaire"),
        ],
        ["FEEDBACK_CHANNEL"] =
        [
            new("SURVEY", "Survey", "استبيان", "Enquête"),
            new("EMAIL", "Email", "بريد إلكتروني", "Email"),
            new("PHONE", "Phone", "هاتف", "Téléphone"),
            new("PORTAL", "Portal", "بوابة إلكترونية", "Portail"),
            new("IN_PERSON", "In person", "حضوريًا", "En personne"),
        ],
        ["INTERESTED_PARTY_CATEGORY"] =
        [
            new("CUSTOMER", "Customer", "عميل", "Client"),
            new("REGULATOR", "Regulator", "جهة تنظيمية", "Régulateur"),
            new("ACCREDITOR", "Accreditation body", "جهة اعتماد", "Organisme d'accréditation"),
            new("STAFF", "Staff", "موظفون", "Personnel"),
            new("SUPPLIER", "Supplier / Partner", "مورد / شريك", "Fournisseur / Partenaire"),
            new("OWNER", "Owner / Management", "المالك / الإدارة", "Propriétaire / Direction"),
        ],
        ["CONTEXT_ISSUE_CATEGORY"] =
        [
            new("STRENGTH", "Strength", "قوة", "Force"),
            new("WEAKNESS", "Weakness", "ضعف", "Faiblesse"),
            new("OPPORTUNITY", "Opportunity", "فرصة", "Opportunité"),
            new("THREAT", "Threat", "تهديد", "Menace"),
        ],
    };

    /// <summary>
    /// Inserts the starter entries for every category in which the tenant has
    /// none yet. Returns the number of entries added (0 when fully covered) —
    /// idempotent, so it is safe at provisioning and as a startup backfill.
    /// </summary>
    public static async Task<int> SeedMissingAsync(IAppDbContext db, Guid tenantId, CancellationToken ct)
    {
        // Bypasses the request-tenant query filter — this runs at provisioning
        // and in startup scopes where no tenant is resolved.
        var covered = await db.LovEntries.IgnoreQueryFilters()
            .Where(l => l.TenantId == tenantId)
            .Select(l => l.Category)
            .Distinct()
            .ToListAsync(ct);
        var coveredSet = covered.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var (category, entries) in Catalog)
        {
            if (coveredSet.Contains(category))
            {
                continue; // The tenant curates this list — never mix defaults in.
            }

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = LovEntry.Create(
                    category, entries[i].Code,
                    LocalizedText.Create(entries[i].En, entries[i].Ar, entries[i].Fr),
                    sortOrder: i + 1);
                entry.TenantId = tenantId;
                db.LovEntries.Add(entry);
                added++;
            }
        }

        return added;
    }
}
