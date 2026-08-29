# ADR-0011 — ADT raw-payload retention and patient-identifier masking

- **Status:** Accepted (2026-08-29)
- **Finding:** HQMS conformance audit M-12 (`E:\QMS\NT_QAMS_HQMS_Audit_Register_2026-08-28.md`)
- **Related:** ADR-0006 (outbox), the RLS tenant-isolation model, `IntegrationMessage`, HQMS release note RN-NTQMS-HQMS01-001

## Context

The ADT integration inbox stores the raw inbound message (`integration_message.raw_payload`)
so an interface problem can be diagnosed against exactly what arrived. Raw HL7 v2 admit/transfer/
discharge messages carry a **PID segment** with direct patient identifiers — medical record number,
name, date of birth, address, phone, national ID. The audit (M-12) flagged that this payload was
stored **verbatim and forever**: a growing store of PHI whose only protections were RLS and
at-rest encryption, retained with no expiry — a privacy and data-minimisation exposure that must be
resolved before any PHI-bearing feed is connected.

The canonical values the system actually uses — `PatientRef` and `EncounterRef` — are extracted
into the `PatientStay` projection at ingest; the stored payload exists only for troubleshooting,
where the message *structure* matters and the identifiers do not.

## Decision

**Mask patient identifiers at store time, and purge the stored payload after a retention window.**

1. **Masking at capture.** `IntegrationMessage.Receive` runs the raw payload through
   `Hl7Redaction.MaskPatientIdentifiers` before persisting it. The masker replaces the value of the
   PID segment's direct-identifier fields (PID-2..7, 11, 13, 14, 19, 20 — IDs/MRN, names, DOB,
   address, phones, SSN, licence) with `***`, leaving every other segment and all field delimiters
   intact so the message shape survives for diagnosis. It never throws on malformed input (an
   unparseable line is returned unchanged rather than risk dropping the record). No un-masked
   payload is ever written.

2. **Retention purge.** `IntegrationPayloadRetentionService` (a leader-elected background job, like
   the compliance sweep) tombstones the `raw_payload` of every **settled** message
   (Processed/Failed) older than `Integration:PayloadRetentionDays` (**default 90 days**, clamped
   1–3650) to `«purged»`. The row itself — status, error detail, timings, dedup key — stays as the
   durable interface-health record; only the payload text is dropped. Received-but-unsettled
   messages keep their payload so an in-flight problem can still be diagnosed.

## Consequences

- After the window, no message body — masked or not — remains; within it, only masked bodies exist.
  Both layers are defence-in-depth: masking bounds what is ever stored, retention bounds how long.
- Operators can tune or extend the window per deployment via configuration; the default is
  deliberately short (90 days) for a PHI-minimising posture.
- Troubleshooting keeps the message structure and the non-PID segments, which is what interface
  debugging needs; a case needing the literal identifiers must go to the source system's own log,
  by design.
- The retention job is single-writer (advisory lock) and idempotent (a tombstoned row matches no
  later round), consistent with the other sweeps.
- This ADR is the M-12 close-out for retention/PHI; it must be in force before a production PHI feed
  is connected (recorded as a pre-go-live condition in FRA doc 02 and REVAL-NTQMS-002).
