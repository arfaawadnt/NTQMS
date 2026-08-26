CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721211309_InitialFoundation') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'qams') THEN
            CREATE SCHEMA qams;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721211309_InitialFoundation') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'saas') THEN
            CREATE SCHEMA saas;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721211309_InitialFoundation') THEN
    CREATE TABLE qams.outbox_event (
        id uuid NOT NULL,
        tenant_id uuid,
        event_type character varying(400) NOT NULL,
        payload text NOT NULL,
        occurred_at_utc timestamp with time zone NOT NULL,
        processed_at_utc timestamp with time zone,
        attempts integer NOT NULL,
        last_error character varying(2000),
        CONSTRAINT pk_outbox_event PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721211309_InitialFoundation') THEN
    CREATE TABLE saas.tenant (
        id uuid NOT NULL,
        identifier character varying(50) NOT NULL,
        name character varying(200) NOT NULL,
        status character varying(20) NOT NULL,
        password_expiry_days integer NOT NULL,
        calibration_reminder_days integer NOT NULL,
        sop_expiry_reminder_months integer NOT NULL,
        default_language character varying(5) NOT NULL,
        time_zone character varying(60) NOT NULL,
        suspension_reason character varying(500),
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_tenant PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721211309_InitialFoundation') THEN
    CREATE INDEX ix_outbox_event_pending ON qams.outbox_event (occurred_at_utc) WHERE processed_at_utc IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721211309_InitialFoundation') THEN
    CREATE UNIQUE INDEX ix_tenant_identifier ON saas.tenant (identifier);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721211309_InitialFoundation') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260721211309_InitialFoundation', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721214118_IdentityAndImprovement') THEN
    CREATE TABLE qams.nonconformance (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        nc_ref character varying(30) NOT NULL,
        title character varying(300) NOT NULL,
        description character varying(4000) NOT NULL,
        severity integer NOT NULL,
        likelihood integer NOT NULL,
        rpn integer NOT NULL,
        source_type character varying(30) NOT NULL,
        status character varying(30) NOT NULL,
        raised_by uuid NOT NULL,
        assigned_to uuid,
        rejection_reason character varying(1000),
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_nonconformance PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721214118_IdentityAndImprovement') THEN
    CREATE TABLE qams.ref_counter (
        tenant_id uuid NOT NULL,
        ref_type character varying(10) NOT NULL,
        year integer NOT NULL,
        last_value bigint NOT NULL,
        CONSTRAINT pk_ref_counter PRIMARY KEY (tenant_id, ref_type, year)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721214118_IdentityAndImprovement') THEN
    CREATE TABLE qams.user_account (
        id uuid NOT NULL,
        tenant_id uuid,
        email character varying(320) NOT NULL,
        display_name character varying(150) NOT NULL,
        password_hash character varying(500) NOT NULL,
        role character varying(30) NOT NULL,
        is_active boolean NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_user_account PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721214118_IdentityAndImprovement') THEN
    CREATE TABLE qams.capa_action (
        id uuid NOT NULL,
        type character varying(20) NOT NULL,
        details character varying(2000) NOT NULL,
        owner_id uuid NOT NULL,
        due_date date NOT NULL,
        status character varying(20) NOT NULL,
        completed_at_utc timestamp with time zone,
        nc_id uuid NOT NULL,
        CONSTRAINT pk_capa_action PRIMARY KEY (id),
        CONSTRAINT fk_capa_action_nonconformance_nc_id FOREIGN KEY (nc_id) REFERENCES qams.nonconformance (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721214118_IdentityAndImprovement') THEN
    CREATE TABLE qams.rca_record (
        id uuid NOT NULL,
        method character varying(20) NOT NULL,
        analysis character varying(8000) NOT NULL,
        investigator_id uuid NOT NULL,
        nc_id uuid NOT NULL,
        CONSTRAINT pk_rca_record PRIMARY KEY (id),
        CONSTRAINT fk_rca_record_nonconformance_nc_id FOREIGN KEY (nc_id) REFERENCES qams.nonconformance (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721214118_IdentityAndImprovement') THEN
    CREATE INDEX ix_capa_action_nc_id ON qams.capa_action (nc_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721214118_IdentityAndImprovement') THEN
    CREATE UNIQUE INDEX ix_nonconformance_tenant_id_nc_ref ON qams.nonconformance (tenant_id, nc_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721214118_IdentityAndImprovement') THEN
    CREATE INDEX ix_nonconformance_tenant_id_status ON qams.nonconformance (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721214118_IdentityAndImprovement') THEN
    CREATE INDEX ix_rca_record_nc_id ON qams.rca_record (nc_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721214118_IdentityAndImprovement') THEN
    ALTER TABLE qams.nonconformance ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation ON qams.nonconformance
        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721214118_IdentityAndImprovement') THEN
    CREATE UNIQUE INDEX ix_user_account_tenant_id_email ON qams.user_account (tenant_id, email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721214118_IdentityAndImprovement') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260721214118_IdentityAndImprovement', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721215255_DocumentControl') THEN
    CREATE TABLE qams.controlled_document (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        code character varying(40) NOT NULL,
        title character varying(300) NOT NULL,
        category character varying(50) NOT NULL,
        status character varying(20) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_controlled_document PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721215255_DocumentControl') THEN
    CREATE TABLE qams.file_reference (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        file_name character varying(260) NOT NULL,
        content_type character varying(150) NOT NULL,
        sha256 character varying(64) NOT NULL,
        size_bytes bigint NOT NULL,
        storage_key character varying(120) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_file_reference PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721215255_DocumentControl') THEN
    CREATE TABLE qams.document_version (
        id uuid NOT NULL,
        major integer NOT NULL,
        minor integer NOT NULL,
        file_id uuid NOT NULL,
        change_summary character varying(1000) NOT NULL,
        state character varying(20) NOT NULL,
        author_id uuid NOT NULL,
        recommended_by uuid,
        recommended_at_utc timestamp with time zone,
        approved_by uuid,
        approved_at_utc timestamp with time zone,
        rejection_reason character varying(1000),
        document_id uuid NOT NULL,
        CONSTRAINT pk_document_version PRIMARY KEY (id),
        CONSTRAINT fk_document_version_controlled_document_document_id FOREIGN KEY (document_id) REFERENCES qams.controlled_document (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721215255_DocumentControl') THEN
    CREATE UNIQUE INDEX ix_controlled_document_tenant_id_code ON qams.controlled_document (tenant_id, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721215255_DocumentControl') THEN
    CREATE INDEX ix_controlled_document_tenant_id_status ON qams.controlled_document (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721215255_DocumentControl') THEN
    CREATE INDEX ix_document_version_document_id ON qams.document_version (document_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721215255_DocumentControl') THEN
    CREATE INDEX ix_file_reference_tenant_id_sha256 ON qams.file_reference (tenant_id, sha256);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721215255_DocumentControl') THEN
    ALTER TABLE qams.controlled_document ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation ON qams.controlled_document
        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
    ALTER TABLE qams.file_reference ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation ON qams.file_reference
        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721215255_DocumentControl') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260721215255_DocumentControl', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721220535_AuditManagement') THEN
    ALTER TABLE qams.nonconformance ADD source_ref text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721220535_AuditManagement') THEN
    CREATE TABLE qams.audit (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        audit_ref character varying(30) NOT NULL,
        title character varying(300) NOT NULL,
        type character varying(20) NOT NULL,
        lead_auditor_id uuid NOT NULL,
        planned_date date NOT NULL,
        status character varying(20) NOT NULL,
        signed_off_by uuid,
        signed_off_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_audit PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721220535_AuditManagement') THEN
    CREATE TABLE qams.audit_checklist_item (
        id uuid NOT NULL,
        iso_clause character varying(30) NOT NULL,
        question character varying(1000) NOT NULL,
        verdict character varying(20) NOT NULL,
        evidence character varying(2000),
        audit_id uuid NOT NULL,
        CONSTRAINT pk_audit_checklist_item PRIMARY KEY (id),
        CONSTRAINT fk_audit_checklist_item_audit_audit_id FOREIGN KEY (audit_id) REFERENCES qams.audit (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721220535_AuditManagement') THEN
    CREATE TABLE qams.audit_finding (
        id uuid NOT NULL,
        grade character varying(20) NOT NULL,
        description character varying(4000) NOT NULL,
        nc_id uuid,
        audit_id uuid NOT NULL,
        CONSTRAINT pk_audit_finding PRIMARY KEY (id),
        CONSTRAINT fk_audit_finding_audit_audit_id FOREIGN KEY (audit_id) REFERENCES qams.audit (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721220535_AuditManagement') THEN
    CREATE UNIQUE INDEX ix_audit_tenant_id_audit_ref ON qams.audit (tenant_id, audit_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721220535_AuditManagement') THEN
    CREATE INDEX ix_audit_tenant_id_status ON qams.audit (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721220535_AuditManagement') THEN
    CREATE INDEX ix_audit_checklist_item_audit_id ON qams.audit_checklist_item (audit_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721220535_AuditManagement') THEN
    CREATE INDEX ix_audit_finding_audit_id ON qams.audit_finding (audit_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721220535_AuditManagement') THEN
    ALTER TABLE qams.audit ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation ON qams.audit
        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721220535_AuditManagement') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260721220535_AuditManagement', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    CREATE TABLE qams.competency_record (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        trainee_id uuid NOT NULL,
        subject character varying(300) NOT NULL,
        document_id uuid,
        status character varying(20) NOT NULL,
        validity_months integer NOT NULL,
        expires_at date,
        authorized_by uuid,
        revocation_reason character varying(1000),
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_competency_record PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    CREATE TABLE qams.equipment_item (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        code character varying(30) NOT NULL,
        name character varying(200) NOT NULL,
        serial_number character varying(100) NOT NULL,
        location character varying(200),
        status character varying(20) NOT NULL,
        calibration_interval_days integer NOT NULL,
        grace_period_days integer NOT NULL,
        last_calibration_at date,
        next_calibration_due date,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_equipment_item PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    CREATE TABLE qams.training_assignment (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        trainee_id uuid NOT NULL,
        subject character varying(300) NOT NULL,
        document_id uuid,
        due_date date NOT NULL,
        completed boolean NOT NULL,
        completed_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_training_assignment PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    CREATE TABLE qams.assessment_result (
        id uuid NOT NULL,
        score integer NOT NULL,
        assessor_id uuid NOT NULL,
        assessed_at_utc timestamp with time zone NOT NULL,
        competency_id uuid NOT NULL,
        CONSTRAINT pk_assessment_result PRIMARY KEY (id),
        CONSTRAINT fk_assessment_result_competency_record_competency_id FOREIGN KEY (competency_id) REFERENCES qams.competency_record (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    CREATE TABLE qams.calibration_record (
        id uuid NOT NULL,
        performed_at date NOT NULL,
        provider character varying(200) NOT NULL,
        result character varying(500) NOT NULL,
        certificate_file_id uuid,
        equipment_id uuid NOT NULL,
        CONSTRAINT pk_calibration_record PRIMARY KEY (id),
        CONSTRAINT fk_calibration_record_equipment_item_equipment_id FOREIGN KEY (equipment_id) REFERENCES qams.equipment_item (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    CREATE TABLE qams.maintenance_record (
        id uuid NOT NULL,
        performed_at date NOT NULL,
        work_description character varying(2000) NOT NULL,
        equipment_id uuid NOT NULL,
        CONSTRAINT pk_maintenance_record PRIMARY KEY (id),
        CONSTRAINT fk_maintenance_record_equipment_item_equipment_id FOREIGN KEY (equipment_id) REFERENCES qams.equipment_item (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    CREATE INDEX ix_assessment_result_competency_id ON qams.assessment_result (competency_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    CREATE INDEX ix_calibration_record_equipment_id ON qams.calibration_record (equipment_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    CREATE INDEX ix_competency_record_tenant_id_status ON qams.competency_record (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    CREATE INDEX ix_competency_record_tenant_id_trainee_id ON qams.competency_record (tenant_id, trainee_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    CREATE UNIQUE INDEX ix_equipment_item_tenant_id_code ON qams.equipment_item (tenant_id, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    CREATE UNIQUE INDEX ix_equipment_item_tenant_id_serial_number ON qams.equipment_item (tenant_id, serial_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    CREATE INDEX ix_equipment_item_tenant_id_status ON qams.equipment_item (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    CREATE INDEX ix_maintenance_record_equipment_id ON qams.maintenance_record (equipment_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    CREATE INDEX ix_training_assignment_tenant_id_trainee_id_completed ON qams.training_assignment (tenant_id, trainee_id, completed);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    ALTER TABLE qams.equipment_item ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation ON qams.equipment_item
        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
    ALTER TABLE qams.competency_record ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation ON qams.competency_record
        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
    ALTER TABLE qams.training_assignment ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation ON qams.training_assignment
        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721221903_ResourcesModules') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260721221903_ResourcesModules', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE TABLE qams.change_request (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        change_ref character varying(30) NOT NULL,
        title character varying(300) NOT NULL,
        impact_analysis character varying(4000) NOT NULL,
        proposed_by uuid NOT NULL,
        risk_item_id uuid,
        status character varying(20) NOT NULL,
        approved_by uuid,
        approved_at_utc timestamp with time zone,
        rejection_reason character varying(1000),
        implementation_notes character varying(4000),
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_change_request PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE TABLE qams.management_review (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        review_ref character varying(30) NOT NULL,
        title character varying(300) NOT NULL,
        review_date date NOT NULL,
        participants character varying(2000) NOT NULL,
        status character varying(20) NOT NULL,
        minutes character varying(20000),
        closed_by uuid,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_management_review PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE TABLE qams.risk_item (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        risk_ref character varying(30) NOT NULL,
        title character varying(300) NOT NULL,
        category character varying(50) NOT NULL,
        likelihood integer NOT NULL,
        impact integer NOT NULL,
        rpn integer NOT NULL,
        residual_likelihood integer,
        residual_impact integer,
        residual_rpn integer,
        status character varying(20) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_risk_item PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE TABLE qams.supplier (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        supplier_ref character varying(30) NOT NULL,
        name character varying(200) NOT NULL,
        supplier_type character varying(50) NOT NULL,
        registered_by uuid NOT NULL,
        status character varying(25) NOT NULL,
        approved_by uuid,
        suspension_reason character varying(500),
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_supplier PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE TABLE qams.supplier_evaluation (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        supplier_id uuid NOT NULL,
        period_start date NOT NULL,
        period_end date NOT NULL,
        criteria_json character varying(8000) NOT NULL,
        weighted_total numeric(5,2) NOT NULL,
        evaluated_by uuid NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_supplier_evaluation PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE TABLE qams.review_decision (
        id uuid NOT NULL,
        description character varying(2000) NOT NULL,
        owner_id uuid NOT NULL,
        due_date date NOT NULL,
        review_id uuid NOT NULL,
        CONSTRAINT pk_review_decision PRIMARY KEY (id),
        CONSTRAINT fk_review_decision_management_review_review_id FOREIGN KEY (review_id) REFERENCES qams.management_review (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE TABLE qams.mitigation_action (
        id uuid NOT NULL,
        description character varying(2000) NOT NULL,
        owner_id uuid NOT NULL,
        due_date date NOT NULL,
        completed boolean NOT NULL,
        risk_id uuid NOT NULL,
        CONSTRAINT pk_mitigation_action PRIMARY KEY (id),
        CONSTRAINT fk_mitigation_action_risk_item_risk_id FOREIGN KEY (risk_id) REFERENCES qams.risk_item (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE TABLE qams.supplier_certificate (
        id uuid NOT NULL,
        certificate_type character varying(100) NOT NULL,
        expires_at date NOT NULL,
        file_id uuid,
        supplier_id uuid NOT NULL,
        CONSTRAINT pk_supplier_certificate PRIMARY KEY (id),
        CONSTRAINT fk_supplier_certificate_supplier_supplier_id FOREIGN KEY (supplier_id) REFERENCES qams.supplier (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE UNIQUE INDEX ix_change_request_tenant_id_change_ref ON qams.change_request (tenant_id, change_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE UNIQUE INDEX ix_management_review_tenant_id_review_ref ON qams.management_review (tenant_id, review_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE INDEX ix_mitigation_action_risk_id ON qams.mitigation_action (risk_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE INDEX ix_review_decision_review_id ON qams.review_decision (review_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE UNIQUE INDEX ix_risk_item_tenant_id_risk_ref ON qams.risk_item (tenant_id, risk_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE INDEX ix_risk_item_tenant_id_status ON qams.risk_item (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE INDEX ix_supplier_tenant_id_status ON qams.supplier (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE UNIQUE INDEX ix_supplier_tenant_id_supplier_ref ON qams.supplier (tenant_id, supplier_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE INDEX ix_supplier_certificate_supplier_id ON qams.supplier_certificate (supplier_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    CREATE INDEX ix_supplier_evaluation_tenant_id_supplier_id ON qams.supplier_evaluation (tenant_id, supplier_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    ALTER TABLE qams.risk_item ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation ON qams.risk_item
        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
    ALTER TABLE qams.change_request ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation ON qams.change_request
        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
    ALTER TABLE qams.management_review ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation ON qams.management_review
        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
    ALTER TABLE qams.supplier ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation ON qams.supplier
        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
    ALTER TABLE qams.supplier_evaluation ENABLE ROW LEVEL SECURITY;
    CREATE POLICY tenant_isolation ON qams.supplier_evaluation
        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721223052_GovernanceAndSuppliers') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260721223052_GovernanceAndSuppliers', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721224327_OrgAndNotifications') THEN
    CREATE TABLE qams.branch (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        code character varying(20) NOT NULL,
        name character varying(200) NOT NULL,
        city character varying(100),
        is_active boolean NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_branch PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721224327_OrgAndNotifications') THEN
    CREATE TABLE qams.lov_entry (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        category character varying(50) NOT NULL,
        code character varying(50) NOT NULL,
        name_en character varying(200) NOT NULL,
        name_ar character varying(200),
        name_fr character varying(200),
        sort_order integer NOT NULL,
        is_active boolean NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_lov_entry PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721224327_OrgAndNotifications') THEN
    CREATE TABLE qams.notification_dispatch (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        source_event_id uuid NOT NULL,
        event_key character varying(50) NOT NULL,
        recipient_user_id uuid NOT NULL,
        recipient_email character varying(320),
        subject character varying(400) NOT NULL,
        body character varying(8000) NOT NULL,
        email_status character varying(10) NOT NULL,
        error character varying(1500),
        sent_at_utc timestamp with time zone,
        read_by_recipient boolean NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_notification_dispatch PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721224327_OrgAndNotifications') THEN
    CREATE TABLE qams.notification_rule (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        event_key character varying(50) NOT NULL,
        recipient_roles character varying(300) NOT NULL,
        email_enabled boolean NOT NULL,
        subject_template character varying(300) NOT NULL,
        body_template character varying(4000) NOT NULL,
        is_active boolean NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_notification_rule PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721224327_OrgAndNotifications') THEN
    CREATE TABLE qams.test_catalog_item (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        test_code character varying(30) NOT NULL,
        test_name character varying(200) NOT NULL,
        methodology character varying(300) NOT NULL,
        turnaround_hours integer NOT NULL,
        is_active boolean NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_test_catalog_item PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721224327_OrgAndNotifications') THEN
    CREATE TABLE qams.department (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        branch_id uuid NOT NULL,
        code character varying(20) NOT NULL,
        name character varying(200) NOT NULL,
        is_active boolean NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_department PRIMARY KEY (id),
        CONSTRAINT fk_department_branch_branch_id FOREIGN KEY (branch_id) REFERENCES qams.branch (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721224327_OrgAndNotifications') THEN
    CREATE UNIQUE INDEX ix_branch_tenant_id_code ON qams.branch (tenant_id, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721224327_OrgAndNotifications') THEN
    CREATE INDEX ix_department_branch_id ON qams.department (branch_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721224327_OrgAndNotifications') THEN
    CREATE UNIQUE INDEX ix_department_tenant_id_branch_id_code ON qams.department (tenant_id, branch_id, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721224327_OrgAndNotifications') THEN
    CREATE UNIQUE INDEX ix_lov_entry_tenant_id_category_code ON qams.lov_entry (tenant_id, category, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721224327_OrgAndNotifications') THEN
    CREATE INDEX ix_notification_dispatch_source_event_id ON qams.notification_dispatch (source_event_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721224327_OrgAndNotifications') THEN
    CREATE INDEX ix_notification_dispatch_tenant_id_recipient_user_id_read_by_r ON qams.notification_dispatch (tenant_id, recipient_user_id, read_by_recipient);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721224327_OrgAndNotifications') THEN
    CREATE INDEX ix_notification_rule_tenant_id_event_key ON qams.notification_rule (tenant_id, event_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721224327_OrgAndNotifications') THEN
    CREATE UNIQUE INDEX ix_test_catalog_item_tenant_id_test_code ON qams.test_catalog_item (tenant_id, test_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721224327_OrgAndNotifications') THEN

                    ALTER TABLE qams.branch ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.branch
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    ALTER TABLE qams.department ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.department
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    ALTER TABLE qams.test_catalog_item ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.test_catalog_item
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    ALTER TABLE qams.lov_entry ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.lov_entry
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    ALTER TABLE qams.notification_rule ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.notification_rule
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    ALTER TABLE qams.notification_dispatch ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.notification_dispatch
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721224327_OrgAndNotifications') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260721224327_OrgAndNotifications', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721225752_AnalyticalQuality') THEN
    CREATE TABLE qams.pt_enrollment (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        pt_ref character varying(30) NOT NULL,
        scheme character varying(100) NOT NULL,
        analyte character varying(100) NOT NULL,
        cycle character varying(50) NOT NULL,
        submitted_value numeric(18,6),
        assigned_value numeric(18,6),
        standard_deviation numeric(18,6),
        z_score numeric(10,3),
        performance character varying(20) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_pt_enrollment PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721225752_AnalyticalQuality') THEN
    CREATE TABLE qams.qc_profile (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        analyte character varying(100) NOT NULL,
        instrument character varying(100) NOT NULL,
        control_lot character varying(60) NOT NULL,
        target_mean numeric(18,6) NOT NULL,
        target_sd numeric(18,6) NOT NULL,
        is_active boolean NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_qc_profile PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721225752_AnalyticalQuality') THEN
    CREATE TABLE qams.qc_run (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        profile_id uuid NOT NULL,
        value numeric(18,6) NOT NULL,
        z_score numeric(10,3) NOT NULL,
        outcome character varying(15) NOT NULL,
        violated_rules character varying(60) NOT NULL,
        operator character varying(150) NOT NULL,
        measured_at_utc timestamp with time zone NOT NULL,
        troubleshooting_note character varying(2000),
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_qc_run PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721225752_AnalyticalQuality') THEN
    CREATE TABLE qams.validation_study (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        study_ref character varying(30) NOT NULL,
        analyte character varying(100) NOT NULL,
        protocol character varying(30) NOT NULL,
        total_allowable_error numeric(10,3) NOT NULL,
        state character varying(20) NOT NULL,
        mean_bias numeric(10,3),
        cv numeric(10,3),
        passed boolean,
        signed_off_by uuid,
        signed_off_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_validation_study PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721225752_AnalyticalQuality') THEN
    CREATE TABLE qams.validation_replicate (
        id uuid NOT NULL,
        level character varying(30) NOT NULL,
        measured numeric(18,6) NOT NULL,
        reference numeric(18,6),
        study_id uuid NOT NULL,
        CONSTRAINT pk_validation_replicate PRIMARY KEY (id),
        CONSTRAINT fk_validation_replicate_validation_study_study_id FOREIGN KEY (study_id) REFERENCES qams.validation_study (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721225752_AnalyticalQuality') THEN
    CREATE UNIQUE INDEX ix_pt_enrollment_tenant_id_pt_ref ON qams.pt_enrollment (tenant_id, pt_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721225752_AnalyticalQuality') THEN
    CREATE INDEX ix_qc_profile_tenant_id_analyte_instrument_control_lot ON qams.qc_profile (tenant_id, analyte, instrument, control_lot);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721225752_AnalyticalQuality') THEN
    CREATE INDEX ix_qc_run_tenant_id_profile_id_measured_at_utc ON qams.qc_run (tenant_id, profile_id, measured_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721225752_AnalyticalQuality') THEN
    CREATE INDEX ix_validation_replicate_study_id ON qams.validation_replicate (study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721225752_AnalyticalQuality') THEN
    CREATE UNIQUE INDEX ix_validation_study_tenant_id_study_ref ON qams.validation_study (tenant_id, study_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721225752_AnalyticalQuality') THEN

                    ALTER TABLE qams.qc_profile ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.qc_profile
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    ALTER TABLE qams.qc_run ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.qc_run
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    ALTER TABLE qams.validation_study ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.validation_study
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    ALTER TABLE qams.pt_enrollment ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.pt_enrollment
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721225752_AnalyticalQuality') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260721225752_AnalyticalQuality', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721231005_RecordsAndSla') THEN
    CREATE TABLE qams.archive_entry (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        archive_ref character varying(30) NOT NULL,
        source_module character varying(50) NOT NULL,
        source_ref character varying(60) NOT NULL,
        snapshot_file_id uuid,
        retention_class character varying(20) NOT NULL,
        archived_on date NOT NULL,
        retention_expiry date,
        state character varying(15) NOT NULL,
        archived_by uuid NOT NULL,
        disposal_authorized_by uuid,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_archive_entry PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721231005_RecordsAndSla') THEN
    CREATE TABLE qams.escalation_timer (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        subject_ref character varying(80) NOT NULL,
        owner_user_id uuid NOT NULL,
        deadline timestamp with time zone NOT NULL,
        level integer NOT NULL,
        next_step_at_utc timestamp with time zone,
        active boolean NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_escalation_timer PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721231005_RecordsAndSla') THEN
    CREATE TABLE qams.sla_definition (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        module character varying(50) NOT NULL,
        severity character varying(30) NOT NULL,
        target_hours integer NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_sla_definition PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721231005_RecordsAndSla') THEN
    CREATE TABLE qams.work_task (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        subject character varying(300) NOT NULL,
        subject_ref character varying(80),
        assignee_user_id uuid,
        assignee_role character varying(30),
        due_date date NOT NULL,
        status character varying(15) NOT NULL,
        completed_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_work_task PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721231005_RecordsAndSla') THEN
    CREATE UNIQUE INDEX ix_archive_entry_tenant_id_source_module_source_ref ON qams.archive_entry (tenant_id, source_module, source_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721231005_RecordsAndSla') THEN
    CREATE INDEX ix_archive_entry_tenant_id_state ON qams.archive_entry (tenant_id, state);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721231005_RecordsAndSla') THEN
    CREATE INDEX ix_escalation_timer_next_step_at_utc ON qams.escalation_timer (next_step_at_utc) WHERE active = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721231005_RecordsAndSla') THEN
    CREATE INDEX ix_escalation_timer_subject_ref ON qams.escalation_timer (subject_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721231005_RecordsAndSla') THEN
    CREATE UNIQUE INDEX ix_sla_definition_tenant_id_module_severity ON qams.sla_definition (tenant_id, module, severity);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721231005_RecordsAndSla') THEN
    CREATE INDEX ix_work_task_subject_ref ON qams.work_task (subject_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721231005_RecordsAndSla') THEN
    CREATE INDEX ix_work_task_tenant_id_assignee_role_status ON qams.work_task (tenant_id, assignee_role, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721231005_RecordsAndSla') THEN
    CREATE INDEX ix_work_task_tenant_id_assignee_user_id_status ON qams.work_task (tenant_id, assignee_user_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721231005_RecordsAndSla') THEN

                    ALTER TABLE qams.archive_entry ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.archive_entry
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    ALTER TABLE qams.sla_definition ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.sla_definition
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    ALTER TABLE qams.work_task ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.work_task
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    ALTER TABLE qams.escalation_timer ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.escalation_timer
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721231005_RecordsAndSla') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260721231005_RecordsAndSla', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721232300_ComplianceAndAuth') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'audit') THEN
            CREATE SCHEMA audit;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721232300_ComplianceAndAuth') THEN
    ALTER TABLE qams.user_account ADD failed_login_attempts integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721232300_ComplianceAndAuth') THEN
    ALTER TABLE qams.user_account ADD locked_until_utc timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721232300_ComplianceAndAuth') THEN
    ALTER TABLE qams.user_account ADD mfa_enabled boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721232300_ComplianceAndAuth') THEN
    ALTER TABLE qams.user_account ADD mfa_secret text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721232300_ComplianceAndAuth') THEN
    ALTER TABLE qams.user_account ADD pin_hash text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721232300_ComplianceAndAuth') THEN
    CREATE TABLE audit.audit_trail (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        sequence bigint NOT NULL,
        event_id uuid NOT NULL,
        event_type character varying(400) NOT NULL,
        payload text NOT NULL,
        occurred_at_utc timestamp with time zone NOT NULL,
        prev_hash character varying(64) NOT NULL,
        entry_hash character varying(64) NOT NULL,
        CONSTRAINT pk_audit_trail PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721232300_ComplianceAndAuth') THEN
    CREATE TABLE audit.electronic_signature (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        signer_id uuid NOT NULL,
        signer_display character varying(150) NOT NULL,
        meaning character varying(500) NOT NULL,
        subject_ref character varying(120) NOT NULL,
        content_hash character varying(64) NOT NULL,
        signed_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT pk_electronic_signature PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721232300_ComplianceAndAuth') THEN
    CREATE TABLE audit.security_event (
        id uuid NOT NULL,
        tenant_id uuid,
        event_type character varying(40) NOT NULL,
        actor character varying(320),
        ip_address character varying(60),
        detail character varying(500),
        occurred_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT pk_security_event PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721232300_ComplianceAndAuth') THEN
    CREATE INDEX ix_audit_trail_occurred_at_utc ON audit.audit_trail (occurred_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721232300_ComplianceAndAuth') THEN
    CREATE UNIQUE INDEX ix_audit_trail_tenant_id_sequence ON audit.audit_trail (tenant_id, sequence);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721232300_ComplianceAndAuth') THEN
    CREATE INDEX ix_electronic_signature_subject_ref ON audit.electronic_signature (subject_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721232300_ComplianceAndAuth') THEN
    CREATE INDEX ix_electronic_signature_tenant_id_signed_at_utc ON audit.electronic_signature (tenant_id, signed_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721232300_ComplianceAndAuth') THEN
    CREATE INDEX ix_security_event_occurred_at_utc ON audit.security_event (occurred_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721232300_ComplianceAndAuth') THEN

                    ALTER TABLE audit.audit_trail ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON audit.audit_trail
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    ALTER TABLE audit.electronic_signature ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON audit.electronic_signature
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

                    CREATE OR REPLACE FUNCTION audit.reject_mutation() RETURNS trigger AS $$
                    BEGIN
                        RAISE EXCEPTION 'audit ledgers are append-only';
                    END;
                    $$ LANGUAGE plpgsql;

                    CREATE TRIGGER audit_trail_append_only BEFORE UPDATE OR DELETE ON audit.audit_trail
                        FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                    CREATE TRIGGER signature_append_only BEFORE UPDATE OR DELETE ON audit.electronic_signature
                        FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                    CREATE TRIGGER security_event_append_only BEFORE UPDATE OR DELETE ON audit.security_event
                        FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260721232300_ComplianceAndAuth') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260721232300_ComplianceAndAuth', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724233505_Complaints') THEN
    CREATE TABLE qams.complaint (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        complaint_ref character varying(30) NOT NULL,
        channel character varying(20) NOT NULL,
        complainant_name character varying(300) NOT NULL,
        complainant_contact character varying(300),
        confidential boolean NOT NULL,
        subject character varying(300) NOT NULL,
        description character varying(4000) NOT NULL,
        status character varying(20) NOT NULL,
        logged_by uuid NOT NULL,
        logged_at_utc timestamp with time zone NOT NULL,
        acknowledged_at_utc timestamp with time zone,
        validation_verdict character varying(2000),
        investigation_outcome character varying(4000),
        resolution character varying(4000),
        linked_nc_id uuid,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_complaint PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724233505_Complaints') THEN
    CREATE UNIQUE INDEX ix_complaint_tenant_id_complaint_ref ON qams.complaint (tenant_id, complaint_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724233505_Complaints') THEN
    CREATE INDEX ix_complaint_tenant_id_status ON qams.complaint (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724233505_Complaints') THEN

                    ALTER TABLE qams.complaint ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.complaint
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724233505_Complaints') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260724233505_Complaints', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724235242_ReportingKpiSnapshots') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'read') THEN
            CREATE SCHEMA read;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724235242_ReportingKpiSnapshots') THEN
    CREATE TABLE read.kpi_snapshot (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        date date NOT NULL,
        open_ncs integer NOT NULL,
        overdue_capa_actions integer NOT NULL,
        open_complaints integer NOT NULL,
        audits_in_progress integer NOT NULL,
        equipment_out_of_service integer NOT NULL,
        high_residual_risks integer NOT NULL,
        overdue_tasks integer NOT NULL,
        pt_unsatisfactory integer NOT NULL,
        CONSTRAINT pk_kpi_snapshot PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724235242_ReportingKpiSnapshots') THEN
    CREATE UNIQUE INDEX ix_kpi_snapshot_tenant_id_date ON read.kpi_snapshot (tenant_id, date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724235242_ReportingKpiSnapshots') THEN

                    ALTER TABLE read.kpi_snapshot ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON read.kpi_snapshot
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724235242_ReportingKpiSnapshots') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260724235242_ReportingKpiSnapshots', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    ALTER TABLE qams.supplier ADD branch_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    ALTER TABLE qams.supplier ADD department_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    ALTER TABLE qams.risk_item ADD branch_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    ALTER TABLE qams.risk_item ADD department_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    ALTER TABLE qams.nonconformance ADD branch_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    ALTER TABLE qams.nonconformance ADD department_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    ALTER TABLE qams.management_review ADD branch_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    ALTER TABLE qams.management_review ADD department_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    ALTER TABLE qams.equipment_item ADD branch_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    ALTER TABLE qams.equipment_item ADD department_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    ALTER TABLE qams.complaint ADD branch_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    ALTER TABLE qams.complaint ADD department_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    ALTER TABLE qams.change_request ADD branch_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    ALTER TABLE qams.change_request ADD department_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    ALTER TABLE qams.audit ADD branch_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    ALTER TABLE qams.audit ADD department_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725001513_RecordAllocation') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725001513_RecordAllocation', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725005044_FieldChangeLedger') THEN
    CREATE TABLE audit.field_change (
        id uuid NOT NULL,
        tenant_id uuid,
        entity_type character varying(150) NOT NULL,
        entity_id character varying(200) NOT NULL,
        action character varying(20) NOT NULL,
        property character varying(150),
        old_value character varying(4000),
        new_value character varying(4000),
        actor_id uuid,
        actor character varying(300) NOT NULL,
        occurred_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT pk_field_change PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725005044_FieldChangeLedger') THEN
    CREATE INDEX ix_field_change_occurred_at_utc ON audit.field_change (occurred_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725005044_FieldChangeLedger') THEN
    CREATE INDEX ix_field_change_tenant_id_entity_id ON audit.field_change (tenant_id, entity_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725005044_FieldChangeLedger') THEN

                    ALTER TABLE audit.field_change ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON audit.field_change
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    CREATE TRIGGER field_change_append_only BEFORE UPDATE OR DELETE ON audit.field_change
                        FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725005044_FieldChangeLedger') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725005044_FieldChangeLedger', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725010119_PasswordPolicy') THEN
    ALTER TABLE qams.user_account ADD password_changed_at_utc timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725010119_PasswordPolicy') THEN
    CREATE TABLE saas.password_history (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        password_hash character varying(500) NOT NULL,
        set_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT pk_password_history PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725010119_PasswordPolicy') THEN
    CREATE INDEX ix_password_history_user_id_set_at_utc ON saas.password_history (user_id, set_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725010119_PasswordPolicy') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725010119_PasswordPolicy', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725054703_DocumentReviewCycles') THEN
    ALTER TABLE qams.controlled_document ADD next_review_due date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725054703_DocumentReviewCycles') THEN
    ALTER TABLE qams.controlled_document ADD review_cycle_months integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725054703_DocumentReviewCycles') THEN
    ALTER TABLE qams.controlled_document ADD review_due_raised boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725054703_DocumentReviewCycles') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725054703_DocumentReviewCycles', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725061912_UncertaintyBudgets') THEN
    CREATE TABLE qams.uncertainty_budget (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        budget_ref character varying(30) NOT NULL,
        analyte character varying(200) NOT NULL,
        method character varying(300) NOT NULL,
        unit character varying(50) NOT NULL,
        level character varying(100) NOT NULL,
        coverage_factor numeric NOT NULL,
        target_expanded_uncertainty numeric,
        status character varying(20) NOT NULL,
        combined_standard_uncertainty numeric,
        expanded_uncertainty numeric,
        meets_target boolean,
        approved_by uuid,
        approved_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_uncertainty_budget PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725061912_UncertaintyBudgets') THEN
    CREATE TABLE qams.uncertainty_component (
        id uuid NOT NULL,
        name character varying(300) NOT NULL,
        type character varying(10) NOT NULL,
        relative_standard_uncertainty numeric NOT NULL,
        source character varying(500),
        budget_id uuid NOT NULL,
        CONSTRAINT pk_uncertainty_component PRIMARY KEY (id),
        CONSTRAINT fk_uncertainty_component_uncertainty_budget_budget_id FOREIGN KEY (budget_id) REFERENCES qams.uncertainty_budget (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725061912_UncertaintyBudgets') THEN
    CREATE UNIQUE INDEX ix_uncertainty_budget_tenant_id_budget_ref ON qams.uncertainty_budget (tenant_id, budget_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725061912_UncertaintyBudgets') THEN
    CREATE INDEX ix_uncertainty_budget_tenant_id_status ON qams.uncertainty_budget (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725061912_UncertaintyBudgets') THEN

                    ALTER TABLE qams.uncertainty_budget ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.uncertainty_budget
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725061912_UncertaintyBudgets') THEN
    CREATE INDEX ix_uncertainty_component_budget_id ON qams.uncertainty_component (budget_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725061912_UncertaintyBudgets') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725061912_UncertaintyBudgets', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725065112_MetrologicalTraceability') THEN
    CREATE TABLE qams.intermediate_check (
        id uuid NOT NULL,
        performed_on date NOT NULL,
        performed_by_id uuid NOT NULL,
        check_type character varying(200) NOT NULL,
        passed boolean NOT NULL,
        reference_standard_id uuid,
        remarks character varying(2000),
        equipment_id uuid NOT NULL,
        CONSTRAINT pk_intermediate_check PRIMARY KEY (id),
        CONSTRAINT fk_intermediate_check_equipment_item_equipment_id FOREIGN KEY (equipment_id) REFERENCES qams.equipment_item (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725065112_MetrologicalTraceability') THEN
    CREATE TABLE qams.reference_standard (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        branch_id uuid,
        department_id uuid,
        standard_ref character varying(30) NOT NULL,
        name character varying(300) NOT NULL,
        type character varying(40) NOT NULL,
        manufacturer character varying(200),
        lot_number character varying(100),
        certificate_number character varying(100),
        traceable_to character varying(500) NOT NULL,
        certified_value character varying(200),
        uncertainty_statement character varying(200),
        received_on date NOT NULL,
        expires_on date,
        status character varying(20) NOT NULL,
        quarantine_reason character varying(1000),
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_reference_standard PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725065112_MetrologicalTraceability') THEN
    CREATE INDEX ix_intermediate_check_equipment_id ON qams.intermediate_check (equipment_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725065112_MetrologicalTraceability') THEN
    CREATE UNIQUE INDEX ix_reference_standard_tenant_id_standard_ref ON qams.reference_standard (tenant_id, standard_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725065112_MetrologicalTraceability') THEN
    CREATE INDEX ix_reference_standard_tenant_id_status ON qams.reference_standard (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725065112_MetrologicalTraceability') THEN

                    ALTER TABLE qams.reference_standard ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.reference_standard
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725065112_MetrologicalTraceability') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725065112_MetrologicalTraceability', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725070822_PersonnelAuthorizationMatrix') THEN
    CREATE TABLE qams.test_authorization (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        user_id uuid NOT NULL,
        test_catalog_item_id uuid NOT NULL,
        competency_record_id uuid NOT NULL,
        scope character varying(20) NOT NULL,
        granted_by uuid NOT NULL,
        granted_on date NOT NULL,
        expires_on date NOT NULL,
        status character varying(20) NOT NULL,
        suspension_reason character varying(1000),
        revocation_reason character varying(1000),
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_test_authorization PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725070822_PersonnelAuthorizationMatrix') THEN
    CREATE INDEX ix_test_authorization_competency_record_id ON qams.test_authorization (competency_record_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725070822_PersonnelAuthorizationMatrix') THEN
    CREATE INDEX ix_test_authorization_tenant_id_status ON qams.test_authorization (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725070822_PersonnelAuthorizationMatrix') THEN
    CREATE INDEX ix_test_authorization_tenant_id_test_catalog_item_id ON qams.test_authorization (tenant_id, test_catalog_item_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725070822_PersonnelAuthorizationMatrix') THEN
    CREATE INDEX ix_test_authorization_tenant_id_user_id ON qams.test_authorization (tenant_id, user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725070822_PersonnelAuthorizationMatrix') THEN

                    ALTER TABLE qams.test_authorization ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.test_authorization
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725070822_PersonnelAuthorizationMatrix') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725070822_PersonnelAuthorizationMatrix', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725073957_EnvironmentalMonitoring') THEN
    CREATE TABLE qams.monitoring_point (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        branch_id uuid,
        department_id uuid,
        point_ref character varying(30) NOT NULL,
        name character varying(200) NOT NULL,
        location character varying(200),
        parameter character varying(100) NOT NULL,
        unit character varying(30) NOT NULL,
        low_limit numeric,
        high_limit numeric,
        status character varying(20) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_monitoring_point PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725073957_EnvironmentalMonitoring') THEN
    CREATE TABLE qams.environmental_reading (
        id uuid NOT NULL,
        value numeric NOT NULL,
        recorded_at_utc timestamp with time zone NOT NULL,
        recorded_by_id uuid NOT NULL,
        in_limit boolean NOT NULL,
        remark character varying(1000),
        point_id uuid NOT NULL,
        CONSTRAINT pk_environmental_reading PRIMARY KEY (id),
        CONSTRAINT fk_environmental_reading_monitoring_point_point_id FOREIGN KEY (point_id) REFERENCES qams.monitoring_point (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725073957_EnvironmentalMonitoring') THEN
    CREATE INDEX ix_environmental_reading_point_id_recorded_at_utc ON qams.environmental_reading (point_id, recorded_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725073957_EnvironmentalMonitoring') THEN
    CREATE UNIQUE INDEX ix_monitoring_point_tenant_id_point_ref ON qams.monitoring_point (tenant_id, point_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725073957_EnvironmentalMonitoring') THEN
    CREATE INDEX ix_monitoring_point_tenant_id_status ON qams.monitoring_point (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725073957_EnvironmentalMonitoring') THEN

                    ALTER TABLE qams.monitoring_point ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.monitoring_point
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725073957_EnvironmentalMonitoring') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725073957_EnvironmentalMonitoring', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725075423_PtPlanAndAuditTrailReview') THEN
    CREATE TABLE qams.audit_trail_review (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        review_ref character varying(30) NOT NULL,
        period_start date NOT NULL,
        period_end date NOT NULL,
        status character varying(20) NOT NULL,
        reviewed_by uuid,
        completed_at_utc timestamp with time zone,
        events_reviewed integer,
        field_changes_reviewed integer,
        anomalies_found boolean,
        conclusion character varying(4000),
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_audit_trail_review PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725075423_PtPlanAndAuditTrailReview') THEN
    CREATE TABLE qams.pt_plan (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        plan_ref character varying(30) NOT NULL,
        year integer NOT NULL,
        status character varying(20) NOT NULL,
        approved_by uuid,
        approved_at_utc timestamp with time zone,
        closure_summary character varying(4000),
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_pt_plan PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725075423_PtPlanAndAuditTrailReview') THEN
    CREATE TABLE qams.pt_plan_item (
        id uuid NOT NULL,
        scheme character varying(200) NOT NULL,
        analyte character varying(200) NOT NULL,
        provider character varying(200),
        planned_cycles integer NOT NULL,
        fulfilled_cycles integer NOT NULL,
        last_enrollment_ref character varying(30),
        notes character varying(1000),
        plan_id uuid NOT NULL,
        CONSTRAINT pk_pt_plan_item PRIMARY KEY (id),
        CONSTRAINT fk_pt_plan_item_pt_plan_plan_id FOREIGN KEY (plan_id) REFERENCES qams.pt_plan (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725075423_PtPlanAndAuditTrailReview') THEN
    CREATE UNIQUE INDEX ix_audit_trail_review_tenant_id_review_ref ON qams.audit_trail_review (tenant_id, review_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725075423_PtPlanAndAuditTrailReview') THEN
    CREATE INDEX ix_audit_trail_review_tenant_id_status ON qams.audit_trail_review (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725075423_PtPlanAndAuditTrailReview') THEN
    CREATE UNIQUE INDEX ix_pt_plan_tenant_id_plan_ref ON qams.pt_plan (tenant_id, plan_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725075423_PtPlanAndAuditTrailReview') THEN
    CREATE UNIQUE INDEX ix_pt_plan_tenant_id_year ON qams.pt_plan (tenant_id, year);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725075423_PtPlanAndAuditTrailReview') THEN
    CREATE INDEX ix_pt_plan_item_plan_id ON qams.pt_plan_item (plan_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725075423_PtPlanAndAuditTrailReview') THEN

                    ALTER TABLE qams.pt_plan ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.pt_plan
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    ALTER TABLE qams.audit_trail_review ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.audit_trail_review
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725075423_PtPlanAndAuditTrailReview') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725075423_PtPlanAndAuditTrailReview', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725080545_ObjectivesAndFeedback') THEN
    CREATE TABLE qams.feedback_entry (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        branch_id uuid,
        department_id uuid,
        feedback_ref character varying(30) NOT NULL,
        source character varying(100) NOT NULL,
        channel character varying(100) NOT NULL,
        type character varying(20) NOT NULL,
        subject character varying(300) NOT NULL,
        details character varying(4000) NOT NULL,
        satisfaction_score integer,
        received_on date NOT NULL,
        logged_by uuid NOT NULL,
        status character varying(20) NOT NULL,
        review_notes character varying(2000),
        action_summary character varying(2000),
        complaint_id uuid,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_feedback_entry PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725080545_ObjectivesAndFeedback') THEN
    CREATE TABLE qams.quality_objective (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        branch_id uuid,
        department_id uuid,
        objective_ref character varying(30) NOT NULL,
        title character varying(300) NOT NULL,
        description character varying(2000),
        metric character varying(300) NOT NULL,
        unit character varying(30) NOT NULL,
        target_value numeric NOT NULL,
        direction character varying(10) NOT NULL,
        owner_id uuid NOT NULL,
        period_start date NOT NULL,
        period_end date NOT NULL,
        status character varying(20) NOT NULL,
        closure_note character varying(2000),
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_quality_objective PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725080545_ObjectivesAndFeedback') THEN
    CREATE TABLE qams.objective_progress (
        id uuid NOT NULL,
        measured_on date NOT NULL,
        value numeric NOT NULL,
        recorded_by_id uuid NOT NULL,
        comment character varying(1000),
        objective_id uuid NOT NULL,
        CONSTRAINT pk_objective_progress PRIMARY KEY (id),
        CONSTRAINT fk_objective_progress_quality_objective_objective_id FOREIGN KEY (objective_id) REFERENCES qams.quality_objective (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725080545_ObjectivesAndFeedback') THEN
    CREATE UNIQUE INDEX ix_feedback_entry_tenant_id_feedback_ref ON qams.feedback_entry (tenant_id, feedback_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725080545_ObjectivesAndFeedback') THEN
    CREATE INDEX ix_feedback_entry_tenant_id_status ON qams.feedback_entry (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725080545_ObjectivesAndFeedback') THEN
    CREATE INDEX ix_objective_progress_objective_id ON qams.objective_progress (objective_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725080545_ObjectivesAndFeedback') THEN
    CREATE UNIQUE INDEX ix_quality_objective_tenant_id_objective_ref ON qams.quality_objective (tenant_id, objective_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725080545_ObjectivesAndFeedback') THEN
    CREATE INDEX ix_quality_objective_tenant_id_status ON qams.quality_objective (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725080545_ObjectivesAndFeedback') THEN

                    ALTER TABLE qams.quality_objective ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.quality_objective
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    ALTER TABLE qams.feedback_entry ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.feedback_entry
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725080545_ObjectivesAndFeedback') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725080545_ObjectivesAndFeedback', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725081714_ImpartialityAndOrgContext') THEN
    CREATE TABLE qams.conflict_declaration (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        conflict_ref character varying(30) NOT NULL,
        declarant_id uuid NOT NULL,
        description character varying(2000) NOT NULL,
        related_party character varying(300) NOT NULL,
        declared_on date NOT NULL,
        status character varying(20) NOT NULL,
        risk_level character varying(10),
        mitigation character varying(2000),
        assessed_by uuid,
        outcome character varying(20),
        closure_note character varying(2000),
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_conflict_declaration PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725081714_ImpartialityAndOrgContext') THEN
    CREATE TABLE qams.context_issue (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        issue_ref character varying(30) NOT NULL,
        type character varying(10) NOT NULL,
        category character varying(100) NOT NULL,
        description character varying(4000) NOT NULL,
        impact character varying(4000) NOT NULL,
        linked_risk_id uuid,
        status character varying(20) NOT NULL,
        resolution character varying(4000),
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_context_issue PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725081714_ImpartialityAndOrgContext') THEN
    CREATE TABLE qams.interested_party (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        party_ref character varying(30) NOT NULL,
        name character varying(200) NOT NULL,
        category character varying(100) NOT NULL,
        needs_and_expectations character varying(4000) NOT NULL,
        relevant_requirements character varying(4000),
        reviewed_on date NOT NULL,
        status character varying(20) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_interested_party PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725081714_ImpartialityAndOrgContext') THEN
    CREATE UNIQUE INDEX ix_conflict_declaration_tenant_id_conflict_ref ON qams.conflict_declaration (tenant_id, conflict_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725081714_ImpartialityAndOrgContext') THEN
    CREATE INDEX ix_conflict_declaration_tenant_id_status ON qams.conflict_declaration (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725081714_ImpartialityAndOrgContext') THEN
    CREATE UNIQUE INDEX ix_context_issue_tenant_id_issue_ref ON qams.context_issue (tenant_id, issue_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725081714_ImpartialityAndOrgContext') THEN
    CREATE UNIQUE INDEX ix_interested_party_tenant_id_party_ref ON qams.interested_party (tenant_id, party_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725081714_ImpartialityAndOrgContext') THEN

                    ALTER TABLE qams.conflict_declaration ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.conflict_declaration
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    ALTER TABLE qams.interested_party ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.interested_party
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                    ALTER TABLE qams.context_issue ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.context_issue
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725081714_ImpartialityAndOrgContext') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725081714_ImpartialityAndOrgContext', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725114820_MethodComparison') THEN
    CREATE TABLE qams.method_comparison_study (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        study_ref character varying(30) NOT NULL,
        analyte character varying(200) NOT NULL,
        unit character varying(50) NOT NULL,
        reference_method character varying(200) NOT NULL,
        test_method character varying(200) NOT NULL,
        state character varying(20) NOT NULL,
        pair_count integer,
        pearson_r numeric,
        deming_slope numeric,
        deming_intercept numeric,
        passing_bablok_slope numeric,
        passing_bablok_intercept numeric,
        mean_bias numeric,
        bias_sd numeric,
        limit_of_agreement_lower numeric,
        limit_of_agreement_upper numeric,
        signed_off_by uuid,
        signed_off_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_method_comparison_study PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725114820_MethodComparison') THEN
    CREATE TABLE qams.measurement_pair (
        id uuid NOT NULL,
        reference_value numeric NOT NULL,
        test_value numeric NOT NULL,
        sample_id character varying(100),
        study_id uuid NOT NULL,
        CONSTRAINT pk_measurement_pair PRIMARY KEY (id),
        CONSTRAINT fk_measurement_pair_method_comparison_study_study_id FOREIGN KEY (study_id) REFERENCES qams.method_comparison_study (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725114820_MethodComparison') THEN
    CREATE INDEX ix_measurement_pair_study_id ON qams.measurement_pair (study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725114820_MethodComparison') THEN
    CREATE INDEX ix_method_comparison_study_tenant_id_state ON qams.method_comparison_study (tenant_id, state);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725114820_MethodComparison') THEN
    CREATE UNIQUE INDEX ix_method_comparison_study_tenant_id_study_ref ON qams.method_comparison_study (tenant_id, study_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725114820_MethodComparison') THEN

                    ALTER TABLE qams.method_comparison_study ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.method_comparison_study
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725114820_MethodComparison') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725114820_MethodComparison', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725120132_LinearityStudies') THEN
    CREATE TABLE qams.linearity_study (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        study_ref character varying(30) NOT NULL,
        analyte character varying(200) NOT NULL,
        unit character varying(50) NOT NULL,
        method character varying(300) NOT NULL,
        allowable_deviation_pct numeric NOT NULL,
        state character varying(20) NOT NULL,
        slope numeric,
        intercept numeric,
        correlation_r numeric,
        is_linear boolean,
        amr_low numeric,
        amr_high numeric,
        signed_off_by uuid,
        signed_off_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_linearity_study PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725120132_LinearityStudies') THEN
    CREATE TABLE qams.linearity_measurement (
        id uuid NOT NULL,
        assigned_value numeric NOT NULL,
        measured_value numeric NOT NULL,
        study_id uuid NOT NULL,
        CONSTRAINT pk_linearity_measurement PRIMARY KEY (id),
        CONSTRAINT fk_linearity_measurement_linearity_study_study_id FOREIGN KEY (study_id) REFERENCES qams.linearity_study (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725120132_LinearityStudies') THEN
    CREATE INDEX ix_linearity_measurement_study_id ON qams.linearity_measurement (study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725120132_LinearityStudies') THEN
    CREATE INDEX ix_linearity_study_tenant_id_state ON qams.linearity_study (tenant_id, state);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725120132_LinearityStudies') THEN
    CREATE UNIQUE INDEX ix_linearity_study_tenant_id_study_ref ON qams.linearity_study (tenant_id, study_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725120132_LinearityStudies') THEN

                    ALTER TABLE qams.linearity_study ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.linearity_study
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725120132_LinearityStudies') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725120132_LinearityStudies', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725122046_DetectionLimitStudies') THEN
    CREATE TABLE qams.detection_limit_study (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        study_ref character varying(30) NOT NULL,
        analyte character varying(200) NOT NULL,
        unit character varying(50) NOT NULL,
        method character varying(300) NOT NULL,
        loq_cv_target_pct numeric NOT NULL,
        state character varying(20) NOT NULL,
        blank_mean numeric,
        blank_sd numeric,
        pooled_low_sd numeric,
        lob numeric,
        lod numeric,
        loq numeric,
        signed_off_by uuid,
        signed_off_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_detection_limit_study PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725122046_DetectionLimitStudies') THEN
    CREATE TABLE qams.detection_measurement (
        id uuid NOT NULL,
        kind character varying(10) NOT NULL,
        assigned_value numeric,
        measured_value numeric NOT NULL,
        study_id uuid NOT NULL,
        CONSTRAINT pk_detection_measurement PRIMARY KEY (id),
        CONSTRAINT fk_detection_measurement_detection_limit_study_study_id FOREIGN KEY (study_id) REFERENCES qams.detection_limit_study (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725122046_DetectionLimitStudies') THEN
    CREATE INDEX ix_detection_limit_study_tenant_id_state ON qams.detection_limit_study (tenant_id, state);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725122046_DetectionLimitStudies') THEN
    CREATE UNIQUE INDEX ix_detection_limit_study_tenant_id_study_ref ON qams.detection_limit_study (tenant_id, study_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725122046_DetectionLimitStudies') THEN
    CREATE INDEX ix_detection_measurement_study_id ON qams.detection_measurement (study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725122046_DetectionLimitStudies') THEN

                    ALTER TABLE qams.detection_limit_study ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.detection_limit_study
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725122046_DetectionLimitStudies') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725122046_DetectionLimitStudies', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725175812_ReferenceIntervalStudies') THEN
    CREATE TABLE qams.reference_interval_study (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        study_ref character varying(30) NOT NULL,
        analyte character varying(200) NOT NULL,
        unit character varying(50) NOT NULL,
        population character varying(150) NOT NULL,
        source character varying(300) NOT NULL,
        claimed_lower numeric NOT NULL,
        claimed_upper numeric NOT NULL,
        state character varying(20) NOT NULL,
        sample_count integer,
        outside_count integer,
        allowed_outside integer,
        verdict character varying(20),
        signed_off_by uuid,
        signed_off_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_reference_interval_study PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725175812_ReferenceIntervalStudies') THEN
    CREATE TABLE qams.reference_sample (
        id uuid NOT NULL,
        value numeric NOT NULL,
        subject_ref character varying(100),
        study_id uuid NOT NULL,
        CONSTRAINT pk_reference_sample PRIMARY KEY (id),
        CONSTRAINT fk_reference_sample_reference_interval_study_study_id FOREIGN KEY (study_id) REFERENCES qams.reference_interval_study (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725175812_ReferenceIntervalStudies') THEN
    CREATE INDEX ix_reference_interval_study_tenant_id_state ON qams.reference_interval_study (tenant_id, state);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725175812_ReferenceIntervalStudies') THEN
    CREATE UNIQUE INDEX ix_reference_interval_study_tenant_id_study_ref ON qams.reference_interval_study (tenant_id, study_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725175812_ReferenceIntervalStudies') THEN
    CREATE INDEX ix_reference_sample_study_id ON qams.reference_sample (study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725175812_ReferenceIntervalStudies') THEN

                    ALTER TABLE qams.reference_interval_study ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.reference_interval_study
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725175812_ReferenceIntervalStudies') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725175812_ReferenceIntervalStudies', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725182042_SigmaAssessments') THEN
    CREATE TABLE qams.sigma_assessment (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        assessment_ref character varying(30) NOT NULL,
        analyte character varying(200) NOT NULL,
        unit character varying(50) NOT NULL,
        allowable_total_error_pct numeric NOT NULL,
        bias_pct numeric NOT NULL,
        cv_pct numeric NOT NULL,
        state character varying(20) NOT NULL,
        sigma_value numeric NOT NULL,
        grade character varying(20) NOT NULL,
        signed_off_by uuid,
        signed_off_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_sigma_assessment PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725182042_SigmaAssessments') THEN
    CREATE UNIQUE INDEX ix_sigma_assessment_tenant_id_assessment_ref ON qams.sigma_assessment (tenant_id, assessment_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725182042_SigmaAssessments') THEN
    CREATE INDEX ix_sigma_assessment_tenant_id_state ON qams.sigma_assessment (tenant_id, state);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725182042_SigmaAssessments') THEN

                    ALTER TABLE qams.sigma_assessment ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.sigma_assessment
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725182042_SigmaAssessments') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725182042_SigmaAssessments', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725183152_PrecisionStudies') THEN
    CREATE TABLE qams.precision_study (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        study_ref character varying(30) NOT NULL,
        analyte character varying(200) NOT NULL,
        unit character varying(50) NOT NULL,
        level character varying(100) NOT NULL,
        claimed_repeatability_cv_pct numeric,
        claimed_within_lab_cv_pct numeric,
        state character varying(20) NOT NULL,
        grand_mean numeric,
        repeatability_sd numeric,
        repeatability_cv_pct numeric,
        between_run_sd numeric,
        between_run_cv_pct numeric,
        within_lab_sd numeric,
        within_lab_cv_pct numeric,
        meets_repeatability_claim boolean,
        meets_within_lab_claim boolean,
        signed_off_by uuid,
        signed_off_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_precision_study PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725183152_PrecisionStudies') THEN
    CREATE TABLE qams.precision_measurement (
        id uuid NOT NULL,
        run_label character varying(60) NOT NULL,
        value numeric NOT NULL,
        study_id uuid NOT NULL,
        CONSTRAINT pk_precision_measurement PRIMARY KEY (id),
        CONSTRAINT fk_precision_measurement_precision_study_study_id FOREIGN KEY (study_id) REFERENCES qams.precision_study (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725183152_PrecisionStudies') THEN
    CREATE INDEX ix_precision_measurement_study_id ON qams.precision_measurement (study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725183152_PrecisionStudies') THEN
    CREATE INDEX ix_precision_study_tenant_id_state ON qams.precision_study (tenant_id, state);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725183152_PrecisionStudies') THEN
    CREATE UNIQUE INDEX ix_precision_study_tenant_id_study_ref ON qams.precision_study (tenant_id, study_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725183152_PrecisionStudies') THEN

                    ALTER TABLE qams.precision_study ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.precision_study
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725183152_PrecisionStudies') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725183152_PrecisionStudies', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE TABLE qams.carryover_study (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        study_ref character varying(30) NOT NULL,
        analyte character varying(200) NOT NULL,
        unit character varying(50) NOT NULL,
        allowable_carryover_pct numeric NOT NULL,
        state character varying(20) NOT NULL,
        mean_high numeric,
        first_low numeric,
        steady_low numeric,
        carryover_pct numeric,
        passes boolean,
        signed_off_by uuid,
        signed_off_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_carryover_study PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE TABLE qams.instrument_comparability_study (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        study_ref character varying(30) NOT NULL,
        analyte character varying(200) NOT NULL,
        unit character varying(50) NOT NULL,
        reference_instrument character varying(100) NOT NULL,
        allowable_bias_pct numeric NOT NULL,
        state character varying(20) NOT NULL,
        instrument_count integer,
        non_comparable_count integer,
        signed_off_by uuid,
        signed_off_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_instrument_comparability_study PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE TABLE qams.interference_study (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        study_ref character varying(30) NOT NULL,
        analyte character varying(200) NOT NULL,
        unit character varying(50) NOT NULL,
        allowable_bias_pct numeric NOT NULL,
        state character varying(20) NOT NULL,
        control_mean numeric,
        interferent_count integer,
        significant_count integer,
        signed_off_by uuid,
        signed_off_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_interference_study PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE TABLE qams.lot_comparison_study (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        study_ref character varying(30) NOT NULL,
        analyte character varying(200) NOT NULL,
        unit character varying(50) NOT NULL,
        current_lot character varying(60) NOT NULL,
        new_lot character varying(60) NOT NULL,
        allowable_bias_pct numeric NOT NULL,
        state character varying(20) NOT NULL,
        pair_count integer,
        mean_current numeric,
        mean_new numeric,
        mean_bias_pct numeric,
        passes boolean,
        signed_off_by uuid,
        signed_off_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_lot_comparison_study PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE TABLE qams.outlier_screening (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        screening_ref character varying(30) NOT NULL,
        dataset character varying(200) NOT NULL,
        unit character varying(50) NOT NULL,
        state character varying(20) NOT NULL,
        point_count integer,
        mean numeric,
        sd numeric,
        median numeric,
        q1 numeric,
        q3 numeric,
        tukey_lower numeric,
        tukey_upper numeric,
        outlier_count integer,
        signed_off_by uuid,
        signed_off_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_outlier_screening PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE TABLE qams.carryover_reading (
        id uuid NOT NULL,
        kind character varying(10) NOT NULL,
        sequence integer NOT NULL,
        value numeric NOT NULL,
        study_id uuid NOT NULL,
        CONSTRAINT pk_carryover_reading PRIMARY KEY (id),
        CONSTRAINT fk_carryover_reading_carryover_study_study_id FOREIGN KEY (study_id) REFERENCES qams.carryover_study (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE TABLE qams.instrument_reading (
        id uuid NOT NULL,
        instrument character varying(100) NOT NULL,
        sample_id character varying(100) NOT NULL,
        value numeric NOT NULL,
        study_id uuid NOT NULL,
        CONSTRAINT pk_instrument_reading PRIMARY KEY (id),
        CONSTRAINT fk_instrument_reading_instrument_comparability_study_study_id FOREIGN KEY (study_id) REFERENCES qams.instrument_comparability_study (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE TABLE qams.interference_measurement (
        id uuid NOT NULL,
        is_control boolean NOT NULL,
        interferent character varying(120),
        value numeric NOT NULL,
        study_id uuid NOT NULL,
        CONSTRAINT pk_interference_measurement PRIMARY KEY (id),
        CONSTRAINT fk_interference_measurement_interference_study_study_id FOREIGN KEY (study_id) REFERENCES qams.interference_study (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE TABLE qams.lot_sample_pair (
        id uuid NOT NULL,
        current_lot_value numeric NOT NULL,
        new_lot_value numeric NOT NULL,
        sample_id character varying(100),
        study_id uuid NOT NULL,
        CONSTRAINT pk_lot_sample_pair PRIMARY KEY (id),
        CONSTRAINT fk_lot_sample_pair_lot_comparison_study_study_id FOREIGN KEY (study_id) REFERENCES qams.lot_comparison_study (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE TABLE qams.outlier_point (
        id uuid NOT NULL,
        value numeric NOT NULL,
        label character varying(100),
        screening_id uuid NOT NULL,
        CONSTRAINT pk_outlier_point PRIMARY KEY (id),
        CONSTRAINT fk_outlier_point_outlier_screening_screening_id FOREIGN KEY (screening_id) REFERENCES qams.outlier_screening (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE INDEX ix_carryover_reading_study_id ON qams.carryover_reading (study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE INDEX ix_carryover_study_tenant_id_state ON qams.carryover_study (tenant_id, state);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE UNIQUE INDEX ix_carryover_study_tenant_id_study_ref ON qams.carryover_study (tenant_id, study_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE INDEX ix_instrument_comparability_study_tenant_id_state ON qams.instrument_comparability_study (tenant_id, state);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE UNIQUE INDEX ix_instrument_comparability_study_tenant_id_study_ref ON qams.instrument_comparability_study (tenant_id, study_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE INDEX ix_instrument_reading_study_id ON qams.instrument_reading (study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE INDEX ix_interference_measurement_study_id ON qams.interference_measurement (study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE INDEX ix_interference_study_tenant_id_state ON qams.interference_study (tenant_id, state);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE UNIQUE INDEX ix_interference_study_tenant_id_study_ref ON qams.interference_study (tenant_id, study_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE INDEX ix_lot_comparison_study_tenant_id_state ON qams.lot_comparison_study (tenant_id, state);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE UNIQUE INDEX ix_lot_comparison_study_tenant_id_study_ref ON qams.lot_comparison_study (tenant_id, study_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE INDEX ix_lot_sample_pair_study_id ON qams.lot_sample_pair (study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE INDEX ix_outlier_point_screening_id ON qams.outlier_point (screening_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE UNIQUE INDEX ix_outlier_screening_tenant_id_screening_ref ON qams.outlier_screening (tenant_id, screening_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    CREATE INDEX ix_outlier_screening_tenant_id_state ON qams.outlier_screening (tenant_id, state);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN

                    ALTER TABLE qams.outlier_screening ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.outlier_screening
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

                    ALTER TABLE qams.carryover_study ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.carryover_study
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

                    ALTER TABLE qams.lot_comparison_study ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.lot_comparison_study
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

                    ALTER TABLE qams.interference_study ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.interference_study
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

                    ALTER TABLE qams.instrument_comparability_study ENABLE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON qams.instrument_comparability_study
                        USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260725201422_AnalyticalComplianceModules') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260725201422_AnalyticalComplianceModules', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726081443_ActivateForcedTenantRls') THEN

                    DO $rls$
                    DECLARE r record;
                    BEGIN
                        FOR r IN
                            SELECT schemaname, tablename FROM pg_policies WHERE policyname = 'tenant_isolation'
                        LOOP
                            EXECUTE format('ALTER TABLE %I.%I ENABLE ROW LEVEL SECURITY', r.schemaname, r.tablename);
                            EXECUTE format('ALTER TABLE %I.%I FORCE ROW LEVEL SECURITY', r.schemaname, r.tablename);
                            EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I.%I', r.schemaname, r.tablename);
                            EXECUTE format($pol$
                                CREATE POLICY tenant_isolation ON %I.%I
                                USING (
                                    tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                                    OR current_setting('app.bypass_rls', true) = 'on'
                                )
                                WITH CHECK (
                                    tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                                    OR current_setting('app.bypass_rls', true) = 'on'
                                )
                            $pol$, r.schemaname, r.tablename);
                        END LOOP;
                    END
                    $rls$;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726081443_ActivateForcedTenantRls') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260726081443_ActivateForcedTenantRls', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726084134_SignedRecordImmutability') THEN

                    CREATE OR REPLACE FUNCTION qams.reject_frozen_mutation() RETURNS trigger AS $fn$
                    DECLARE
                        frozen_col text := TG_ARGV[0];
                        frozen_val text := TG_ARGV[1];
                        old_val text;
                    BEGIN
                        old_val := row_to_json(OLD) ->> frozen_col;
                        IF old_val = frozen_val THEN
                            RAISE EXCEPTION
                                'signed/approved record is immutable and cannot be modified or deleted (%.% is %)',
                                TG_TABLE_SCHEMA, TG_TABLE_NAME, frozen_val
                                USING ERRCODE = 'check_violation';
                        END IF;
                        IF TG_OP = 'DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
                    END;
                    $fn$ LANGUAGE plpgsql;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726084134_SignedRecordImmutability') THEN

                        DROP TRIGGER IF EXISTS frozen_immutability ON qams.validation_study;
                        CREATE TRIGGER frozen_immutability
                            BEFORE UPDATE OR DELETE ON qams.validation_study
                            FOR EACH ROW EXECUTE FUNCTION qams.reject_frozen_mutation('state', 'SignedOff');
                    
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726084134_SignedRecordImmutability') THEN

                        DROP TRIGGER IF EXISTS frozen_immutability ON qams.method_comparison_study;
                        CREATE TRIGGER frozen_immutability
                            BEFORE UPDATE OR DELETE ON qams.method_comparison_study
                            FOR EACH ROW EXECUTE FUNCTION qams.reject_frozen_mutation('state', 'SignedOff');
                    
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726084134_SignedRecordImmutability') THEN

                        DROP TRIGGER IF EXISTS frozen_immutability ON qams.precision_study;
                        CREATE TRIGGER frozen_immutability
                            BEFORE UPDATE OR DELETE ON qams.precision_study
                            FOR EACH ROW EXECUTE FUNCTION qams.reject_frozen_mutation('state', 'SignedOff');
                    
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726084134_SignedRecordImmutability') THEN

                        DROP TRIGGER IF EXISTS frozen_immutability ON qams.linearity_study;
                        CREATE TRIGGER frozen_immutability
                            BEFORE UPDATE OR DELETE ON qams.linearity_study
                            FOR EACH ROW EXECUTE FUNCTION qams.reject_frozen_mutation('state', 'SignedOff');
                    
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726084134_SignedRecordImmutability') THEN

                        DROP TRIGGER IF EXISTS frozen_immutability ON qams.detection_limit_study;
                        CREATE TRIGGER frozen_immutability
                            BEFORE UPDATE OR DELETE ON qams.detection_limit_study
                            FOR EACH ROW EXECUTE FUNCTION qams.reject_frozen_mutation('state', 'SignedOff');
                    
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726084134_SignedRecordImmutability') THEN

                        DROP TRIGGER IF EXISTS frozen_immutability ON qams.reference_interval_study;
                        CREATE TRIGGER frozen_immutability
                            BEFORE UPDATE OR DELETE ON qams.reference_interval_study
                            FOR EACH ROW EXECUTE FUNCTION qams.reject_frozen_mutation('state', 'SignedOff');
                    
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726084134_SignedRecordImmutability') THEN

                        DROP TRIGGER IF EXISTS frozen_immutability ON qams.sigma_assessment;
                        CREATE TRIGGER frozen_immutability
                            BEFORE UPDATE OR DELETE ON qams.sigma_assessment
                            FOR EACH ROW EXECUTE FUNCTION qams.reject_frozen_mutation('state', 'SignedOff');
                    
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726084134_SignedRecordImmutability') THEN

                        DROP TRIGGER IF EXISTS frozen_immutability ON qams.outlier_screening;
                        CREATE TRIGGER frozen_immutability
                            BEFORE UPDATE OR DELETE ON qams.outlier_screening
                            FOR EACH ROW EXECUTE FUNCTION qams.reject_frozen_mutation('state', 'SignedOff');
                    
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726084134_SignedRecordImmutability') THEN

                        DROP TRIGGER IF EXISTS frozen_immutability ON qams.carryover_study;
                        CREATE TRIGGER frozen_immutability
                            BEFORE UPDATE OR DELETE ON qams.carryover_study
                            FOR EACH ROW EXECUTE FUNCTION qams.reject_frozen_mutation('state', 'SignedOff');
                    
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726084134_SignedRecordImmutability') THEN

                        DROP TRIGGER IF EXISTS frozen_immutability ON qams.lot_comparison_study;
                        CREATE TRIGGER frozen_immutability
                            BEFORE UPDATE OR DELETE ON qams.lot_comparison_study
                            FOR EACH ROW EXECUTE FUNCTION qams.reject_frozen_mutation('state', 'SignedOff');
                    
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726084134_SignedRecordImmutability') THEN

                        DROP TRIGGER IF EXISTS frozen_immutability ON qams.interference_study;
                        CREATE TRIGGER frozen_immutability
                            BEFORE UPDATE OR DELETE ON qams.interference_study
                            FOR EACH ROW EXECUTE FUNCTION qams.reject_frozen_mutation('state', 'SignedOff');
                    
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726084134_SignedRecordImmutability') THEN

                        DROP TRIGGER IF EXISTS frozen_immutability ON qams.instrument_comparability_study;
                        CREATE TRIGGER frozen_immutability
                            BEFORE UPDATE OR DELETE ON qams.instrument_comparability_study
                            FOR EACH ROW EXECUTE FUNCTION qams.reject_frozen_mutation('state', 'SignedOff');
                    
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726084134_SignedRecordImmutability') THEN

                        DROP TRIGGER IF EXISTS frozen_immutability ON qams.uncertainty_budget;
                        CREATE TRIGGER frozen_immutability
                            BEFORE UPDATE OR DELETE ON qams.uncertainty_budget
                            FOR EACH ROW EXECUTE FUNCTION qams.reject_frozen_mutation('status', 'Approved');
                    
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726084134_SignedRecordImmutability') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260726084134_SignedRecordImmutability', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726103650_RelaxAuditRlsWriteCheck') THEN

                    DO $rls$
                    DECLARE r record;
                    BEGIN
                        FOR r IN
                            SELECT schemaname, tablename FROM pg_policies
                            WHERE policyname = 'tenant_isolation' AND schemaname = 'audit'
                        LOOP
                            EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I.%I', r.schemaname, r.tablename);
                            EXECUTE format($pol$
                                CREATE POLICY tenant_isolation ON %I.%I
                                USING (
                                    tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                                    OR current_setting('app.bypass_rls', true) = 'on'
                                )
                                WITH CHECK (
                                    tenant_id IS NULL
                                    OR tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                                    OR current_setting('app.bypass_rls', true) = 'on'
                                )
                            $pol$, r.schemaname, r.tablename);
                        END LOOP;
                    END
                    $rls$;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726103650_RelaxAuditRlsWriteCheck') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260726103650_RelaxAuditRlsWriteCheck', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726132544_TenantMfaPolicy') THEN
    ALTER TABLE saas.tenant ADD require_mfa_privileged boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726132544_TenantMfaPolicy') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260726132544_TenantMfaPolicy', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726190957_QcTargetEffectiveDating') THEN
    ALTER TABLE qams.qc_profile ADD last_target_change_reason character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726190957_QcTargetEffectiveDating') THEN
    ALTER TABLE qams.qc_profile ADD target_effective_from_utc timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726190957_QcTargetEffectiveDating') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260726190957_QcTargetEffectiveDating', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.work_task ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.validation_study ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.user_account ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.uncertainty_budget ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.training_assignment ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.test_catalog_item ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.test_authorization ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE saas.tenant ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.supplier_evaluation ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.supplier ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.sla_definition ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.sigma_assessment ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.risk_item ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.reference_standard ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.reference_interval_study ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.quality_objective ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.qc_run ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.qc_profile ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.pt_plan ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.pt_enrollment ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.precision_study ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.outlier_screening ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.notification_rule ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.notification_dispatch ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.nonconformance ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.monitoring_point ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.method_comparison_study ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.management_review ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.lov_entry ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.lot_comparison_study ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.linearity_study ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.interference_study ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.interested_party ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.instrument_comparability_study ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.file_reference ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.feedback_entry ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.escalation_timer ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.equipment_item ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.detection_limit_study ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.department ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.controlled_document ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.context_issue ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.conflict_declaration ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.complaint ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.competency_record ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.change_request ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.carryover_study ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.branch ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.audit_trail_review ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.audit ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    ALTER TABLE qams.archive_entry ADD created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726192118_CreatedByUserIdForSoD') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260726192118_CreatedByUserIdForSoD', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726193528_FieldChangeReason') THEN
    ALTER TABLE audit.field_change ADD reason character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726193528_FieldChangeReason') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260726193528_FieldChangeReason', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726195821_ArchiveLegalHold') THEN
    ALTER TABLE qams.archive_entry ADD is_on_legal_hold boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726195821_ArchiveLegalHold') THEN
    ALTER TABLE qams.archive_entry ADD legal_hold_placed_by uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726195821_ArchiveLegalHold') THEN
    ALTER TABLE qams.archive_entry ADD legal_hold_reason character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726195821_ArchiveLegalHold') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260726195821_ArchiveLegalHold', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726203026_QualityPolicy') THEN
    CREATE TABLE qams.quality_policy (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        policy_ref character varying(30) NOT NULL,
        version integer NOT NULL,
        statement character varying(8000) NOT NULL,
        status character varying(20) NOT NULL,
        effective_date date,
        approved_by_id uuid,
        approved_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_quality_policy PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726203026_QualityPolicy') THEN
    CREATE UNIQUE INDEX ix_quality_policy_tenant_id_policy_ref ON qams.quality_policy (tenant_id, policy_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726203026_QualityPolicy') THEN
    CREATE INDEX ix_quality_policy_tenant_id_status ON qams.quality_policy (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726203026_QualityPolicy') THEN
    CREATE UNIQUE INDEX ix_quality_policy_tenant_id_version ON qams.quality_policy (tenant_id, version);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726203026_QualityPolicy') THEN

                    ALTER TABLE qams.quality_policy ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE qams.quality_policy FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS tenant_isolation ON qams.quality_policy;
                    CREATE POLICY tenant_isolation ON qams.quality_policy
                    USING (
                        tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                        OR current_setting('app.bypass_rls', true) = 'on'
                    )
                    WITH CHECK (
                        tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                        OR current_setting('app.bypass_rls', true) = 'on'
                    );
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726203026_QualityPolicy') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260726203026_QualityPolicy', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726204141_DocumentAcknowledgement') THEN
    CREATE TABLE qams.document_acknowledgement (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        document_id uuid NOT NULL,
        document_code character varying(60) NOT NULL,
        version_label character varying(20) NOT NULL,
        user_id uuid NOT NULL,
        acknowledged_at_utc timestamp with time zone NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_document_acknowledgement PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726204141_DocumentAcknowledgement') THEN
    CREATE UNIQUE INDEX ix_document_acknowledgement_tenant_id_document_id_version_labe ON qams.document_acknowledgement (tenant_id, document_id, version_label, user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726204141_DocumentAcknowledgement') THEN
    CREATE INDEX ix_document_acknowledgement_tenant_id_user_id ON qams.document_acknowledgement (tenant_id, user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726204141_DocumentAcknowledgement') THEN

                    ALTER TABLE qams.document_acknowledgement ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE qams.document_acknowledgement FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS tenant_isolation ON qams.document_acknowledgement;
                    CREATE POLICY tenant_isolation ON qams.document_acknowledgement
                    USING (
                        tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                        OR current_setting('app.bypass_rls', true) = 'on'
                    )
                    WITH CHECK (
                        tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                        OR current_setting('app.bypass_rls', true) = 'on'
                    );
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726204141_DocumentAcknowledgement') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260726204141_DocumentAcknowledgement', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726205501_NcEventType') THEN
    ALTER TABLE qams.nonconformance ADD event_type character varying(30) NOT NULL DEFAULT 'Nonconformity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726205501_NcEventType') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260726205501_NcEventType', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726211332_ChangePostImplementationReview') THEN
    ALTER TABLE qams.change_request ADD change_effective boolean;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726211332_ChangePostImplementationReview') THEN
    ALTER TABLE qams.change_request ADD post_implementation_review_notes text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726211332_ChangePostImplementationReview') THEN
    ALTER TABLE qams.change_request ADD post_implementation_reviewed_at_utc timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726211332_ChangePostImplementationReview') THEN
    ALTER TABLE qams.change_request ADD post_implementation_reviewed_by uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726211332_ChangePostImplementationReview') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260726211332_ChangePostImplementationReview', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726213412_UserAccessReview') THEN
    CREATE TABLE qams.user_access_review (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        review_ref character varying(30) NOT NULL,
        opened_on date NOT NULL,
        status character varying(20) NOT NULL,
        reviewed_by uuid,
        completed_at_utc timestamp with time zone,
        accounts_reviewed integer,
        changes_required boolean,
        conclusion character varying(4000),
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_user_access_review PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726213412_UserAccessReview') THEN
    CREATE UNIQUE INDEX ix_user_access_review_tenant_id_review_ref ON qams.user_access_review (tenant_id, review_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726213412_UserAccessReview') THEN
    CREATE INDEX ix_user_access_review_tenant_id_status ON qams.user_access_review (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726213412_UserAccessReview') THEN

                    ALTER TABLE qams.user_access_review ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE qams.user_access_review FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS tenant_isolation ON qams.user_access_review;
                    CREATE POLICY tenant_isolation ON qams.user_access_review
                    USING (
                        tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                        OR current_setting('app.bypass_rls', true) = 'on'
                    )
                    WITH CHECK (
                        tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                        OR current_setting('app.bypass_rls', true) = 'on'
                    );
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726213412_UserAccessReview') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260726213412_UserAccessReview', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726214512_DocumentControlledCopy') THEN
    CREATE TABLE qams.document_controlled_copy (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        document_id uuid NOT NULL,
        document_code character varying(60) NOT NULL,
        version_label character varying(20) NOT NULL,
        copy_number integer NOT NULL,
        holder character varying(200) NOT NULL,
        issued_by uuid NOT NULL,
        issued_at_utc timestamp with time zone NOT NULL,
        status character varying(20) NOT NULL,
        closed_by uuid,
        closed_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_document_controlled_copy PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726214512_DocumentControlledCopy') THEN
    CREATE UNIQUE INDEX ix_document_controlled_copy_tenant_id_document_id_copy_number ON qams.document_controlled_copy (tenant_id, document_id, copy_number);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726214512_DocumentControlledCopy') THEN
    CREATE INDEX ix_document_controlled_copy_tenant_id_status ON qams.document_controlled_copy (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726214512_DocumentControlledCopy') THEN

                    ALTER TABLE qams.document_controlled_copy ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE qams.document_controlled_copy FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS tenant_isolation ON qams.document_controlled_copy;
                    CREATE POLICY tenant_isolation ON qams.document_controlled_copy
                    USING (
                        tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                        OR current_setting('app.bypass_rls', true) = 'on'
                    )
                    WITH CHECK (
                        tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                        OR current_setting('app.bypass_rls', true) = 'on'
                    );
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260726214512_DocumentControlledCopy') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260726214512_DocumentControlledCopy', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727211942_Phase1OutboxResilienceAndConcurrency') THEN
    DROP INDEX qams.ix_outbox_event_pending;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727211942_Phase1OutboxResilienceAndConcurrency') THEN
    ALTER TABLE qams.outbox_event ADD claimed_until_utc timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727211942_Phase1OutboxResilienceAndConcurrency') THEN
    ALTER TABLE qams.outbox_event ADD dead_lettered_at_utc timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727211942_Phase1OutboxResilienceAndConcurrency') THEN
    ALTER TABLE qams.outbox_event ADD next_attempt_at_utc timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727211942_Phase1OutboxResilienceAndConcurrency') THEN
    CREATE INDEX ix_outbox_event_dead_letter ON qams.outbox_event (dead_lettered_at_utc) WHERE dead_lettered_at_utc IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727211942_Phase1OutboxResilienceAndConcurrency') THEN
    CREATE INDEX ix_outbox_event_pending ON qams.outbox_event (occurred_at_utc) WHERE processed_at_utc IS NULL AND dead_lettered_at_utc IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727211942_Phase1OutboxResilienceAndConcurrency') THEN
    CREATE UNIQUE INDEX ux_nonconformance_source ON qams.nonconformance (tenant_id, source_ref) WHERE source_ref IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727211942_Phase1OutboxResilienceAndConcurrency') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260727211942_Phase1OutboxResilienceAndConcurrency', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727215019_Phase2OutboxTraceParent') THEN
    ALTER TABLE qams.outbox_event ADD trace_parent character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727215019_Phase2OutboxTraceParent') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260727215019_Phase2OutboxTraceParent', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727223029_Phase4IdempotencyRecords') THEN
    CREATE TABLE qams.idempotency_record (
        id uuid NOT NULL,
        actor_id uuid NOT NULL,
        idempotency_key character varying(100) NOT NULL,
        request_type character varying(300) NOT NULL,
        response_json text NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT pk_idempotency_record PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727223029_Phase4IdempotencyRecords') THEN
    CREATE INDEX ix_idempotency_created ON qams.idempotency_record (created_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727223029_Phase4IdempotencyRecords') THEN
    CREATE UNIQUE INDEX ux_idempotency_actor_key ON qams.idempotency_record (actor_id, idempotency_key, request_type);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727223029_Phase4IdempotencyRecords') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260727223029_Phase4IdempotencyRecords', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728073229_Phase5CheckConstraints') THEN
    ALTER TABLE qams.nonconformance
        ADD CONSTRAINT ck_nonconformance_severity_range CHECK (severity BETWEEN 1 AND 5),
        ADD CONSTRAINT ck_nonconformance_likelihood_range CHECK (likelihood BETWEEN 1 AND 5),
        ADD CONSTRAINT ck_nonconformance_rpn_range CHECK (rpn BETWEEN 1 AND 25),
        ADD CONSTRAINT ck_nonconformance_status_domain CHECK (status IN
            ('Draft','Raised','Assigned','Rca','ActionPlan','PendingVerification',
             'EffectivenessCheck','Closed','Rejected'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728073229_Phase5CheckConstraints') THEN
    ALTER TABLE qams.risk_item
        ADD CONSTRAINT ck_risk_item_likelihood_range CHECK (likelihood BETWEEN 1 AND 5),
        ADD CONSTRAINT ck_risk_item_impact_range CHECK (impact BETWEEN 1 AND 5),
        ADD CONSTRAINT ck_risk_item_rpn_range CHECK (rpn BETWEEN 1 AND 25),
        ADD CONSTRAINT ck_risk_item_residual_ranges CHECK (
            (residual_likelihood IS NULL OR residual_likelihood BETWEEN 1 AND 5) AND
            (residual_impact IS NULL OR residual_impact BETWEEN 1 AND 5) AND
            (residual_rpn IS NULL OR residual_rpn BETWEEN 1 AND 25));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728073229_Phase5CheckConstraints') THEN
    ALTER TABLE qams.equipment_item
        ADD CONSTRAINT ck_equipment_interval_positive CHECK (calibration_interval_days > 0),
        ADD CONSTRAINT ck_equipment_grace_nonnegative CHECK (grace_period_days >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728073229_Phase5CheckConstraints') THEN
    ALTER TABLE qams.supplier_evaluation
        ADD CONSTRAINT ck_supplier_evaluation_score_nonnegative CHECK (weighted_total >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728073229_Phase5CheckConstraints') THEN
    ALTER TABLE qams.work_task
        ADD CONSTRAINT ck_work_task_completion_order CHECK
            (completed_at_utc IS NULL OR completed_at_utc >= created_at_utc);
    ALTER TABLE qams.training_assignment
        ADD CONSTRAINT ck_training_completion_order CHECK
            (completed_at_utc IS NULL OR completed_at_utc >= created_at_utc);
    ALTER TABLE qams.audit
        ADD CONSTRAINT ck_audit_signoff_order CHECK
            (signed_off_at_utc IS NULL OR signed_off_at_utc >= created_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728073229_Phase5CheckConstraints') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260728073229_Phase5CheckConstraints', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728130923_Phase7RefreshSessions') THEN
    CREATE TABLE qams.refresh_session (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        family_id uuid NOT NULL,
        token_hash character varying(64) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        expires_at_utc timestamp with time zone NOT NULL,
        revoked_at_utc timestamp with time zone,
        replaced_by_id uuid,
        CONSTRAINT pk_refresh_session PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728130923_Phase7RefreshSessions') THEN
    CREATE INDEX ix_refresh_session_expires ON qams.refresh_session (expires_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728130923_Phase7RefreshSessions') THEN
    CREATE INDEX ix_refresh_session_family ON qams.refresh_session (family_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728130923_Phase7RefreshSessions') THEN
    CREATE INDEX ix_refresh_session_user ON qams.refresh_session (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728130923_Phase7RefreshSessions') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260728130923_Phase7RefreshSessions', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260730112800_RolePrivilegeModule') THEN
    ALTER TABLE qams.user_account ADD preferred_language character varying(10);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260730112800_RolePrivilegeModule') THEN
    ALTER TABLE qams.user_account ADD role_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260730112800_RolePrivilegeModule') THEN
    CREATE TABLE qams.role (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        name character varying(80) NOT NULL,
        normalized_name character varying(80) NOT NULL,
        description character varying(500),
        is_system boolean NOT NULL,
        default_language character varying(10),
        is_active boolean NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_role PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260730112800_RolePrivilegeModule') THEN
    CREATE TABLE qams.user_branch_access (
        branch_id uuid NOT NULL,
        user_id uuid NOT NULL,
        CONSTRAINT pk_user_branch_access PRIMARY KEY (user_id, branch_id),
        CONSTRAINT fk_user_branch_access_user_account_user_id FOREIGN KEY (user_id) REFERENCES qams.user_account (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260730112800_RolePrivilegeModule') THEN
    CREATE TABLE qams.user_department_access (
        department_id uuid NOT NULL,
        user_id uuid NOT NULL,
        CONSTRAINT pk_user_department_access PRIMARY KEY (user_id, department_id),
        CONSTRAINT fk_user_department_access_user_account_user_id FOREIGN KEY (user_id) REFERENCES qams.user_account (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260730112800_RolePrivilegeModule') THEN
    CREATE TABLE qams.role_permission (
        permission_key character varying(60) NOT NULL,
        role_id uuid NOT NULL,
        CONSTRAINT pk_role_permission PRIMARY KEY (role_id, permission_key),
        CONSTRAINT fk_role_permission_role_role_id FOREIGN KEY (role_id) REFERENCES qams.role (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260730112800_RolePrivilegeModule') THEN
    CREATE INDEX ix_user_account_role_id ON qams.user_account (role_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260730112800_RolePrivilegeModule') THEN
    CREATE UNIQUE INDEX ix_role_tenant_id_normalized_name ON qams.role (tenant_id, normalized_name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260730112800_RolePrivilegeModule') THEN

                    ALTER TABLE qams.role ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE qams.role FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS tenant_isolation ON qams.role;
                    CREATE POLICY tenant_isolation ON qams.role
                    USING (
                        tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                        OR current_setting('app.bypass_rls', true) = 'on'
                    )
                    WITH CHECK (
                        tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                        OR current_setting('app.bypass_rls', true) = 'on'
                    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260730112800_RolePrivilegeModule') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260730112800_RolePrivilegeModule', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.supplier_evaluation RENAME COLUMN criteria_json TO criteria;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.supplier_evaluation ALTER COLUMN criteria TYPE jsonb USING criteria::jsonb;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER INDEX qams.ix_notification_dispatch_tenant_id_recipient_user_id_read_by_r RENAME TO ix_notif_dispatch_tenant_recipient_read;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER INDEX qams.ix_document_controlled_copy_tenant_id_document_id_copy_number RENAME TO ux_doc_copy_tenant_document_number;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER INDEX qams.ix_document_acknowledgement_tenant_id_document_id_version_labe RENAME TO ux_doc_ack_tenant_document_version_user;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.user_access_review ALTER COLUMN conclusion TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.test_authorization ALTER COLUMN suspension_reason TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.test_authorization ALTER COLUMN revocation_reason TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE audit.security_event ALTER COLUMN ip_address TYPE inet USING ip_address::inet;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.review_decision ALTER COLUMN description TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.reference_standard ALTER COLUMN quarantine_reason TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.rca_record ALTER COLUMN analysis TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.quality_policy ALTER COLUMN statement TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.quality_objective ALTER COLUMN description TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.quality_objective ALTER COLUMN closure_note TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.qc_run ALTER COLUMN troubleshooting_note TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.pt_plan_item ALTER COLUMN notes TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.pt_plan ALTER COLUMN closure_summary TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.outbox_event ALTER COLUMN last_error TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.objective_progress ALTER COLUMN comment TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.notification_rule ALTER COLUMN body_template TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.notification_dispatch ALTER COLUMN error TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.notification_dispatch ALTER COLUMN body TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.nonconformance ALTER COLUMN rejection_reason TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.nonconformance ALTER COLUMN description TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.mitigation_action ALTER COLUMN description TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.management_review ALTER COLUMN participants TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.management_review ALTER COLUMN minutes TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.maintenance_record ALTER COLUMN work_description TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.intermediate_check ALTER COLUMN remarks TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.interested_party ALTER COLUMN relevant_requirements TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.interested_party ALTER COLUMN needs_and_expectations TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE audit.field_change ALTER COLUMN reason TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE audit.field_change ALTER COLUMN old_value TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE audit.field_change ALTER COLUMN new_value TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.feedback_entry ALTER COLUMN review_notes TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.feedback_entry ALTER COLUMN details TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.feedback_entry ALTER COLUMN action_summary TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.environmental_reading ALTER COLUMN remark TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.document_version ALTER COLUMN rejection_reason TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.document_version ALTER COLUMN change_summary TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.context_issue ALTER COLUMN resolution TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.context_issue ALTER COLUMN impact TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.context_issue ALTER COLUMN description TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.conflict_declaration ALTER COLUMN mitigation TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.conflict_declaration ALTER COLUMN description TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.conflict_declaration ALTER COLUMN closure_note TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.complaint ALTER COLUMN validation_verdict TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.complaint ALTER COLUMN resolution TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.complaint ALTER COLUMN investigation_outcome TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.complaint ALTER COLUMN description TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.competency_record ALTER COLUMN revocation_reason TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.change_request ALTER COLUMN rejection_reason TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.change_request ALTER COLUMN implementation_notes TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.change_request ALTER COLUMN impact_analysis TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.capa_action ALTER COLUMN details TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.audit_trail_review ALTER COLUMN conclusion TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.audit_finding ALTER COLUMN description TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.audit_checklist_item ALTER COLUMN question TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.audit_checklist_item ALTER COLUMN evidence TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    ALTER TABLE qams.archive_entry ALTER COLUMN legal_hold_reason TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731180344_Hardening1_TypesAndNames') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260731180344_Hardening1_TypesAndNames', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731181845_Hardening2_RlsGapClosure') THEN
    ALTER TABLE audit.security_event ENABLE ROW LEVEL SECURITY;
    ALTER TABLE audit.security_event FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON audit.security_event;
    CREATE POLICY tenant_isolation ON audit.security_event
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id IS NULL
             OR tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731181845_Hardening2_RlsGapClosure') THEN
    ALTER TABLE qams.ref_counter ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.ref_counter FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.ref_counter;
    CREATE POLICY tenant_isolation ON qams.ref_counter
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731181845_Hardening2_RlsGapClosure') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260731181845_Hardening2_RlsGapClosure', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731191212_Hardening3_CheckDomains') THEN
    ALTER TABLE qams.archive_entry ADD CONSTRAINT ck_archive_entry_retention_class_domain CHECK (retention_class IN ('FiveYears', 'TenYears', 'Permanent')) NOT VALID;
    ALTER TABLE qams.archive_entry VALIDATE CONSTRAINT ck_archive_entry_retention_class_domain;
    ALTER TABLE qams.archive_entry ADD CONSTRAINT ck_archive_entry_state_domain CHECK (state IN ('Archived', 'Retrieved', 'Disposed')) NOT VALID;
    ALTER TABLE qams.archive_entry VALIDATE CONSTRAINT ck_archive_entry_state_domain;
    ALTER TABLE qams.audit ADD CONSTRAINT ck_audit_status_domain CHECK (status IN ('Scheduled', 'InProgress', 'SignedOff')) NOT VALID;
    ALTER TABLE qams.audit VALIDATE CONSTRAINT ck_audit_status_domain;
    ALTER TABLE qams.audit ADD CONSTRAINT ck_audit_type_domain CHECK (type IN ('Internal', 'ExternalHosted')) NOT VALID;
    ALTER TABLE qams.audit VALIDATE CONSTRAINT ck_audit_type_domain;
    ALTER TABLE qams.audit_checklist_item ADD CONSTRAINT ck_audit_checklist_item_verdict_domain CHECK (verdict IN ('Unanswered', 'Conform', 'Ofi', 'NonConform')) NOT VALID;
    ALTER TABLE qams.audit_checklist_item VALIDATE CONSTRAINT ck_audit_checklist_item_verdict_domain;
    ALTER TABLE qams.audit_finding ADD CONSTRAINT ck_audit_finding_grade_domain CHECK (grade IN ('Ofi', 'MinorNc', 'MajorNc')) NOT VALID;
    ALTER TABLE qams.audit_finding VALIDATE CONSTRAINT ck_audit_finding_grade_domain;
    ALTER TABLE qams.audit_trail_review ADD CONSTRAINT ck_audit_trail_review_status_domain CHECK (status IN ('Open', 'Completed')) NOT VALID;
    ALTER TABLE qams.audit_trail_review VALIDATE CONSTRAINT ck_audit_trail_review_status_domain;
    ALTER TABLE qams.capa_action ADD CONSTRAINT ck_capa_action_status_domain CHECK (status IN ('Open', 'Completed')) NOT VALID;
    ALTER TABLE qams.capa_action VALIDATE CONSTRAINT ck_capa_action_status_domain;
    ALTER TABLE qams.capa_action ADD CONSTRAINT ck_capa_action_type_domain CHECK (type IN ('Corrective', 'Preventive')) NOT VALID;
    ALTER TABLE qams.capa_action VALIDATE CONSTRAINT ck_capa_action_type_domain;
    ALTER TABLE qams.carryover_reading ADD CONSTRAINT ck_carryover_reading_kind_domain CHECK (kind IN ('High', 'Low')) NOT VALID;
    ALTER TABLE qams.carryover_reading VALIDATE CONSTRAINT ck_carryover_reading_kind_domain;
    ALTER TABLE qams.carryover_study ADD CONSTRAINT ck_carryover_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
    ALTER TABLE qams.carryover_study VALIDATE CONSTRAINT ck_carryover_study_state_domain;
    ALTER TABLE qams.change_request ADD CONSTRAINT ck_change_request_status_domain CHECK (status IN ('Proposed', 'Approved', 'Rejected', 'Closed', 'Reviewed')) NOT VALID;
    ALTER TABLE qams.change_request VALIDATE CONSTRAINT ck_change_request_status_domain;
    ALTER TABLE qams.competency_record ADD CONSTRAINT ck_competency_record_status_domain CHECK (status IN ('PendingTraining', 'Evaluated', 'Authorized', 'Revoked')) NOT VALID;
    ALTER TABLE qams.competency_record VALIDATE CONSTRAINT ck_competency_record_status_domain;
    ALTER TABLE qams.complaint ADD CONSTRAINT ck_complaint_channel_domain CHECK (channel IN ('Phone', 'Email', 'Portal', 'InPerson', 'Letter')) NOT VALID;
    ALTER TABLE qams.complaint VALIDATE CONSTRAINT ck_complaint_channel_domain;
    ALTER TABLE qams.complaint ADD CONSTRAINT ck_complaint_status_domain CHECK (status IN ('Logged', 'Acknowledged', 'Validated', 'Investigating', 'OutcomeLogged', 'Resolved', 'Closed', 'Invalid')) NOT VALID;
    ALTER TABLE qams.complaint VALIDATE CONSTRAINT ck_complaint_status_domain;
    ALTER TABLE qams.conflict_declaration ADD CONSTRAINT ck_conflict_declaration_outcome_domain CHECK (outcome IN ('Accepted', 'Mitigated', 'Withdrawn')) NOT VALID;
    ALTER TABLE qams.conflict_declaration VALIDATE CONSTRAINT ck_conflict_declaration_outcome_domain;
    ALTER TABLE qams.conflict_declaration ADD CONSTRAINT ck_conflict_declaration_risk_level_domain CHECK (risk_level IN ('Low', 'Medium', 'High')) NOT VALID;
    ALTER TABLE qams.conflict_declaration VALIDATE CONSTRAINT ck_conflict_declaration_risk_level_domain;
    ALTER TABLE qams.conflict_declaration ADD CONSTRAINT ck_conflict_declaration_status_domain CHECK (status IN ('Declared', 'Assessed', 'Closed')) NOT VALID;
    ALTER TABLE qams.conflict_declaration VALIDATE CONSTRAINT ck_conflict_declaration_status_domain;
    ALTER TABLE qams.context_issue ADD CONSTRAINT ck_context_issue_status_domain CHECK (status IN ('Active', 'Closed')) NOT VALID;
    ALTER TABLE qams.context_issue VALIDATE CONSTRAINT ck_context_issue_status_domain;
    ALTER TABLE qams.context_issue ADD CONSTRAINT ck_context_issue_type_domain CHECK (type IN ('Internal', 'External')) NOT VALID;
    ALTER TABLE qams.context_issue VALIDATE CONSTRAINT ck_context_issue_type_domain;
    ALTER TABLE qams.controlled_document ADD CONSTRAINT ck_controlled_document_status_domain CHECK (status IN ('Draft', 'Published', 'Obsolete')) NOT VALID;
    ALTER TABLE qams.controlled_document VALIDATE CONSTRAINT ck_controlled_document_status_domain;
    ALTER TABLE qams.detection_limit_study ADD CONSTRAINT ck_detection_limit_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
    ALTER TABLE qams.detection_limit_study VALIDATE CONSTRAINT ck_detection_limit_study_state_domain;
    ALTER TABLE qams.detection_measurement ADD CONSTRAINT ck_detection_measurement_kind_domain CHECK (kind IN ('Blank', 'LowLevel')) NOT VALID;
    ALTER TABLE qams.detection_measurement VALIDATE CONSTRAINT ck_detection_measurement_kind_domain;
    ALTER TABLE qams.document_controlled_copy ADD CONSTRAINT ck_document_controlled_copy_status_domain CHECK (status IN ('Issued', 'Returned', 'Destroyed')) NOT VALID;
    ALTER TABLE qams.document_controlled_copy VALIDATE CONSTRAINT ck_document_controlled_copy_status_domain;
    ALTER TABLE qams.document_version ADD CONSTRAINT ck_document_version_state_domain CHECK (state IN ('Draft', 'UnderReview', 'Approved', 'Published', 'Obsolete', 'Rejected')) NOT VALID;
    ALTER TABLE qams.document_version VALIDATE CONSTRAINT ck_document_version_state_domain;
    ALTER TABLE qams.equipment_item ADD CONSTRAINT ck_equipment_item_status_domain CHECK (status IN ('NeedsCalibration', 'Active', 'OutOfService', 'Retired')) NOT VALID;
    ALTER TABLE qams.equipment_item VALIDATE CONSTRAINT ck_equipment_item_status_domain;
    ALTER TABLE qams.feedback_entry ADD CONSTRAINT ck_feedback_entry_status_domain CHECK (status IN ('Logged', 'Reviewed', 'Closed', 'Escalated')) NOT VALID;
    ALTER TABLE qams.feedback_entry VALIDATE CONSTRAINT ck_feedback_entry_status_domain;
    ALTER TABLE qams.feedback_entry ADD CONSTRAINT ck_feedback_entry_type_domain CHECK (type IN ('Compliment', 'Suggestion', 'Dissatisfaction')) NOT VALID;
    ALTER TABLE qams.feedback_entry VALIDATE CONSTRAINT ck_feedback_entry_type_domain;
    ALTER TABLE qams.instrument_comparability_study ADD CONSTRAINT ck_instrument_comparability_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
    ALTER TABLE qams.instrument_comparability_study VALIDATE CONSTRAINT ck_instrument_comparability_study_state_domain;
    ALTER TABLE qams.interested_party ADD CONSTRAINT ck_interested_party_status_domain CHECK (status IN ('Active', 'Archived')) NOT VALID;
    ALTER TABLE qams.interested_party VALIDATE CONSTRAINT ck_interested_party_status_domain;
    ALTER TABLE qams.interference_study ADD CONSTRAINT ck_interference_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
    ALTER TABLE qams.interference_study VALIDATE CONSTRAINT ck_interference_study_state_domain;
    ALTER TABLE qams.linearity_study ADD CONSTRAINT ck_linearity_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
    ALTER TABLE qams.linearity_study VALIDATE CONSTRAINT ck_linearity_study_state_domain;
    ALTER TABLE qams.lot_comparison_study ADD CONSTRAINT ck_lot_comparison_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
    ALTER TABLE qams.lot_comparison_study VALIDATE CONSTRAINT ck_lot_comparison_study_state_domain;
    ALTER TABLE qams.management_review ADD CONSTRAINT ck_management_review_status_domain CHECK (status IN ('Scheduled', 'Closed')) NOT VALID;
    ALTER TABLE qams.management_review VALIDATE CONSTRAINT ck_management_review_status_domain;
    ALTER TABLE qams.method_comparison_study ADD CONSTRAINT ck_method_comparison_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
    ALTER TABLE qams.method_comparison_study VALIDATE CONSTRAINT ck_method_comparison_study_state_domain;
    ALTER TABLE qams.monitoring_point ADD CONSTRAINT ck_monitoring_point_status_domain CHECK (status IN ('Active', 'Suspended', 'Retired')) NOT VALID;
    ALTER TABLE qams.monitoring_point VALIDATE CONSTRAINT ck_monitoring_point_status_domain;
    ALTER TABLE qams.nonconformance ADD CONSTRAINT ck_nonconformance_event_type_domain CHECK (event_type IN ('Nonconformity', 'Deviation', 'OutOfSpecification', 'OutOfTrend')) NOT VALID;
    ALTER TABLE qams.nonconformance VALIDATE CONSTRAINT ck_nonconformance_event_type_domain;
    ALTER TABLE qams.nonconformance ADD CONSTRAINT ck_nonconformance_source_type_domain CHECK (source_type IN ('Internal', 'Complaint', 'Audit', 'Supplier', 'ProficiencyTest')) NOT VALID;
    ALTER TABLE qams.nonconformance VALIDATE CONSTRAINT ck_nonconformance_source_type_domain;
    ALTER TABLE qams.notification_dispatch ADD CONSTRAINT ck_notification_dispatch_email_status_domain CHECK (email_status IN ('Queued', 'Sent', 'Failed')) NOT VALID;
    ALTER TABLE qams.notification_dispatch VALIDATE CONSTRAINT ck_notification_dispatch_email_status_domain;
    ALTER TABLE qams.outlier_screening ADD CONSTRAINT ck_outlier_screening_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
    ALTER TABLE qams.outlier_screening VALIDATE CONSTRAINT ck_outlier_screening_state_domain;
    ALTER TABLE qams.precision_study ADD CONSTRAINT ck_precision_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
    ALTER TABLE qams.precision_study VALIDATE CONSTRAINT ck_precision_study_state_domain;
    ALTER TABLE qams.pt_enrollment ADD CONSTRAINT ck_pt_enrollment_performance_domain CHECK (performance IN ('Pending', 'Satisfactory', 'Questionable', 'Unsatisfactory')) NOT VALID;
    ALTER TABLE qams.pt_enrollment VALIDATE CONSTRAINT ck_pt_enrollment_performance_domain;
    ALTER TABLE qams.pt_plan ADD CONSTRAINT ck_pt_plan_status_domain CHECK (status IN ('Draft', 'Approved', 'Closed')) NOT VALID;
    ALTER TABLE qams.pt_plan VALIDATE CONSTRAINT ck_pt_plan_status_domain;
    ALTER TABLE qams.quality_objective ADD CONSTRAINT ck_quality_objective_direction_domain CHECK (direction IN ('AtLeast', 'AtMost')) NOT VALID;
    ALTER TABLE qams.quality_objective VALIDATE CONSTRAINT ck_quality_objective_direction_domain;
    ALTER TABLE qams.quality_objective ADD CONSTRAINT ck_quality_objective_status_domain CHECK (status IN ('Active', 'Achieved', 'Missed', 'Cancelled')) NOT VALID;
    ALTER TABLE qams.quality_objective VALIDATE CONSTRAINT ck_quality_objective_status_domain;
    ALTER TABLE qams.quality_policy ADD CONSTRAINT ck_quality_policy_status_domain CHECK (status IN ('Draft', 'Active', 'Superseded')) NOT VALID;
    ALTER TABLE qams.quality_policy VALIDATE CONSTRAINT ck_quality_policy_status_domain;
    ALTER TABLE qams.rca_record ADD CONSTRAINT ck_rca_record_method_domain CHECK (method IN ('FiveWhys', 'Fishbone', 'Other')) NOT VALID;
    ALTER TABLE qams.rca_record VALIDATE CONSTRAINT ck_rca_record_method_domain;
    ALTER TABLE qams.reference_interval_study ADD CONSTRAINT ck_reference_interval_study_state_domain CHECK (state IN ('DataEntry', 'Calculated', 'SignedOff')) NOT VALID;
    ALTER TABLE qams.reference_interval_study VALIDATE CONSTRAINT ck_reference_interval_study_state_domain;
    ALTER TABLE qams.reference_interval_study ADD CONSTRAINT ck_reference_interval_study_verdict_domain CHECK (verdict IN ('Verified', 'Rejected')) NOT VALID;
    ALTER TABLE qams.reference_interval_study VALIDATE CONSTRAINT ck_reference_interval_study_verdict_domain;
    ALTER TABLE qams.reference_standard ADD CONSTRAINT ck_reference_standard_status_domain CHECK (status IN ('Active', 'Quarantined', 'Expired', 'Retired')) NOT VALID;
    ALTER TABLE qams.reference_standard VALIDATE CONSTRAINT ck_reference_standard_status_domain;
    ALTER TABLE qams.reference_standard ADD CONSTRAINT ck_reference_standard_type_domain CHECK (type IN ('CertifiedReferenceMaterial', 'ReferenceStandard', 'WorkingStandard')) NOT VALID;
    ALTER TABLE qams.reference_standard VALIDATE CONSTRAINT ck_reference_standard_type_domain;
    ALTER TABLE qams.risk_item ADD CONSTRAINT ck_risk_item_status_domain CHECK (status IN ('Identified', 'Mitigating', 'Closed')) NOT VALID;
    ALTER TABLE qams.risk_item VALIDATE CONSTRAINT ck_risk_item_status_domain;
    ALTER TABLE qams.sigma_assessment ADD CONSTRAINT ck_sigma_assessment_grade_domain CHECK (grade IN ('Unacceptable', 'Marginal', 'Good', 'Excellent', 'WorldClass')) NOT VALID;
    ALTER TABLE qams.sigma_assessment VALIDATE CONSTRAINT ck_sigma_assessment_grade_domain;
    ALTER TABLE qams.sigma_assessment ADD CONSTRAINT ck_sigma_assessment_state_domain CHECK (state IN ('Draft', 'SignedOff')) NOT VALID;
    ALTER TABLE qams.sigma_assessment VALIDATE CONSTRAINT ck_sigma_assessment_state_domain;
    ALTER TABLE qams.supplier ADD CONSTRAINT ck_supplier_status_domain CHECK (status IN ('PendingEvaluation', 'Approved', 'Suspended')) NOT VALID;
    ALTER TABLE qams.supplier VALIDATE CONSTRAINT ck_supplier_status_domain;
    ALTER TABLE qams.test_authorization ADD CONSTRAINT ck_test_authorization_scope_domain CHECK (scope IN ('Perform', 'ReviewAndRelease', 'Train')) NOT VALID;
    ALTER TABLE qams.test_authorization VALIDATE CONSTRAINT ck_test_authorization_scope_domain;
    ALTER TABLE qams.test_authorization ADD CONSTRAINT ck_test_authorization_status_domain CHECK (status IN ('Active', 'Suspended', 'Revoked', 'Expired')) NOT VALID;
    ALTER TABLE qams.test_authorization VALIDATE CONSTRAINT ck_test_authorization_status_domain;
    ALTER TABLE qams.uncertainty_budget ADD CONSTRAINT ck_uncertainty_budget_status_domain CHECK (status IN ('Draft', 'Calculated', 'Approved')) NOT VALID;
    ALTER TABLE qams.uncertainty_budget VALIDATE CONSTRAINT ck_uncertainty_budget_status_domain;
    ALTER TABLE qams.uncertainty_component ADD CONSTRAINT ck_uncertainty_component_type_domain CHECK (type IN ('TypeA', 'TypeB')) NOT VALID;
    ALTER TABLE qams.uncertainty_component VALIDATE CONSTRAINT ck_uncertainty_component_type_domain;
    ALTER TABLE qams.user_access_review ADD CONSTRAINT ck_user_access_review_status_domain CHECK (status IN ('Open', 'Completed')) NOT VALID;
    ALTER TABLE qams.user_access_review VALIDATE CONSTRAINT ck_user_access_review_status_domain;
    ALTER TABLE qams.user_account ADD CONSTRAINT ck_user_account_role_domain CHECK (role IN ('PlatformAdmin', 'TenantAdmin', 'QualityManager', 'DepartmentHead', 'Analyst', 'ExternalAuditor')) NOT VALID;
    ALTER TABLE qams.user_account VALIDATE CONSTRAINT ck_user_account_role_domain;
    ALTER TABLE qams.validation_study ADD CONSTRAINT ck_validation_study_state_domain CHECK (state IN ('ProtocolConfigured', 'DataEntered', 'StatsCalculated', 'SignedOff')) NOT VALID;
    ALTER TABLE qams.validation_study VALIDATE CONSTRAINT ck_validation_study_state_domain;
    ALTER TABLE qams.work_task ADD CONSTRAINT ck_work_task_status_domain CHECK (status IN ('Pending', 'Completed')) NOT VALID;
    ALTER TABLE qams.work_task VALIDATE CONSTRAINT ck_work_task_status_domain;
    ALTER TABLE saas.tenant ADD CONSTRAINT ck_tenant_status_domain CHECK (status IN ('Provisioning', 'Active', 'Suspended', 'Terminated')) NOT VALID;
    ALTER TABLE saas.tenant VALIDATE CONSTRAINT ck_tenant_status_domain;
    ALTER TABLE qams.qc_run ADD CONSTRAINT ck_qc_run_outcome_domain CHECK (outcome IN ('InControl', 'Warning', 'OutOfControl')) NOT VALID;
    ALTER TABLE qams.qc_run VALIDATE CONSTRAINT ck_qc_run_outcome_domain;
    ALTER TABLE audit.field_change ADD CONSTRAINT ck_field_change_action_domain CHECK (action IN ('Created', 'Modified', 'Deleted')) NOT VALID;
    ALTER TABLE audit.field_change VALIDATE CONSTRAINT ck_field_change_action_domain;
    ALTER TABLE audit.audit_trail ADD CONSTRAINT ck_audit_trail_prev_hash_sha256 CHECK (prev_hash ~ '^[0-9a-f]{64}$') NOT VALID;
    ALTER TABLE audit.audit_trail VALIDATE CONSTRAINT ck_audit_trail_prev_hash_sha256;
    ALTER TABLE audit.audit_trail ADD CONSTRAINT ck_audit_trail_entry_hash_sha256 CHECK (entry_hash ~ '^[0-9a-f]{64}$') NOT VALID;
    ALTER TABLE audit.audit_trail VALIDATE CONSTRAINT ck_audit_trail_entry_hash_sha256;
    ALTER TABLE audit.electronic_signature ADD CONSTRAINT ck_electronic_signature_content_hash_sha256 CHECK (content_hash ~ '^[0-9a-f]{64}$') NOT VALID;
    ALTER TABLE audit.electronic_signature VALIDATE CONSTRAINT ck_electronic_signature_content_hash_sha256;
    ALTER TABLE qams.file_reference ADD CONSTRAINT ck_file_reference_sha256_sha256 CHECK (sha256 ~ '^[0-9a-f]{64}$') NOT VALID;
    ALTER TABLE qams.file_reference VALIDATE CONSTRAINT ck_file_reference_sha256_sha256;
    ALTER TABLE qams.refresh_session ADD CONSTRAINT ck_refresh_session_token_hash_sha256 CHECK (token_hash ~ '^[0-9A-F]{64}$') NOT VALID;
    ALTER TABLE qams.refresh_session VALIDATE CONSTRAINT ck_refresh_session_token_hash_sha256;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731191212_Hardening3_CheckDomains') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260731191212_Hardening3_CheckDomains', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.validation_replicate ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.user_department_access ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.user_branch_access ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.uncertainty_component ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.supplier_certificate ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.role_permission ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.review_decision ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.reference_sample ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.rca_record ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.pt_plan_item ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.precision_measurement ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.outlier_point ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.objective_progress ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.mitigation_action ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.measurement_pair ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.maintenance_record ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.lot_sample_pair ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.linearity_measurement ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.intermediate_check ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.interference_measurement ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.instrument_reading ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.environmental_reading ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.document_version ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.detection_measurement ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.carryover_reading ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.capa_action ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.calibration_record ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.audit_finding ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.audit_checklist_item ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    ALTER TABLE qams.assessment_result ADD tenant_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    -- 0) This migration is trusted infrastructure: the backfill below must read
    -- parent rows across every tenant, and the parents' own tenant_isolation
    -- policies would otherwise hide them from the tenant-less migration session
    -- (the round-trip proved it: every UPDATE..FROM was a no-op and the first
    -- composite FK failed on a nil tenant). Transaction-local, so nothing leaks.
    SET LOCAL app.bypass_rls = 'on';

    -- 1) Backfill tenant_id from the owning aggregate, then drop the nil default.
    UPDATE qams.assessment_result c SET tenant_id = p.tenant_id FROM qams.competency_record p WHERE p.id = c.competency_id;
    ALTER TABLE qams.assessment_result ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.audit_checklist_item c SET tenant_id = p.tenant_id FROM qams.audit p WHERE p.id = c.audit_id;
    ALTER TABLE qams.audit_checklist_item ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.audit_finding c SET tenant_id = p.tenant_id FROM qams.audit p WHERE p.id = c.audit_id;
    ALTER TABLE qams.audit_finding ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.calibration_record c SET tenant_id = p.tenant_id FROM qams.equipment_item p WHERE p.id = c.equipment_id;
    ALTER TABLE qams.calibration_record ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.capa_action c SET tenant_id = p.tenant_id FROM qams.nonconformance p WHERE p.id = c.nc_id;
    ALTER TABLE qams.capa_action ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.carryover_reading c SET tenant_id = p.tenant_id FROM qams.carryover_study p WHERE p.id = c.study_id;
    ALTER TABLE qams.carryover_reading ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.detection_measurement c SET tenant_id = p.tenant_id FROM qams.detection_limit_study p WHERE p.id = c.study_id;
    ALTER TABLE qams.detection_measurement ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.document_version c SET tenant_id = p.tenant_id FROM qams.controlled_document p WHERE p.id = c.document_id;
    ALTER TABLE qams.document_version ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.environmental_reading c SET tenant_id = p.tenant_id FROM qams.monitoring_point p WHERE p.id = c.point_id;
    ALTER TABLE qams.environmental_reading ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.instrument_reading c SET tenant_id = p.tenant_id FROM qams.instrument_comparability_study p WHERE p.id = c.study_id;
    ALTER TABLE qams.instrument_reading ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.interference_measurement c SET tenant_id = p.tenant_id FROM qams.interference_study p WHERE p.id = c.study_id;
    ALTER TABLE qams.interference_measurement ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.intermediate_check c SET tenant_id = p.tenant_id FROM qams.equipment_item p WHERE p.id = c.equipment_id;
    ALTER TABLE qams.intermediate_check ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.linearity_measurement c SET tenant_id = p.tenant_id FROM qams.linearity_study p WHERE p.id = c.study_id;
    ALTER TABLE qams.linearity_measurement ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.lot_sample_pair c SET tenant_id = p.tenant_id FROM qams.lot_comparison_study p WHERE p.id = c.study_id;
    ALTER TABLE qams.lot_sample_pair ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.maintenance_record c SET tenant_id = p.tenant_id FROM qams.equipment_item p WHERE p.id = c.equipment_id;
    ALTER TABLE qams.maintenance_record ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.measurement_pair c SET tenant_id = p.tenant_id FROM qams.method_comparison_study p WHERE p.id = c.study_id;
    ALTER TABLE qams.measurement_pair ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.mitigation_action c SET tenant_id = p.tenant_id FROM qams.risk_item p WHERE p.id = c.risk_id;
    ALTER TABLE qams.mitigation_action ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.objective_progress c SET tenant_id = p.tenant_id FROM qams.quality_objective p WHERE p.id = c.objective_id;
    ALTER TABLE qams.objective_progress ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.outlier_point c SET tenant_id = p.tenant_id FROM qams.outlier_screening p WHERE p.id = c.screening_id;
    ALTER TABLE qams.outlier_point ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.precision_measurement c SET tenant_id = p.tenant_id FROM qams.precision_study p WHERE p.id = c.study_id;
    ALTER TABLE qams.precision_measurement ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.pt_plan_item c SET tenant_id = p.tenant_id FROM qams.pt_plan p WHERE p.id = c.plan_id;
    ALTER TABLE qams.pt_plan_item ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.rca_record c SET tenant_id = p.tenant_id FROM qams.nonconformance p WHERE p.id = c.nc_id;
    ALTER TABLE qams.rca_record ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.reference_sample c SET tenant_id = p.tenant_id FROM qams.reference_interval_study p WHERE p.id = c.study_id;
    ALTER TABLE qams.reference_sample ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.review_decision c SET tenant_id = p.tenant_id FROM qams.management_review p WHERE p.id = c.review_id;
    ALTER TABLE qams.review_decision ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.role_permission c SET tenant_id = p.tenant_id FROM qams.role p WHERE p.id = c.role_id;
    ALTER TABLE qams.role_permission ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.supplier_certificate c SET tenant_id = p.tenant_id FROM qams.supplier p WHERE p.id = c.supplier_id;
    ALTER TABLE qams.supplier_certificate ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.uncertainty_component c SET tenant_id = p.tenant_id FROM qams.uncertainty_budget p WHERE p.id = c.budget_id;
    ALTER TABLE qams.uncertainty_component ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.user_branch_access c SET tenant_id = p.tenant_id FROM qams.user_account p WHERE p.id = c.user_id;
    ALTER TABLE qams.user_branch_access ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.user_department_access c SET tenant_id = p.tenant_id FROM qams.user_account p WHERE p.id = c.user_id;
    ALTER TABLE qams.user_department_access ALTER COLUMN tenant_id DROP DEFAULT;
    UPDATE qams.validation_replicate c SET tenant_id = p.tenant_id FROM qams.validation_study p WHERE p.id = c.study_id;
    ALTER TABLE qams.validation_replicate ALTER COLUMN tenant_id DROP DEFAULT;

    -- 2) One UNIQUE (id, tenant_id) per parent, so the composite FKs have a target.
    ALTER TABLE qams.audit ADD CONSTRAINT ux_audit_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.carryover_study ADD CONSTRAINT ux_carryover_study_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.competency_record ADD CONSTRAINT ux_competency_record_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.controlled_document ADD CONSTRAINT ux_controlled_document_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.detection_limit_study ADD CONSTRAINT ux_detection_limit_study_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.equipment_item ADD CONSTRAINT ux_equipment_item_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.instrument_comparability_study ADD CONSTRAINT ux_instrument_comparability_study_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.interference_study ADD CONSTRAINT ux_interference_study_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.linearity_study ADD CONSTRAINT ux_linearity_study_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.lot_comparison_study ADD CONSTRAINT ux_lot_comparison_study_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.management_review ADD CONSTRAINT ux_management_review_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.method_comparison_study ADD CONSTRAINT ux_method_comparison_study_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.monitoring_point ADD CONSTRAINT ux_monitoring_point_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.nonconformance ADD CONSTRAINT ux_nonconformance_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.outlier_screening ADD CONSTRAINT ux_outlier_screening_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.precision_study ADD CONSTRAINT ux_precision_study_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.pt_plan ADD CONSTRAINT ux_pt_plan_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.quality_objective ADD CONSTRAINT ux_quality_objective_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.reference_interval_study ADD CONSTRAINT ux_reference_interval_study_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.risk_item ADD CONSTRAINT ux_risk_item_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.role ADD CONSTRAINT ux_role_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.supplier ADD CONSTRAINT ux_supplier_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.uncertainty_budget ADD CONSTRAINT ux_uncertainty_budget_id_tenant UNIQUE (id, tenant_id);
    ALTER TABLE qams.validation_study ADD CONSTRAINT ux_validation_study_id_tenant UNIQUE (id, tenant_id);

    -- 3) Swap each single-column CASCADE FK for the tenant-composite one:
    --    a child row with a tenant different from its parent's becomes impossible.
    ALTER TABLE qams.assessment_result DROP CONSTRAINT fk_assessment_result_competency_record_competency_id;
    ALTER TABLE qams.assessment_result ADD CONSTRAINT fk_assessment_result_competency_record_tenant FOREIGN KEY (competency_id, tenant_id)
      REFERENCES qams.competency_record (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.audit_checklist_item DROP CONSTRAINT fk_audit_checklist_item_audit_audit_id;
    ALTER TABLE qams.audit_checklist_item ADD CONSTRAINT fk_audit_checklist_item_audit_tenant FOREIGN KEY (audit_id, tenant_id)
      REFERENCES qams.audit (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.audit_finding DROP CONSTRAINT fk_audit_finding_audit_audit_id;
    ALTER TABLE qams.audit_finding ADD CONSTRAINT fk_audit_finding_audit_tenant FOREIGN KEY (audit_id, tenant_id)
      REFERENCES qams.audit (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.calibration_record DROP CONSTRAINT fk_calibration_record_equipment_item_equipment_id;
    ALTER TABLE qams.calibration_record ADD CONSTRAINT fk_calibration_record_equipment_item_tenant FOREIGN KEY (equipment_id, tenant_id)
      REFERENCES qams.equipment_item (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.capa_action DROP CONSTRAINT fk_capa_action_nonconformance_nc_id;
    ALTER TABLE qams.capa_action ADD CONSTRAINT fk_capa_action_nonconformance_tenant FOREIGN KEY (nc_id, tenant_id)
      REFERENCES qams.nonconformance (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.carryover_reading DROP CONSTRAINT fk_carryover_reading_carryover_study_study_id;
    ALTER TABLE qams.carryover_reading ADD CONSTRAINT fk_carryover_reading_carryover_study_tenant FOREIGN KEY (study_id, tenant_id)
      REFERENCES qams.carryover_study (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.detection_measurement DROP CONSTRAINT fk_detection_measurement_detection_limit_study_study_id;
    ALTER TABLE qams.detection_measurement ADD CONSTRAINT fk_detection_measurement_detection_limit_study_tenant FOREIGN KEY (study_id, tenant_id)
      REFERENCES qams.detection_limit_study (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.document_version DROP CONSTRAINT fk_document_version_controlled_document_document_id;
    ALTER TABLE qams.document_version ADD CONSTRAINT fk_document_version_controlled_document_tenant FOREIGN KEY (document_id, tenant_id)
      REFERENCES qams.controlled_document (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.environmental_reading DROP CONSTRAINT fk_environmental_reading_monitoring_point_point_id;
    ALTER TABLE qams.environmental_reading ADD CONSTRAINT fk_environmental_reading_monitoring_point_tenant FOREIGN KEY (point_id, tenant_id)
      REFERENCES qams.monitoring_point (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.instrument_reading DROP CONSTRAINT fk_instrument_reading_instrument_comparability_study_study_id;
    ALTER TABLE qams.instrument_reading ADD CONSTRAINT fk_instrument_reading_instrument_comparability_study_tenant FOREIGN KEY (study_id, tenant_id)
      REFERENCES qams.instrument_comparability_study (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.interference_measurement DROP CONSTRAINT fk_interference_measurement_interference_study_study_id;
    ALTER TABLE qams.interference_measurement ADD CONSTRAINT fk_interference_measurement_interference_study_tenant FOREIGN KEY (study_id, tenant_id)
      REFERENCES qams.interference_study (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.intermediate_check DROP CONSTRAINT fk_intermediate_check_equipment_item_equipment_id;
    ALTER TABLE qams.intermediate_check ADD CONSTRAINT fk_intermediate_check_equipment_item_tenant FOREIGN KEY (equipment_id, tenant_id)
      REFERENCES qams.equipment_item (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.linearity_measurement DROP CONSTRAINT fk_linearity_measurement_linearity_study_study_id;
    ALTER TABLE qams.linearity_measurement ADD CONSTRAINT fk_linearity_measurement_linearity_study_tenant FOREIGN KEY (study_id, tenant_id)
      REFERENCES qams.linearity_study (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.lot_sample_pair DROP CONSTRAINT fk_lot_sample_pair_lot_comparison_study_study_id;
    ALTER TABLE qams.lot_sample_pair ADD CONSTRAINT fk_lot_sample_pair_lot_comparison_study_tenant FOREIGN KEY (study_id, tenant_id)
      REFERENCES qams.lot_comparison_study (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.maintenance_record DROP CONSTRAINT fk_maintenance_record_equipment_item_equipment_id;
    ALTER TABLE qams.maintenance_record ADD CONSTRAINT fk_maintenance_record_equipment_item_tenant FOREIGN KEY (equipment_id, tenant_id)
      REFERENCES qams.equipment_item (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.measurement_pair DROP CONSTRAINT fk_measurement_pair_method_comparison_study_study_id;
    ALTER TABLE qams.measurement_pair ADD CONSTRAINT fk_measurement_pair_method_comparison_study_tenant FOREIGN KEY (study_id, tenant_id)
      REFERENCES qams.method_comparison_study (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.mitigation_action DROP CONSTRAINT fk_mitigation_action_risk_item_risk_id;
    ALTER TABLE qams.mitigation_action ADD CONSTRAINT fk_mitigation_action_risk_item_tenant FOREIGN KEY (risk_id, tenant_id)
      REFERENCES qams.risk_item (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.objective_progress DROP CONSTRAINT fk_objective_progress_quality_objective_objective_id;
    ALTER TABLE qams.objective_progress ADD CONSTRAINT fk_objective_progress_quality_objective_tenant FOREIGN KEY (objective_id, tenant_id)
      REFERENCES qams.quality_objective (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.outlier_point DROP CONSTRAINT fk_outlier_point_outlier_screening_screening_id;
    ALTER TABLE qams.outlier_point ADD CONSTRAINT fk_outlier_point_outlier_screening_tenant FOREIGN KEY (screening_id, tenant_id)
      REFERENCES qams.outlier_screening (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.precision_measurement DROP CONSTRAINT fk_precision_measurement_precision_study_study_id;
    ALTER TABLE qams.precision_measurement ADD CONSTRAINT fk_precision_measurement_precision_study_tenant FOREIGN KEY (study_id, tenant_id)
      REFERENCES qams.precision_study (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.pt_plan_item DROP CONSTRAINT fk_pt_plan_item_pt_plan_plan_id;
    ALTER TABLE qams.pt_plan_item ADD CONSTRAINT fk_pt_plan_item_pt_plan_tenant FOREIGN KEY (plan_id, tenant_id)
      REFERENCES qams.pt_plan (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.rca_record DROP CONSTRAINT fk_rca_record_nonconformance_nc_id;
    ALTER TABLE qams.rca_record ADD CONSTRAINT fk_rca_record_nonconformance_tenant FOREIGN KEY (nc_id, tenant_id)
      REFERENCES qams.nonconformance (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.reference_sample DROP CONSTRAINT fk_reference_sample_reference_interval_study_study_id;
    ALTER TABLE qams.reference_sample ADD CONSTRAINT fk_reference_sample_reference_interval_study_tenant FOREIGN KEY (study_id, tenant_id)
      REFERENCES qams.reference_interval_study (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.review_decision DROP CONSTRAINT fk_review_decision_management_review_review_id;
    ALTER TABLE qams.review_decision ADD CONSTRAINT fk_review_decision_management_review_tenant FOREIGN KEY (review_id, tenant_id)
      REFERENCES qams.management_review (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.role_permission DROP CONSTRAINT fk_role_permission_role_role_id;
    ALTER TABLE qams.role_permission ADD CONSTRAINT fk_role_permission_role_tenant FOREIGN KEY (role_id, tenant_id)
      REFERENCES qams.role (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.supplier_certificate DROP CONSTRAINT fk_supplier_certificate_supplier_supplier_id;
    ALTER TABLE qams.supplier_certificate ADD CONSTRAINT fk_supplier_certificate_supplier_tenant FOREIGN KEY (supplier_id, tenant_id)
      REFERENCES qams.supplier (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.uncertainty_component DROP CONSTRAINT fk_uncertainty_component_uncertainty_budget_budget_id;
    ALTER TABLE qams.uncertainty_component ADD CONSTRAINT fk_uncertainty_component_uncertainty_budget_tenant FOREIGN KEY (budget_id, tenant_id)
      REFERENCES qams.uncertainty_budget (id, tenant_id) ON DELETE CASCADE;
    ALTER TABLE qams.validation_replicate DROP CONSTRAINT fk_validation_replicate_validation_study_study_id;
    ALTER TABLE qams.validation_replicate ADD CONSTRAINT fk_validation_replicate_validation_study_tenant FOREIGN KEY (study_id, tenant_id)
      REFERENCES qams.validation_study (id, tenant_id) ON DELETE CASCADE;

    -- 4) RLS on every child - the CASCADE FK never isolated reads; this does.
    ALTER TABLE qams.assessment_result ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.assessment_result FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.assessment_result;
    CREATE POLICY tenant_isolation ON qams.assessment_result
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.audit_checklist_item ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.audit_checklist_item FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.audit_checklist_item;
    CREATE POLICY tenant_isolation ON qams.audit_checklist_item
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.audit_finding ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.audit_finding FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.audit_finding;
    CREATE POLICY tenant_isolation ON qams.audit_finding
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.calibration_record ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.calibration_record FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.calibration_record;
    CREATE POLICY tenant_isolation ON qams.calibration_record
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.capa_action ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.capa_action FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.capa_action;
    CREATE POLICY tenant_isolation ON qams.capa_action
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.carryover_reading ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.carryover_reading FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.carryover_reading;
    CREATE POLICY tenant_isolation ON qams.carryover_reading
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.detection_measurement ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.detection_measurement FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.detection_measurement;
    CREATE POLICY tenant_isolation ON qams.detection_measurement
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.document_version ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.document_version FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.document_version;
    CREATE POLICY tenant_isolation ON qams.document_version
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.environmental_reading ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.environmental_reading FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.environmental_reading;
    CREATE POLICY tenant_isolation ON qams.environmental_reading
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.instrument_reading ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.instrument_reading FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.instrument_reading;
    CREATE POLICY tenant_isolation ON qams.instrument_reading
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.interference_measurement ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.interference_measurement FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.interference_measurement;
    CREATE POLICY tenant_isolation ON qams.interference_measurement
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.intermediate_check ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.intermediate_check FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.intermediate_check;
    CREATE POLICY tenant_isolation ON qams.intermediate_check
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.linearity_measurement ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.linearity_measurement FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.linearity_measurement;
    CREATE POLICY tenant_isolation ON qams.linearity_measurement
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.lot_sample_pair ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.lot_sample_pair FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.lot_sample_pair;
    CREATE POLICY tenant_isolation ON qams.lot_sample_pair
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.maintenance_record ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.maintenance_record FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.maintenance_record;
    CREATE POLICY tenant_isolation ON qams.maintenance_record
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.measurement_pair ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.measurement_pair FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.measurement_pair;
    CREATE POLICY tenant_isolation ON qams.measurement_pair
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.mitigation_action ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.mitigation_action FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.mitigation_action;
    CREATE POLICY tenant_isolation ON qams.mitigation_action
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.objective_progress ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.objective_progress FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.objective_progress;
    CREATE POLICY tenant_isolation ON qams.objective_progress
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.outlier_point ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.outlier_point FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.outlier_point;
    CREATE POLICY tenant_isolation ON qams.outlier_point
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.precision_measurement ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.precision_measurement FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.precision_measurement;
    CREATE POLICY tenant_isolation ON qams.precision_measurement
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.pt_plan_item ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.pt_plan_item FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.pt_plan_item;
    CREATE POLICY tenant_isolation ON qams.pt_plan_item
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.rca_record ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.rca_record FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.rca_record;
    CREATE POLICY tenant_isolation ON qams.rca_record
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.reference_sample ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.reference_sample FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.reference_sample;
    CREATE POLICY tenant_isolation ON qams.reference_sample
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.review_decision ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.review_decision FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.review_decision;
    CREATE POLICY tenant_isolation ON qams.review_decision
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.role_permission ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.role_permission FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.role_permission;
    CREATE POLICY tenant_isolation ON qams.role_permission
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.supplier_certificate ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.supplier_certificate FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.supplier_certificate;
    CREATE POLICY tenant_isolation ON qams.supplier_certificate
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.uncertainty_component ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.uncertainty_component FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.uncertainty_component;
    CREATE POLICY tenant_isolation ON qams.uncertainty_component
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.user_branch_access ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.user_branch_access FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.user_branch_access;
    CREATE POLICY tenant_isolation ON qams.user_branch_access
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.user_department_access ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.user_department_access FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.user_department_access;
    CREATE POLICY tenant_isolation ON qams.user_department_access
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    ALTER TABLE qams.validation_replicate ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.validation_replicate FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.validation_replicate;
    CREATE POLICY tenant_isolation ON qams.validation_replicate
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');

    -- 5) Tenant FKs on the five elevated-writer tables (plan 4.5) - RESTRICT, never CASCADE.
    ALTER TABLE qams.outbox_event ADD CONSTRAINT fk_outbox_event_tenant FOREIGN KEY (tenant_id)
      REFERENCES saas.tenant (id) ON DELETE RESTRICT NOT VALID;
    ALTER TABLE qams.outbox_event VALIDATE CONSTRAINT fk_outbox_event_tenant;
    ALTER TABLE qams.ref_counter ADD CONSTRAINT fk_ref_counter_tenant FOREIGN KEY (tenant_id)
      REFERENCES saas.tenant (id) ON DELETE RESTRICT NOT VALID;
    ALTER TABLE qams.ref_counter VALIDATE CONSTRAINT fk_ref_counter_tenant;
    ALTER TABLE read.kpi_snapshot ADD CONSTRAINT fk_kpi_snapshot_tenant FOREIGN KEY (tenant_id)
      REFERENCES saas.tenant (id) ON DELETE RESTRICT NOT VALID;
    ALTER TABLE read.kpi_snapshot VALIDATE CONSTRAINT fk_kpi_snapshot_tenant;
    ALTER TABLE qams.branch ADD CONSTRAINT fk_branch_tenant FOREIGN KEY (tenant_id)
      REFERENCES saas.tenant (id) ON DELETE RESTRICT NOT VALID;
    ALTER TABLE qams.branch VALIDATE CONSTRAINT fk_branch_tenant;
    ALTER TABLE qams.user_account ADD CONSTRAINT fk_user_account_tenant FOREIGN KEY (tenant_id)
      REFERENCES saas.tenant (id) ON DELETE RESTRICT NOT VALID;
    ALTER TABLE qams.user_account VALIDATE CONSTRAINT fk_user_account_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731201114_Hardening4_ChildTenancy') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260731201114_Hardening4_ChildTenancy', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.assessment_result DROP CONSTRAINT fk_assessment_result_competency_record_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.audit_checklist_item DROP CONSTRAINT fk_audit_checklist_item_audit_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.audit_finding DROP CONSTRAINT fk_audit_finding_audit_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.calibration_record DROP CONSTRAINT fk_calibration_record_equipment_item_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.capa_action DROP CONSTRAINT fk_capa_action_nonconformance_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.carryover_reading DROP CONSTRAINT fk_carryover_reading_carryover_study_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.department DROP CONSTRAINT fk_department_branch_branch_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.detection_measurement DROP CONSTRAINT fk_detection_measurement_detection_limit_study_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.document_version DROP CONSTRAINT fk_document_version_controlled_document_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.environmental_reading DROP CONSTRAINT fk_environmental_reading_monitoring_point_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.instrument_reading DROP CONSTRAINT fk_instrument_reading_instrument_comparability_study_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.interference_measurement DROP CONSTRAINT fk_interference_measurement_interference_study_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.intermediate_check DROP CONSTRAINT fk_intermediate_check_equipment_item_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.linearity_measurement DROP CONSTRAINT fk_linearity_measurement_linearity_study_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.lot_sample_pair DROP CONSTRAINT fk_lot_sample_pair_lot_comparison_study_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.maintenance_record DROP CONSTRAINT fk_maintenance_record_equipment_item_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.measurement_pair DROP CONSTRAINT fk_measurement_pair_method_comparison_study_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.mitigation_action DROP CONSTRAINT fk_mitigation_action_risk_item_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.objective_progress DROP CONSTRAINT fk_objective_progress_quality_objective_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.outlier_point DROP CONSTRAINT fk_outlier_point_outlier_screening_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.precision_measurement DROP CONSTRAINT fk_precision_measurement_precision_study_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.pt_plan_item DROP CONSTRAINT fk_pt_plan_item_pt_plan_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.rca_record DROP CONSTRAINT fk_rca_record_nonconformance_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.reference_sample DROP CONSTRAINT fk_reference_sample_reference_interval_study_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.review_decision DROP CONSTRAINT fk_review_decision_management_review_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.role_permission DROP CONSTRAINT fk_role_permission_role_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.supplier_certificate DROP CONSTRAINT fk_supplier_certificate_supplier_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.uncertainty_component DROP CONSTRAINT fk_uncertainty_component_uncertainty_budget_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.validation_replicate DROP CONSTRAINT fk_validation_replicate_validation_study_tenant;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.work_task DROP CONSTRAINT pk_work_task;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.validation_study DROP CONSTRAINT pk_validation_study;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.validation_replicate DROP CONSTRAINT pk_validation_replicate;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_validation_replicate_study_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.user_department_access DROP CONSTRAINT pk_user_department_access;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.user_branch_access DROP CONSTRAINT pk_user_branch_access;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.user_access_review DROP CONSTRAINT pk_user_access_review;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.uncertainty_component DROP CONSTRAINT pk_uncertainty_component;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_uncertainty_component_budget_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.uncertainty_budget DROP CONSTRAINT pk_uncertainty_budget;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.training_assignment DROP CONSTRAINT pk_training_assignment;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.test_catalog_item DROP CONSTRAINT pk_test_catalog_item;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.test_authorization DROP CONSTRAINT pk_test_authorization;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.supplier_evaluation DROP CONSTRAINT pk_supplier_evaluation;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.supplier_certificate DROP CONSTRAINT pk_supplier_certificate;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_supplier_certificate_supplier_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.supplier DROP CONSTRAINT pk_supplier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.sla_definition DROP CONSTRAINT pk_sla_definition;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.sigma_assessment DROP CONSTRAINT pk_sigma_assessment;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.role_permission DROP CONSTRAINT pk_role_permission;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.role DROP CONSTRAINT pk_role;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.risk_item DROP CONSTRAINT pk_risk_item;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.review_decision DROP CONSTRAINT pk_review_decision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_review_decision_review_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.reference_standard DROP CONSTRAINT pk_reference_standard;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.reference_sample DROP CONSTRAINT pk_reference_sample;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_reference_sample_study_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.reference_interval_study DROP CONSTRAINT pk_reference_interval_study;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.rca_record DROP CONSTRAINT pk_rca_record;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_rca_record_nc_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.quality_policy DROP CONSTRAINT pk_quality_policy;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.quality_objective DROP CONSTRAINT pk_quality_objective;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.qc_run DROP CONSTRAINT pk_qc_run;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.qc_profile DROP CONSTRAINT pk_qc_profile;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.pt_plan_item DROP CONSTRAINT pk_pt_plan_item;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_pt_plan_item_plan_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.pt_plan DROP CONSTRAINT pk_pt_plan;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.pt_enrollment DROP CONSTRAINT pk_pt_enrollment;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.precision_study DROP CONSTRAINT pk_precision_study;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.precision_measurement DROP CONSTRAINT pk_precision_measurement;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_precision_measurement_study_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.outlier_screening DROP CONSTRAINT pk_outlier_screening;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.outlier_point DROP CONSTRAINT pk_outlier_point;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_outlier_point_screening_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.objective_progress DROP CONSTRAINT pk_objective_progress;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_objective_progress_objective_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.notification_rule DROP CONSTRAINT pk_notification_rule;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.notification_dispatch DROP CONSTRAINT pk_notification_dispatch;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.nonconformance DROP CONSTRAINT pk_nonconformance;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.monitoring_point DROP CONSTRAINT pk_monitoring_point;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.mitigation_action DROP CONSTRAINT pk_mitigation_action;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_mitigation_action_risk_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.method_comparison_study DROP CONSTRAINT pk_method_comparison_study;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.measurement_pair DROP CONSTRAINT pk_measurement_pair;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_measurement_pair_study_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.management_review DROP CONSTRAINT pk_management_review;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.maintenance_record DROP CONSTRAINT pk_maintenance_record;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_maintenance_record_equipment_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.lov_entry DROP CONSTRAINT pk_lov_entry;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.lot_sample_pair DROP CONSTRAINT pk_lot_sample_pair;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_lot_sample_pair_study_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.lot_comparison_study DROP CONSTRAINT pk_lot_comparison_study;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.linearity_study DROP CONSTRAINT pk_linearity_study;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.linearity_measurement DROP CONSTRAINT pk_linearity_measurement;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_linearity_measurement_study_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE read.kpi_snapshot DROP CONSTRAINT pk_kpi_snapshot;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.intermediate_check DROP CONSTRAINT pk_intermediate_check;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_intermediate_check_equipment_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.interference_study DROP CONSTRAINT pk_interference_study;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.interference_measurement DROP CONSTRAINT pk_interference_measurement;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_interference_measurement_study_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.interested_party DROP CONSTRAINT pk_interested_party;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.instrument_reading DROP CONSTRAINT pk_instrument_reading;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_instrument_reading_study_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.instrument_comparability_study DROP CONSTRAINT pk_instrument_comparability_study;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.file_reference DROP CONSTRAINT pk_file_reference;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.feedback_entry DROP CONSTRAINT pk_feedback_entry;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.escalation_timer DROP CONSTRAINT pk_escalation_timer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.equipment_item DROP CONSTRAINT pk_equipment_item;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.environmental_reading DROP CONSTRAINT pk_environmental_reading;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE audit.electronic_signature DROP CONSTRAINT pk_electronic_signature;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.document_version DROP CONSTRAINT pk_document_version;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_document_version_document_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.document_controlled_copy DROP CONSTRAINT pk_document_controlled_copy;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.document_acknowledgement DROP CONSTRAINT pk_document_acknowledgement;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.detection_measurement DROP CONSTRAINT pk_detection_measurement;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_detection_measurement_study_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.detection_limit_study DROP CONSTRAINT pk_detection_limit_study;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.department DROP CONSTRAINT pk_department;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_department_branch_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.controlled_document DROP CONSTRAINT pk_controlled_document;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.context_issue DROP CONSTRAINT pk_context_issue;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.conflict_declaration DROP CONSTRAINT pk_conflict_declaration;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.complaint DROP CONSTRAINT pk_complaint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.competency_record DROP CONSTRAINT pk_competency_record;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.change_request DROP CONSTRAINT pk_change_request;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.carryover_study DROP CONSTRAINT pk_carryover_study;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.carryover_reading DROP CONSTRAINT pk_carryover_reading;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_carryover_reading_study_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.capa_action DROP CONSTRAINT pk_capa_action;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_capa_action_nc_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.calibration_record DROP CONSTRAINT pk_calibration_record;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_calibration_record_equipment_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.branch DROP CONSTRAINT pk_branch;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.audit_trail_review DROP CONSTRAINT pk_audit_trail_review;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE audit.audit_trail DROP CONSTRAINT pk_audit_trail;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.audit_finding DROP CONSTRAINT pk_audit_finding;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_audit_finding_audit_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.audit_checklist_item DROP CONSTRAINT pk_audit_checklist_item;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_audit_checklist_item_audit_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.audit DROP CONSTRAINT pk_audit;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.assessment_result DROP CONSTRAINT pk_assessment_result;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    DROP INDEX qams.ix_assessment_result_competency_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.archive_entry DROP CONSTRAINT pk_archive_entry;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.work_task ADD CONSTRAINT pk_work_task PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.validation_study ADD CONSTRAINT pk_validation_study PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.validation_replicate ADD CONSTRAINT pk_validation_replicate PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.user_department_access ADD CONSTRAINT pk_user_department_access PRIMARY KEY (tenant_id, user_id, department_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.user_branch_access ADD CONSTRAINT pk_user_branch_access PRIMARY KEY (tenant_id, user_id, branch_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.user_access_review ADD CONSTRAINT pk_user_access_review PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.uncertainty_component ADD CONSTRAINT pk_uncertainty_component PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.uncertainty_budget ADD CONSTRAINT pk_uncertainty_budget PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.training_assignment ADD CONSTRAINT pk_training_assignment PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.test_catalog_item ADD CONSTRAINT pk_test_catalog_item PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.test_authorization ADD CONSTRAINT pk_test_authorization PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.supplier_evaluation ADD CONSTRAINT pk_supplier_evaluation PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.supplier_certificate ADD CONSTRAINT pk_supplier_certificate PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.supplier ADD CONSTRAINT pk_supplier PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.sla_definition ADD CONSTRAINT pk_sla_definition PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.sigma_assessment ADD CONSTRAINT pk_sigma_assessment PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.role_permission ADD CONSTRAINT pk_role_permission PRIMARY KEY (tenant_id, role_id, permission_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.role ADD CONSTRAINT pk_role PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.risk_item ADD CONSTRAINT pk_risk_item PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.review_decision ADD CONSTRAINT pk_review_decision PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.reference_standard ADD CONSTRAINT pk_reference_standard PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.reference_sample ADD CONSTRAINT pk_reference_sample PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.reference_interval_study ADD CONSTRAINT pk_reference_interval_study PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.rca_record ADD CONSTRAINT pk_rca_record PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.quality_policy ADD CONSTRAINT pk_quality_policy PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.quality_objective ADD CONSTRAINT pk_quality_objective PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.qc_run ADD CONSTRAINT pk_qc_run PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.qc_profile ADD CONSTRAINT pk_qc_profile PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.pt_plan_item ADD CONSTRAINT pk_pt_plan_item PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.pt_plan ADD CONSTRAINT pk_pt_plan PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.pt_enrollment ADD CONSTRAINT pk_pt_enrollment PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.precision_study ADD CONSTRAINT pk_precision_study PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.precision_measurement ADD CONSTRAINT pk_precision_measurement PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.outlier_screening ADD CONSTRAINT pk_outlier_screening PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.outlier_point ADD CONSTRAINT pk_outlier_point PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.objective_progress ADD CONSTRAINT pk_objective_progress PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.notification_rule ADD CONSTRAINT pk_notification_rule PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.notification_dispatch ADD CONSTRAINT pk_notification_dispatch PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.nonconformance ADD CONSTRAINT pk_nonconformance PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.monitoring_point ADD CONSTRAINT pk_monitoring_point PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.mitigation_action ADD CONSTRAINT pk_mitigation_action PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.method_comparison_study ADD CONSTRAINT pk_method_comparison_study PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.measurement_pair ADD CONSTRAINT pk_measurement_pair PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.management_review ADD CONSTRAINT pk_management_review PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.maintenance_record ADD CONSTRAINT pk_maintenance_record PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.lov_entry ADD CONSTRAINT pk_lov_entry PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.lot_sample_pair ADD CONSTRAINT pk_lot_sample_pair PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.lot_comparison_study ADD CONSTRAINT pk_lot_comparison_study PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.linearity_study ADD CONSTRAINT pk_linearity_study PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.linearity_measurement ADD CONSTRAINT pk_linearity_measurement PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE read.kpi_snapshot ADD CONSTRAINT pk_kpi_snapshot PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.intermediate_check ADD CONSTRAINT pk_intermediate_check PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.interference_study ADD CONSTRAINT pk_interference_study PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.interference_measurement ADD CONSTRAINT pk_interference_measurement PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.interested_party ADD CONSTRAINT pk_interested_party PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.instrument_reading ADD CONSTRAINT pk_instrument_reading PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.instrument_comparability_study ADD CONSTRAINT pk_instrument_comparability_study PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.file_reference ADD CONSTRAINT pk_file_reference PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.feedback_entry ADD CONSTRAINT pk_feedback_entry PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.escalation_timer ADD CONSTRAINT pk_escalation_timer PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.equipment_item ADD CONSTRAINT pk_equipment_item PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.environmental_reading ADD CONSTRAINT pk_environmental_reading PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE audit.electronic_signature ADD CONSTRAINT pk_electronic_signature PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.document_version ADD CONSTRAINT pk_document_version PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.document_controlled_copy ADD CONSTRAINT pk_document_controlled_copy PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.document_acknowledgement ADD CONSTRAINT pk_document_acknowledgement PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.detection_measurement ADD CONSTRAINT pk_detection_measurement PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.detection_limit_study ADD CONSTRAINT pk_detection_limit_study PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.department ADD CONSTRAINT pk_department PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.controlled_document ADD CONSTRAINT pk_controlled_document PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.context_issue ADD CONSTRAINT pk_context_issue PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.conflict_declaration ADD CONSTRAINT pk_conflict_declaration PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.complaint ADD CONSTRAINT pk_complaint PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.competency_record ADD CONSTRAINT pk_competency_record PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.change_request ADD CONSTRAINT pk_change_request PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.carryover_study ADD CONSTRAINT pk_carryover_study PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.carryover_reading ADD CONSTRAINT pk_carryover_reading PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.capa_action ADD CONSTRAINT pk_capa_action PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.calibration_record ADD CONSTRAINT pk_calibration_record PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.branch ADD CONSTRAINT pk_branch PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.audit_trail_review ADD CONSTRAINT pk_audit_trail_review PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE audit.audit_trail ADD CONSTRAINT pk_audit_trail PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.audit_finding ADD CONSTRAINT pk_audit_finding PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.audit_checklist_item ADD CONSTRAINT pk_audit_checklist_item PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.audit ADD CONSTRAINT pk_audit PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.assessment_result ADD CONSTRAINT pk_assessment_result PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.archive_entry ADD CONSTRAINT pk_archive_entry PRIMARY KEY (tenant_id, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_validation_replicate_tenant_id_study_id ON qams.validation_replicate (tenant_id, study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_user_department_access_user_id ON qams.user_department_access (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_user_branch_access_user_id ON qams.user_branch_access (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_uncertainty_component_tenant_id_budget_id ON qams.uncertainty_component (tenant_id, budget_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_supplier_certificate_tenant_id_supplier_id ON qams.supplier_certificate (tenant_id, supplier_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_review_decision_tenant_id_review_id ON qams.review_decision (tenant_id, review_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_reference_sample_tenant_id_study_id ON qams.reference_sample (tenant_id, study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_rca_record_tenant_id_nc_id ON qams.rca_record (tenant_id, nc_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_pt_plan_item_tenant_id_plan_id ON qams.pt_plan_item (tenant_id, plan_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_precision_measurement_tenant_id_study_id ON qams.precision_measurement (tenant_id, study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_outlier_point_tenant_id_screening_id ON qams.outlier_point (tenant_id, screening_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_objective_progress_tenant_id_objective_id ON qams.objective_progress (tenant_id, objective_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_mitigation_action_tenant_id_risk_id ON qams.mitigation_action (tenant_id, risk_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_measurement_pair_tenant_id_study_id ON qams.measurement_pair (tenant_id, study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_maintenance_record_tenant_id_equipment_id ON qams.maintenance_record (tenant_id, equipment_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_lot_sample_pair_tenant_id_study_id ON qams.lot_sample_pair (tenant_id, study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_linearity_measurement_tenant_id_study_id ON qams.linearity_measurement (tenant_id, study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_intermediate_check_tenant_id_equipment_id ON qams.intermediate_check (tenant_id, equipment_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_interference_measurement_tenant_id_study_id ON qams.interference_measurement (tenant_id, study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_instrument_reading_tenant_id_study_id ON qams.instrument_reading (tenant_id, study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_environmental_reading_tenant_id_point_id ON qams.environmental_reading (tenant_id, point_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_document_version_tenant_id_document_id ON qams.document_version (tenant_id, document_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_detection_measurement_tenant_id_study_id ON qams.detection_measurement (tenant_id, study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_carryover_reading_tenant_id_study_id ON qams.carryover_reading (tenant_id, study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_capa_action_tenant_id_nc_id ON qams.capa_action (tenant_id, nc_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_calibration_record_tenant_id_equipment_id ON qams.calibration_record (tenant_id, equipment_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_audit_finding_tenant_id_audit_id ON qams.audit_finding (tenant_id, audit_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_audit_checklist_item_tenant_id_audit_id ON qams.audit_checklist_item (tenant_id, audit_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    CREATE INDEX ix_assessment_result_tenant_id_competency_id ON qams.assessment_result (tenant_id, competency_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.assessment_result ADD CONSTRAINT fk_assessment_result_competency_record_tenant_id_competency_id FOREIGN KEY (tenant_id, competency_id) REFERENCES qams.competency_record (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.audit_checklist_item ADD CONSTRAINT fk_audit_checklist_item_audit_tenant_id_audit_id FOREIGN KEY (tenant_id, audit_id) REFERENCES qams.audit (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.audit_finding ADD CONSTRAINT fk_audit_finding_audit_tenant_id_audit_id FOREIGN KEY (tenant_id, audit_id) REFERENCES qams.audit (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.calibration_record ADD CONSTRAINT fk_calibration_record_equipment_item_tenant_id_equipment_id FOREIGN KEY (tenant_id, equipment_id) REFERENCES qams.equipment_item (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.capa_action ADD CONSTRAINT fk_capa_action_nonconformance_tenant_id_nc_id FOREIGN KEY (tenant_id, nc_id) REFERENCES qams.nonconformance (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.carryover_reading ADD CONSTRAINT fk_carryover_reading_carryover_study_tenant_id_study_id FOREIGN KEY (tenant_id, study_id) REFERENCES qams.carryover_study (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.department ADD CONSTRAINT fk_department_branch_tenant_id_branch_id FOREIGN KEY (tenant_id, branch_id) REFERENCES qams.branch (tenant_id, id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.detection_measurement ADD CONSTRAINT fk_detection_measurement_detection_limit_study_tenant_id_study FOREIGN KEY (tenant_id, study_id) REFERENCES qams.detection_limit_study (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.document_version ADD CONSTRAINT fk_document_version_controlled_document_tenant_id_document_id FOREIGN KEY (tenant_id, document_id) REFERENCES qams.controlled_document (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.environmental_reading ADD CONSTRAINT fk_environmental_reading_monitoring_point_tenant_id_point_id FOREIGN KEY (tenant_id, point_id) REFERENCES qams.monitoring_point (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.instrument_reading ADD CONSTRAINT fk_instrument_reading_instrument_comparability_study_tenant_id FOREIGN KEY (tenant_id, study_id) REFERENCES qams.instrument_comparability_study (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.interference_measurement ADD CONSTRAINT fk_interference_measurement_interference_study_tenant_id_study FOREIGN KEY (tenant_id, study_id) REFERENCES qams.interference_study (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.intermediate_check ADD CONSTRAINT fk_intermediate_check_equipment_item_tenant_id_equipment_id FOREIGN KEY (tenant_id, equipment_id) REFERENCES qams.equipment_item (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.linearity_measurement ADD CONSTRAINT fk_linearity_measurement_linearity_study_tenant_id_study_id FOREIGN KEY (tenant_id, study_id) REFERENCES qams.linearity_study (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.lot_sample_pair ADD CONSTRAINT fk_lot_sample_pair_lot_comparison_study_tenant_id_study_id FOREIGN KEY (tenant_id, study_id) REFERENCES qams.lot_comparison_study (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.maintenance_record ADD CONSTRAINT fk_maintenance_record_equipment_item_tenant_id_equipment_id FOREIGN KEY (tenant_id, equipment_id) REFERENCES qams.equipment_item (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.measurement_pair ADD CONSTRAINT fk_measurement_pair_method_comparison_study_tenant_id_study_id FOREIGN KEY (tenant_id, study_id) REFERENCES qams.method_comparison_study (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.mitigation_action ADD CONSTRAINT fk_mitigation_action_risk_item_tenant_id_risk_id FOREIGN KEY (tenant_id, risk_id) REFERENCES qams.risk_item (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.objective_progress ADD CONSTRAINT fk_objective_progress_quality_objective_tenant_id_objective_id FOREIGN KEY (tenant_id, objective_id) REFERENCES qams.quality_objective (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.outlier_point ADD CONSTRAINT fk_outlier_point_outlier_screening_tenant_id_screening_id FOREIGN KEY (tenant_id, screening_id) REFERENCES qams.outlier_screening (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.precision_measurement ADD CONSTRAINT fk_precision_measurement_precision_study_tenant_id_study_id FOREIGN KEY (tenant_id, study_id) REFERENCES qams.precision_study (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.pt_plan_item ADD CONSTRAINT fk_pt_plan_item_pt_plan_tenant_id_plan_id FOREIGN KEY (tenant_id, plan_id) REFERENCES qams.pt_plan (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.rca_record ADD CONSTRAINT fk_rca_record_nonconformance_tenant_id_nc_id FOREIGN KEY (tenant_id, nc_id) REFERENCES qams.nonconformance (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.reference_sample ADD CONSTRAINT fk_ref_sample_ri_study_tenant FOREIGN KEY (tenant_id, study_id) REFERENCES qams.reference_interval_study (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.review_decision ADD CONSTRAINT fk_review_decision_management_review_tenant_id_review_id FOREIGN KEY (tenant_id, review_id) REFERENCES qams.management_review (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.role_permission ADD CONSTRAINT fk_role_permission_role_tenant_id_role_id FOREIGN KEY (tenant_id, role_id) REFERENCES qams.role (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.supplier_certificate ADD CONSTRAINT fk_supplier_certificate_supplier_tenant_id_supplier_id FOREIGN KEY (tenant_id, supplier_id) REFERENCES qams.supplier (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.uncertainty_component ADD CONSTRAINT fk_unc_component_unc_budget_tenant FOREIGN KEY (tenant_id, budget_id) REFERENCES qams.uncertainty_budget (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    ALTER TABLE qams.validation_replicate ADD CONSTRAINT fk_validation_replicate_validation_study_tenant_id_study_id FOREIGN KEY (tenant_id, study_id) REFERENCES qams.validation_study (tenant_id, id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731210953_Hardening5_CompositeKeys') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260731210953_Hardening5_CompositeKeys', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731223800_Hardening6_DeferrableTenantFks') THEN
    ALTER TABLE qams.outbox_event DROP CONSTRAINT fk_outbox_event_tenant;
    ALTER TABLE qams.outbox_event ADD CONSTRAINT fk_outbox_event_tenant FOREIGN KEY (tenant_id)
      REFERENCES saas.tenant (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
    ALTER TABLE qams.ref_counter DROP CONSTRAINT fk_ref_counter_tenant;
    ALTER TABLE qams.ref_counter ADD CONSTRAINT fk_ref_counter_tenant FOREIGN KEY (tenant_id)
      REFERENCES saas.tenant (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
    ALTER TABLE read.kpi_snapshot DROP CONSTRAINT fk_kpi_snapshot_tenant;
    ALTER TABLE read.kpi_snapshot ADD CONSTRAINT fk_kpi_snapshot_tenant FOREIGN KEY (tenant_id)
      REFERENCES saas.tenant (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
    ALTER TABLE qams.branch DROP CONSTRAINT fk_branch_tenant;
    ALTER TABLE qams.branch ADD CONSTRAINT fk_branch_tenant FOREIGN KEY (tenant_id)
      REFERENCES saas.tenant (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
    ALTER TABLE qams.user_account DROP CONSTRAINT fk_user_account_tenant;
    ALTER TABLE qams.user_account ADD CONSTRAINT fk_user_account_tenant FOREIGN KEY (tenant_id)
      REFERENCES saas.tenant (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260731223800_Hardening6_DeferrableTenantFks') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260731223800_Hardening6_DeferrableTenantFks', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801131521_QualityHealthProfile') THEN
    CREATE TABLE qams.quality_health_profile (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_quality_health_profile PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801131521_QualityHealthProfile') THEN
    CREATE TABLE qams.quality_health_weight (
        tenant_id uuid NOT NULL,
        id integer GENERATED BY DEFAULT AS IDENTITY,
        category character varying(30) NOT NULL,
        weight integer NOT NULL,
        profile_id uuid NOT NULL,
        CONSTRAINT pk_quality_health_weight PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_quality_health_weight_profile FOREIGN KEY (tenant_id, profile_id) REFERENCES qams.quality_health_profile (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801131521_QualityHealthProfile') THEN
    CREATE UNIQUE INDEX ux_quality_health_profile_tenant ON qams.quality_health_profile (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801131521_QualityHealthProfile') THEN
    CREATE UNIQUE INDEX ux_quality_health_weight_category ON qams.quality_health_weight (tenant_id, profile_id, category);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801131521_QualityHealthProfile') THEN
    ALTER TABLE qams.quality_health_profile ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.quality_health_profile FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.quality_health_profile;
    CREATE POLICY tenant_isolation ON qams.quality_health_profile
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801131521_QualityHealthProfile') THEN
    ALTER TABLE qams.quality_health_weight ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.quality_health_weight FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.quality_health_weight;
    CREATE POLICY tenant_isolation ON qams.quality_health_weight
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801131521_QualityHealthProfile') THEN
    ALTER TABLE qams.quality_health_weight
      ADD CONSTRAINT ck_quality_health_weight_category_domain
      CHECK (category IN ('DocumentControl','NonconformanceCapa','Complaints',
                          'InternalAudit','Equipment','Competency',
                          'ProficiencyTesting','SupplierQuality','Risk')) NOT VALID;
    ALTER TABLE qams.quality_health_weight
      VALIDATE CONSTRAINT ck_quality_health_weight_category_domain;

    ALTER TABLE qams.quality_health_weight
      ADD CONSTRAINT ck_quality_health_weight_range
      CHECK (weight >= 0 AND weight <= 100) NOT VALID;
    ALTER TABLE qams.quality_health_weight
      VALIDATE CONSTRAINT ck_quality_health_weight_range;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801131521_QualityHealthProfile') THEN
    SET LOCAL app.bypass_rls = 'on';

    INSERT INTO qams.role_permission (tenant_id, role_id, permission_key)
    SELECT r.tenant_id, r.id, 'reports.manage'
    FROM qams.role r
    WHERE r.is_system = true
      AND r.name IN ('Tenant Administrator', 'Quality Manager')
      AND NOT EXISTS (
        SELECT 1 FROM qams.role_permission rp
        WHERE rp.tenant_id = r.tenant_id
          AND rp.role_id = r.id
          AND rp.permission_key = 'reports.manage');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801131521_QualityHealthProfile') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260801131521_QualityHealthProfile', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801183201_ReviewAgendaLinkParticipants') THEN
    ALTER TABLE qams.management_review ADD agenda text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801183201_ReviewAgendaLinkParticipants') THEN
    ALTER TABLE qams.management_review ADD meeting_link character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801183201_ReviewAgendaLinkParticipants') THEN
    CREATE TABLE qams.review_participant (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        user_id uuid NOT NULL,
        review_id uuid NOT NULL,
        CONSTRAINT pk_review_participant PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_review_participant_management_review_tenant_id_review_id FOREIGN KEY (tenant_id, review_id) REFERENCES qams.management_review (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801183201_ReviewAgendaLinkParticipants') THEN
    CREATE UNIQUE INDEX ux_review_participant_user ON qams.review_participant (tenant_id, review_id, user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801183201_ReviewAgendaLinkParticipants') THEN
    ALTER TABLE qams.review_participant ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.review_participant FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.review_participant;
    CREATE POLICY tenant_isolation ON qams.review_participant
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801183201_ReviewAgendaLinkParticipants') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260801183201_ReviewAgendaLinkParticipants', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801194628_MaintenanceCertificate') THEN
    ALTER TABLE qams.maintenance_record ADD certificate_file_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260801194628_MaintenanceCertificate') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260801194628_MaintenanceCertificate', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260808073533_AddNcReopenReason') THEN
    ALTER TABLE qams.nonconformance ADD reopen_reason text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260808073533_AddNcReopenReason') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260808073533_AddNcReopenReason', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260808152142_AddTenantMailSettings') THEN
    CREATE TABLE qams.tenant_mail_settings (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        from_name character varying(150) NOT NULL,
        from_address character varying(320) NOT NULL,
        reply_to character varying(320),
        enabled boolean NOT NULL,
        brand_color character varying(9),
        footer_note character varying(500),
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_tenant_mail_settings PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260808152142_AddTenantMailSettings') THEN
    CREATE UNIQUE INDEX ux_tenant_mail_settings_tenant ON qams.tenant_mail_settings (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260808152142_AddTenantMailSettings') THEN
    ALTER TABLE qams.tenant_mail_settings ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.tenant_mail_settings FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.tenant_mail_settings;
    CREATE POLICY tenant_isolation ON qams.tenant_mail_settings
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260808152142_AddTenantMailSettings') THEN
    ALTER TABLE qams.tenant_mail_settings
      ADD CONSTRAINT ck_tenant_mail_settings_brand_color
      CHECK (brand_color IS NULL OR brand_color ~ '^#[0-9A-Fa-f]{6}$') NOT VALID;
    ALTER TABLE qams.tenant_mail_settings VALIDATE CONSTRAINT ck_tenant_mail_settings_brand_color;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260808152142_AddTenantMailSettings') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260808152142_AddTenantMailSettings', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825085434_AddIncidentModule') THEN
    CREATE TABLE qams.incident (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        branch_id uuid,
        department_id uuid,
        incident_ref character varying(30) NOT NULL,
        title character varying(300) NOT NULL,
        description text NOT NULL,
        category character varying(30) NOT NULL,
        location character varying(200),
        occurred_at_utc timestamp with time zone NOT NULL,
        channel character varying(20) NOT NULL,
        harm_grade character varying(20) NOT NULL,
        is_sentinel boolean NOT NULL,
        sentinel_declared_at_utc timestamp with time zone,
        status character varying(30) NOT NULL,
        reported_by uuid,
        is_anonymous boolean NOT NULL,
        anonymous_reference_hash character varying(64),
        assigned_to uuid,
        investigator_id uuid,
        investigation_summary text,
        rejection_reason character varying(1000),
        closure_summary text,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_incident PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825085434_AddIncidentModule') THEN
    CREATE TABLE qams.incident_contributing_factor (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        category character varying(20) NOT NULL,
        description text NOT NULL,
        incident_id uuid NOT NULL,
        CONSTRAINT pk_incident_contributing_factor PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_incident_contributing_factor_incident_tenant_id_incident_id FOREIGN KEY (tenant_id, incident_id) REFERENCES qams.incident (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825085434_AddIncidentModule') THEN
    CREATE TABLE qams.incident_timeline_entry (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        occurred_at_utc timestamp with time zone NOT NULL,
        note text NOT NULL,
        recorded_by uuid NOT NULL,
        incident_id uuid NOT NULL,
        CONSTRAINT pk_incident_timeline_entry PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_incident_timeline_entry_incident_tenant_id_incident_id FOREIGN KEY (tenant_id, incident_id) REFERENCES qams.incident (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825085434_AddIncidentModule') THEN
    CREATE INDEX ix_incident_tenant_id_anonymous_reference_hash ON qams.incident (tenant_id, anonymous_reference_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825085434_AddIncidentModule') THEN
    CREATE UNIQUE INDEX ix_incident_tenant_id_incident_ref ON qams.incident (tenant_id, incident_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825085434_AddIncidentModule') THEN
    CREATE INDEX ix_incident_tenant_id_status ON qams.incident (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825085434_AddIncidentModule') THEN
    CREATE INDEX ix_incident_contributing_factor_tenant_id_incident_id ON qams.incident_contributing_factor (tenant_id, incident_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825085434_AddIncidentModule') THEN
    CREATE INDEX ix_incident_timeline_entry_tenant_id_incident_id ON qams.incident_timeline_entry (tenant_id, incident_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825085434_AddIncidentModule') THEN
    ALTER TABLE qams.incident ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.incident FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.incident;
    CREATE POLICY tenant_isolation ON qams.incident
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825085434_AddIncidentModule') THEN
    ALTER TABLE qams.incident_contributing_factor ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.incident_contributing_factor FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.incident_contributing_factor;
    CREATE POLICY tenant_isolation ON qams.incident_contributing_factor
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825085434_AddIncidentModule') THEN
    ALTER TABLE qams.incident_timeline_entry ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.incident_timeline_entry FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.incident_timeline_entry;
    CREATE POLICY tenant_isolation ON qams.incident_timeline_entry
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825085434_AddIncidentModule') THEN
    ALTER TABLE qams.incident ADD CONSTRAINT ck_incident_status_domain
      CHECK (status IN ('Reported','Triaged','UnderInvestigation','PendingReview','Closed','Rejected')) NOT VALID;
    ALTER TABLE qams.incident VALIDATE CONSTRAINT ck_incident_status_domain;

    ALTER TABLE qams.incident ADD CONSTRAINT ck_incident_category_domain
      CHECK (category IN ('Medication','Fall','Procedural','Transfusion','Device','Laboratory','Security','Documentation','Other')) NOT VALID;
    ALTER TABLE qams.incident VALIDATE CONSTRAINT ck_incident_category_domain;

    ALTER TABLE qams.incident ADD CONSTRAINT ck_incident_harm_grade_domain
      CHECK (harm_grade IN ('NearMiss','NoHarm','Minor','Moderate','Severe','Death')) NOT VALID;
    ALTER TABLE qams.incident VALIDATE CONSTRAINT ck_incident_harm_grade_domain;

    ALTER TABLE qams.incident ADD CONSTRAINT ck_incident_channel_domain
      CHECK (channel IN ('Web','Mobile','Kiosk','Phone','Paper')) NOT VALID;
    ALTER TABLE qams.incident VALIDATE CONSTRAINT ck_incident_channel_domain;

    ALTER TABLE qams.incident ADD CONSTRAINT ck_incident_anonymous_reference_hash
      CHECK (anonymous_reference_hash IS NULL OR anonymous_reference_hash ~ '^[0-9a-f]{64}$') NOT VALID;
    ALTER TABLE qams.incident VALIDATE CONSTRAINT ck_incident_anonymous_reference_hash;

    ALTER TABLE qams.incident_contributing_factor ADD CONSTRAINT ck_incident_contributing_factor_category_domain
      CHECK (category IN ('People','Process','Equipment','Environment','Materials','Management','Other')) NOT VALID;
    ALTER TABLE qams.incident_contributing_factor VALIDATE CONSTRAINT ck_incident_contributing_factor_category_domain;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825085434_AddIncidentModule') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260825085434_AddIncidentModule', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825110722_IncidentCapaConvergence') THEN
    ALTER TABLE qams.incident ADD corrective_action_nc_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825110722_IncidentCapaConvergence') THEN
    ALTER TABLE qams.nonconformance DROP CONSTRAINT IF EXISTS ck_nonconformance_source_type_domain;
    ALTER TABLE qams.nonconformance ADD CONSTRAINT ck_nonconformance_source_type_domain
      CHECK (source_type IN ('Internal','Complaint','Audit','Supplier','ProficiencyTest','Incident')) NOT VALID;
    ALTER TABLE qams.nonconformance VALIDATE CONSTRAINT ck_nonconformance_source_type_domain;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825110722_IncidentCapaConvergence') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260825110722_IncidentCapaConvergence', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825121213_AddQualityIndicators') THEN
    CREATE TABLE qams.quality_indicator (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        indicator_ref character varying(30) NOT NULL,
        code character varying(50) NOT NULL,
        name character varying(300) NOT NULL,
        description character varying(2000),
        numerator text NOT NULL,
        denominator text NOT NULL,
        inclusions character varying(2000),
        exclusions character varying(2000),
        data_source character varying(1000),
        frequency character varying(20) NOT NULL,
        unit character varying(50) NOT NULL,
        rate_factor numeric(18,4) NOT NULL,
        direction character varying(20) NOT NULL,
        target numeric(18,4),
        warning_threshold numeric(18,4),
        action_threshold numeric(18,4),
        status character varying(20) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_quality_indicator PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825121213_AddQualityIndicators') THEN
    CREATE TABLE qams.indicator_measurement (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        period date NOT NULL,
        numerator numeric(18,4) NOT NULL,
        denominator numeric(18,4) NOT NULL,
        value numeric(18,4) NOT NULL,
        status character varying(20) NOT NULL,
        entered_by uuid NOT NULL,
        recorded_at_utc timestamp with time zone NOT NULL,
        note text,
        indicator_id uuid NOT NULL,
        CONSTRAINT pk_indicator_measurement PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_indicator_measurement_quality_indicator_tenant_id_indicator FOREIGN KEY (tenant_id, indicator_id) REFERENCES qams.quality_indicator (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825121213_AddQualityIndicators') THEN
    CREATE UNIQUE INDEX ix_indicator_measurement_tenant_id_indicator_id_period ON qams.indicator_measurement (tenant_id, indicator_id, period);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825121213_AddQualityIndicators') THEN
    CREATE UNIQUE INDEX ix_quality_indicator_tenant_id_code ON qams.quality_indicator (tenant_id, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825121213_AddQualityIndicators') THEN
    CREATE INDEX ix_quality_indicator_tenant_id_status ON qams.quality_indicator (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825121213_AddQualityIndicators') THEN
    ALTER TABLE qams.quality_indicator ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.quality_indicator FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.quality_indicator;
    CREATE POLICY tenant_isolation ON qams.quality_indicator
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825121213_AddQualityIndicators') THEN
    ALTER TABLE qams.indicator_measurement ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.indicator_measurement FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.indicator_measurement;
    CREATE POLICY tenant_isolation ON qams.indicator_measurement
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825121213_AddQualityIndicators') THEN
    ALTER TABLE qams.quality_indicator ADD CONSTRAINT ck_quality_indicator_frequency_domain
      CHECK (frequency IN ('Weekly','Monthly','Quarterly','Annually')) NOT VALID;
    ALTER TABLE qams.quality_indicator VALIDATE CONSTRAINT ck_quality_indicator_frequency_domain;

    ALTER TABLE qams.quality_indicator ADD CONSTRAINT ck_quality_indicator_direction_domain
      CHECK (direction IN ('HigherIsBetter','LowerIsBetter')) NOT VALID;
    ALTER TABLE qams.quality_indicator VALIDATE CONSTRAINT ck_quality_indicator_direction_domain;

    ALTER TABLE qams.quality_indicator ADD CONSTRAINT ck_quality_indicator_status_domain
      CHECK (status IN ('Active','Retired')) NOT VALID;
    ALTER TABLE qams.quality_indicator VALIDATE CONSTRAINT ck_quality_indicator_status_domain;

    ALTER TABLE qams.indicator_measurement ADD CONSTRAINT ck_indicator_measurement_status_domain
      CHECK (status IN ('InTarget','Warning','Breached')) NOT VALID;
    ALTER TABLE qams.indicator_measurement VALIDATE CONSTRAINT ck_indicator_measurement_status_domain;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825121213_AddQualityIndicators') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260825121213_AddQualityIndicators', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825141218_DocumentReadAndUnderstand') THEN
    ALTER TABLE qams.controlled_document ADD audience_scope character varying(20) NOT NULL DEFAULT 'AllStaff';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825141218_DocumentReadAndUnderstand') THEN
    ALTER TABLE qams.controlled_document ADD requires_acknowledgement boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825141218_DocumentReadAndUnderstand') THEN
    CREATE TABLE qams.document_audience_department (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        department_id uuid NOT NULL,
        document_id uuid NOT NULL,
        CONSTRAINT pk_document_audience_department PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_document_audience_department_controlled_document_tenant_id_ FOREIGN KEY (tenant_id, document_id) REFERENCES qams.controlled_document (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825141218_DocumentReadAndUnderstand') THEN
    CREATE INDEX ix_document_audience_department_tenant_id_document_id ON qams.document_audience_department (tenant_id, document_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825141218_DocumentReadAndUnderstand') THEN
    ALTER TABLE qams.document_audience_department ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.document_audience_department FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.document_audience_department;
    CREATE POLICY tenant_isolation ON qams.document_audience_department
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825141218_DocumentReadAndUnderstand') THEN
    ALTER TABLE qams.controlled_document ADD CONSTRAINT ck_controlled_document_audience_scope_domain
      CHECK (audience_scope IN ('AllStaff','ByDepartment')) NOT VALID;
    ALTER TABLE qams.controlled_document VALIDATE CONSTRAINT ck_controlled_document_audience_scope_domain;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825141218_DocumentReadAndUnderstand') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260825141218_DocumentReadAndUnderstand', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825163629_AddAccreditationEngine') THEN
    CREATE TABLE qams.evidence_link (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        standard_set_id uuid NOT NULL,
        element_id uuid NOT NULL,
        source_type character varying(20) NOT NULL,
        source_id uuid NOT NULL,
        source_ref character varying(200) NOT NULL,
        description character varying(1000),
        linked_by uuid NOT NULL,
        linked_at_utc timestamp with time zone NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_evidence_link PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825163629_AddAccreditationEngine') THEN
    CREATE TABLE qams.standard_set (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        framework character varying(20) NOT NULL,
        name character varying(200) NOT NULL,
        version character varying(40) NOT NULL,
        status character varying(20) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_standard_set PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825163629_AddAccreditationEngine') THEN
    CREATE TABLE qams.standard_element (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        chapter_code character varying(40) NOT NULL,
        chapter_title character varying(300) NOT NULL,
        standard_code character varying(40) NOT NULL,
        element_code character varying(40) NOT NULL,
        text text NOT NULL,
        weight integer NOT NULL,
        compliance_status character varying(20) NOT NULL,
        assessment_note text,
        assessed_by uuid,
        assessed_at_utc timestamp with time zone,
        standard_set_id uuid NOT NULL,
        CONSTRAINT pk_standard_element PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_standard_element_standard_set_tenant_id_standard_set_id FOREIGN KEY (tenant_id, standard_set_id) REFERENCES qams.standard_set (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825163629_AddAccreditationEngine') THEN
    CREATE INDEX ix_evidence_link_tenant_id_element_id ON qams.evidence_link (tenant_id, element_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825163629_AddAccreditationEngine') THEN
    CREATE INDEX ix_evidence_link_tenant_id_standard_set_id ON qams.evidence_link (tenant_id, standard_set_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825163629_AddAccreditationEngine') THEN
    CREATE UNIQUE INDEX ux_standard_element_set_code ON qams.standard_element (tenant_id, standard_set_id, element_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825163629_AddAccreditationEngine') THEN
    CREATE INDEX ix_standard_set_tenant_id_status ON qams.standard_set (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825163629_AddAccreditationEngine') THEN
    ALTER TABLE qams.standard_set ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.standard_set FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.standard_set;
    CREATE POLICY tenant_isolation ON qams.standard_set
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825163629_AddAccreditationEngine') THEN
    ALTER TABLE qams.standard_element ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.standard_element FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.standard_element;
    CREATE POLICY tenant_isolation ON qams.standard_element
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825163629_AddAccreditationEngine') THEN
    ALTER TABLE qams.evidence_link ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.evidence_link FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.evidence_link;
    CREATE POLICY tenant_isolation ON qams.evidence_link
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825163629_AddAccreditationEngine') THEN
    ALTER TABLE qams.standard_set ADD CONSTRAINT ck_standard_set_framework_domain
      CHECK (framework IN ('GAHAR','JCI','ISO9001','ISO15189','Other')) NOT VALID;
    ALTER TABLE qams.standard_set VALIDATE CONSTRAINT ck_standard_set_framework_domain;

    ALTER TABLE qams.standard_set ADD CONSTRAINT ck_standard_set_status_domain
      CHECK (status IN ('Draft','Active','Archived')) NOT VALID;
    ALTER TABLE qams.standard_set VALIDATE CONSTRAINT ck_standard_set_status_domain;

    ALTER TABLE qams.standard_element ADD CONSTRAINT ck_standard_element_compliance_status_domain
      CHECK (compliance_status IN ('NotAssessed','Compliant','PartiallyCompliant','NonCompliant','NotApplicable')) NOT VALID;
    ALTER TABLE qams.standard_element VALIDATE CONSTRAINT ck_standard_element_compliance_status_domain;

    ALTER TABLE qams.evidence_link ADD CONSTRAINT ck_evidence_link_source_type_domain
      CHECK (source_type IN ('Document','Incident','Nonconformance','Audit','Indicator','Training','Committee','Other')) NOT VALID;
    ALTER TABLE qams.evidence_link VALIDATE CONSTRAINT ck_evidence_link_source_type_domain;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825163629_AddAccreditationEngine') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260825163629_AddAccreditationEngine', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825165947_AddAuditProgram') THEN
    CREATE TABLE qams.audit_program (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        year integer NOT NULL,
        title character varying(200) NOT NULL,
        status character varying(20) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_audit_program PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825165947_AddAuditProgram') THEN
    CREATE TABLE qams.planned_audit (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        scope_area character varying(200) NOT NULL,
        department_id uuid,
        standard_chapter character varying(120),
        priority character varying(20) NOT NULL,
        planned_quarter integer NOT NULL,
        status character varying(20) NOT NULL,
        scheduled_audit_id uuid,
        completed_on date,
        audit_program_id uuid NOT NULL,
        CONSTRAINT pk_planned_audit PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_planned_audit_audit_program_tenant_id_audit_program_id FOREIGN KEY (tenant_id, audit_program_id) REFERENCES qams.audit_program (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825165947_AddAuditProgram') THEN
    CREATE INDEX ix_audit_program_tenant_id_year ON qams.audit_program (tenant_id, year);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825165947_AddAuditProgram') THEN
    CREATE INDEX ix_planned_audit_tenant_id_audit_program_id ON qams.planned_audit (tenant_id, audit_program_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825165947_AddAuditProgram') THEN
    ALTER TABLE qams.audit_program ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.audit_program FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.audit_program;
    CREATE POLICY tenant_isolation ON qams.audit_program
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825165947_AddAuditProgram') THEN
    ALTER TABLE qams.planned_audit ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.planned_audit FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.planned_audit;
    CREATE POLICY tenant_isolation ON qams.planned_audit
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825165947_AddAuditProgram') THEN
    ALTER TABLE qams.audit_program ADD CONSTRAINT ck_audit_program_status_domain
      CHECK (status IN ('Draft','Active','Closed')) NOT VALID;
    ALTER TABLE qams.audit_program VALIDATE CONSTRAINT ck_audit_program_status_domain;

    ALTER TABLE qams.planned_audit ADD CONSTRAINT ck_planned_audit_priority_domain
      CHECK (priority IN ('Low','Medium','High')) NOT VALID;
    ALTER TABLE qams.planned_audit VALIDATE CONSTRAINT ck_planned_audit_priority_domain;

    ALTER TABLE qams.planned_audit ADD CONSTRAINT ck_planned_audit_status_domain
      CHECK (status IN ('Planned','Scheduled','Completed')) NOT VALID;
    ALTER TABLE qams.planned_audit VALIDATE CONSTRAINT ck_planned_audit_status_domain;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825165947_AddAuditProgram') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260825165947_AddAuditProgram', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825172624_AddFmeaStudy') THEN
    CREATE TABLE qams.fmea_study (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        branch_id uuid,
        department_id uuid,
        fmea_ref character varying(30) NOT NULL,
        title character varying(200) NOT NULL,
        process_name character varying(200) NOT NULL,
        type character varying(20) NOT NULL,
        status character varying(20) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_fmea_study PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825172624_AddFmeaStudy') THEN
    CREATE TABLE qams.fmea_failure_mode (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        process_step character varying(200) NOT NULL,
        failure_mode_text character varying(500) NOT NULL,
        effect character varying(1000) NOT NULL,
        cause character varying(1000) NOT NULL,
        severity integer NOT NULL,
        occurrence integer NOT NULL,
        detection integer NOT NULL,
        rpn integer NOT NULL,
        recommended_action character varying(2000),
        action_owner_id uuid,
        residual_severity integer,
        residual_occurrence integer,
        residual_detection integer,
        residual_rpn integer,
        status character varying(20) NOT NULL,
        fmea_study_id uuid NOT NULL,
        CONSTRAINT pk_fmea_failure_mode PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_fmea_failure_mode_fmea_study_tenant_id_fmea_study_id FOREIGN KEY (tenant_id, fmea_study_id) REFERENCES qams.fmea_study (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825172624_AddFmeaStudy') THEN
    CREATE INDEX ix_fmea_failure_mode_tenant_id_fmea_study_id ON qams.fmea_failure_mode (tenant_id, fmea_study_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825172624_AddFmeaStudy') THEN
    CREATE UNIQUE INDEX ix_fmea_study_tenant_id_fmea_ref ON qams.fmea_study (tenant_id, fmea_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825172624_AddFmeaStudy') THEN
    CREATE INDEX ix_fmea_study_tenant_id_status ON qams.fmea_study (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825172624_AddFmeaStudy') THEN
    ALTER TABLE qams.fmea_study ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.fmea_study FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.fmea_study;
    CREATE POLICY tenant_isolation ON qams.fmea_study
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825172624_AddFmeaStudy') THEN
    ALTER TABLE qams.fmea_failure_mode ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.fmea_failure_mode FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.fmea_failure_mode;
    CREATE POLICY tenant_isolation ON qams.fmea_failure_mode
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825172624_AddFmeaStudy') THEN
    ALTER TABLE qams.fmea_study ADD CONSTRAINT ck_fmea_study_type_domain
      CHECK (type IN ('Fmea','Hfmea')) NOT VALID;
    ALTER TABLE qams.fmea_study VALIDATE CONSTRAINT ck_fmea_study_type_domain;

    ALTER TABLE qams.fmea_study ADD CONSTRAINT ck_fmea_study_status_domain
      CHECK (status IN ('Draft','Active','Closed')) NOT VALID;
    ALTER TABLE qams.fmea_study VALIDATE CONSTRAINT ck_fmea_study_status_domain;

    ALTER TABLE qams.fmea_failure_mode ADD CONSTRAINT ck_fmea_failure_mode_status_domain
      CHECK (status IN ('Open','Actioned')) NOT VALID;
    ALTER TABLE qams.fmea_failure_mode VALIDATE CONSTRAINT ck_fmea_failure_mode_status_domain;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825172624_AddFmeaStudy') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260825172624_AddFmeaStudy', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    CREATE TABLE qams.committee (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        name character varying(200) NOT NULL,
        terms_of_reference text NOT NULL,
        frequency character varying(20) NOT NULL,
        quorum_size integer NOT NULL,
        status character varying(20) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_committee PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    CREATE TABLE qams.meeting (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        committee_id uuid NOT NULL,
        meeting_ref character varying(30) NOT NULL,
        scheduled_at_utc timestamp with time zone NOT NULL,
        status character varying(20) NOT NULL,
        minutes text,
        minutes_approved_by uuid,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_meeting PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    CREATE TABLE qams.committee_member (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        user_id uuid NOT NULL,
        role_title character varying(100) NOT NULL,
        committee_id uuid NOT NULL,
        CONSTRAINT pk_committee_member PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_committee_member_committee_tenant_id_committee_id FOREIGN KEY (tenant_id, committee_id) REFERENCES qams.committee (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    CREATE TABLE qams.meeting_agenda_item (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        title character varying(300) NOT NULL,
        detail character varying(2000),
        source_ref character varying(120),
        carried_forward boolean NOT NULL,
        meeting_id uuid NOT NULL,
        CONSTRAINT pk_meeting_agenda_item PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_meeting_agenda_item_meeting_tenant_id_meeting_id FOREIGN KEY (tenant_id, meeting_id) REFERENCES qams.meeting (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    CREATE TABLE qams.meeting_attendance (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        user_id uuid NOT NULL,
        present boolean NOT NULL,
        meeting_id uuid NOT NULL,
        CONSTRAINT pk_meeting_attendance PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_meeting_attendance_meeting_tenant_id_meeting_id FOREIGN KEY (tenant_id, meeting_id) REFERENCES qams.meeting (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    CREATE TABLE qams.meeting_decision (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        description character varying(2000) NOT NULL,
        owner_id uuid,
        due_date date,
        status character varying(20) NOT NULL,
        closure_note character varying(2000),
        meeting_id uuid NOT NULL,
        CONSTRAINT pk_meeting_decision PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_meeting_decision_meeting_tenant_id_meeting_id FOREIGN KEY (tenant_id, meeting_id) REFERENCES qams.meeting (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    CREATE INDEX ix_committee_tenant_id_status ON qams.committee (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    CREATE INDEX ix_committee_member_tenant_id_committee_id ON qams.committee_member (tenant_id, committee_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    CREATE INDEX ix_meeting_tenant_id_committee_id ON qams.meeting (tenant_id, committee_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    CREATE UNIQUE INDEX ix_meeting_tenant_id_meeting_ref ON qams.meeting (tenant_id, meeting_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    CREATE INDEX ix_meeting_agenda_item_tenant_id_meeting_id ON qams.meeting_agenda_item (tenant_id, meeting_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    CREATE INDEX ix_meeting_attendance_tenant_id_meeting_id ON qams.meeting_attendance (tenant_id, meeting_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    CREATE INDEX ix_meeting_decision_tenant_id_meeting_id ON qams.meeting_decision (tenant_id, meeting_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    ALTER TABLE qams.committee ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.committee FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.committee;
    CREATE POLICY tenant_isolation ON qams.committee
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    ALTER TABLE qams.committee_member ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.committee_member FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.committee_member;
    CREATE POLICY tenant_isolation ON qams.committee_member
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    ALTER TABLE qams.meeting ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.meeting FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.meeting;
    CREATE POLICY tenant_isolation ON qams.meeting
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    ALTER TABLE qams.meeting_agenda_item ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.meeting_agenda_item FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.meeting_agenda_item;
    CREATE POLICY tenant_isolation ON qams.meeting_agenda_item
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    ALTER TABLE qams.meeting_attendance ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.meeting_attendance FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.meeting_attendance;
    CREATE POLICY tenant_isolation ON qams.meeting_attendance
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    ALTER TABLE qams.meeting_decision ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.meeting_decision FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.meeting_decision;
    CREATE POLICY tenant_isolation ON qams.meeting_decision
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    ALTER TABLE qams.committee ADD CONSTRAINT ck_committee_frequency_domain
      CHECK (frequency IN ('Weekly','Monthly','Quarterly','Biannual','Annual','AdHoc')) NOT VALID;
    ALTER TABLE qams.committee VALIDATE CONSTRAINT ck_committee_frequency_domain;

    ALTER TABLE qams.committee ADD CONSTRAINT ck_committee_status_domain
      CHECK (status IN ('Active','Disbanded')) NOT VALID;
    ALTER TABLE qams.committee VALIDATE CONSTRAINT ck_committee_status_domain;

    ALTER TABLE qams.meeting ADD CONSTRAINT ck_meeting_status_domain
      CHECK (status IN ('Scheduled','Held','MinutesApproved','Cancelled')) NOT VALID;
    ALTER TABLE qams.meeting VALIDATE CONSTRAINT ck_meeting_status_domain;

    ALTER TABLE qams.meeting_decision ADD CONSTRAINT ck_meeting_decision_status_domain
      CHECK (status IN ('Open','Closed')) NOT VALID;
    ALTER TABLE qams.meeting_decision VALIDATE CONSTRAINT ck_meeting_decision_status_domain;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825174944_AddCommittees') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260825174944_AddCommittees', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825181300_AddSatisfactionSurveys') THEN
    CREATE TABLE qams.satisfaction_survey (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        title character varying(200) NOT NULL,
        description character varying(2000),
        status character varying(20) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_satisfaction_survey PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825181300_AddSatisfactionSurveys') THEN
    CREATE TABLE qams.survey_response (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        survey_id uuid NOT NULL,
        department_id uuid,
        service_line character varying(150),
        submitted_at_utc timestamp with time zone NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_survey_response PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825181300_AddSatisfactionSurveys') THEN
    CREATE TABLE qams.survey_question (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        text character varying(500) NOT NULL,
        domain character varying(100) NOT NULL,
        display_order integer NOT NULL,
        survey_id uuid NOT NULL,
        CONSTRAINT pk_survey_question PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_survey_question_satisfaction_survey_tenant_id_survey_id FOREIGN KEY (tenant_id, survey_id) REFERENCES qams.satisfaction_survey (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825181300_AddSatisfactionSurveys') THEN
    CREATE TABLE qams.survey_answer (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        question_id uuid NOT NULL,
        score integer NOT NULL,
        survey_response_id uuid NOT NULL,
        CONSTRAINT pk_survey_answer PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_survey_answer_survey_response_tenant_id_survey_response_id FOREIGN KEY (tenant_id, survey_response_id) REFERENCES qams.survey_response (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825181300_AddSatisfactionSurveys') THEN
    CREATE INDEX ix_satisfaction_survey_tenant_id_status ON qams.satisfaction_survey (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825181300_AddSatisfactionSurveys') THEN
    CREATE INDEX ix_survey_answer_tenant_id_survey_response_id ON qams.survey_answer (tenant_id, survey_response_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825181300_AddSatisfactionSurveys') THEN
    CREATE INDEX ix_survey_question_tenant_id_survey_id ON qams.survey_question (tenant_id, survey_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825181300_AddSatisfactionSurveys') THEN
    CREATE INDEX ix_survey_response_tenant_id_survey_id ON qams.survey_response (tenant_id, survey_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825181300_AddSatisfactionSurveys') THEN
    ALTER TABLE qams.satisfaction_survey ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.satisfaction_survey FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.satisfaction_survey;
    CREATE POLICY tenant_isolation ON qams.satisfaction_survey
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825181300_AddSatisfactionSurveys') THEN
    ALTER TABLE qams.survey_question ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.survey_question FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.survey_question;
    CREATE POLICY tenant_isolation ON qams.survey_question
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825181300_AddSatisfactionSurveys') THEN
    ALTER TABLE qams.survey_response ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.survey_response FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.survey_response;
    CREATE POLICY tenant_isolation ON qams.survey_response
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825181300_AddSatisfactionSurveys') THEN
    ALTER TABLE qams.survey_answer ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.survey_answer FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.survey_answer;
    CREATE POLICY tenant_isolation ON qams.survey_answer
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825181300_AddSatisfactionSurveys') THEN
    ALTER TABLE qams.satisfaction_survey ADD CONSTRAINT ck_satisfaction_survey_status_domain
      CHECK (status IN ('Draft','Open','Closed')) NOT VALID;
    ALTER TABLE qams.satisfaction_survey VALIDATE CONSTRAINT ck_satisfaction_survey_status_domain;

    ALTER TABLE qams.survey_answer ADD CONSTRAINT ck_survey_answer_score_range
      CHECK (score BETWEEN 1 AND 5) NOT VALID;
    ALTER TABLE qams.survey_answer VALIDATE CONSTRAINT ck_survey_answer_score_range;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825181300_AddSatisfactionSurveys') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260825181300_AddSatisfactionSurveys', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825223639_AddIntegrationHub') THEN
    CREATE TABLE qams.integration_endpoint (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        name character varying(150) NOT NULL,
        system character varying(20) NOT NULL,
        protocol character varying(20) NOT NULL,
        status character varying(20) NOT NULL,
        last_message_at_utc timestamp with time zone,
        last_error_at_utc timestamp with time zone,
        consecutive_failures integer NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_integration_endpoint PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825223639_AddIntegrationHub') THEN
    CREATE TABLE qams.integration_message (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        endpoint_id uuid NOT NULL,
        dedup_key character varying(200) NOT NULL,
        message_type character varying(40) NOT NULL,
        raw_payload text NOT NULL,
        status character varying(20) NOT NULL,
        error_detail text,
        received_at_utc timestamp with time zone NOT NULL,
        processed_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_integration_message PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825223639_AddIntegrationHub') THEN
    CREATE TABLE qams.patient_stay (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        patient_ref character varying(100) NOT NULL,
        encounter_ref character varying(100) NOT NULL,
        unit character varying(100) NOT NULL,
        department_id uuid,
        admitted_at_utc timestamp with time zone NOT NULL,
        discharged_at_utc timestamp with time zone,
        status character varying(20) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_patient_stay PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825223639_AddIntegrationHub') THEN
    CREATE INDEX ix_integration_endpoint_tenant_id_status ON qams.integration_endpoint (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825223639_AddIntegrationHub') THEN
    CREATE INDEX ix_integration_message_tenant_id_endpoint_id_status ON qams.integration_message (tenant_id, endpoint_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825223639_AddIntegrationHub') THEN
    CREATE UNIQUE INDEX ux_integration_message_dedup ON qams.integration_message (tenant_id, endpoint_id, dedup_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825223639_AddIntegrationHub') THEN
    CREATE INDEX ix_patient_stay_tenant_id_status ON qams.patient_stay (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825223639_AddIntegrationHub') THEN
    CREATE UNIQUE INDEX ux_patient_stay_encounter ON qams.patient_stay (tenant_id, encounter_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825223639_AddIntegrationHub') THEN
    ALTER TABLE qams.integration_endpoint ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.integration_endpoint FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.integration_endpoint;
    CREATE POLICY tenant_isolation ON qams.integration_endpoint
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825223639_AddIntegrationHub') THEN
    ALTER TABLE qams.integration_message ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.integration_message FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.integration_message;
    CREATE POLICY tenant_isolation ON qams.integration_message
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825223639_AddIntegrationHub') THEN
    ALTER TABLE qams.patient_stay ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.patient_stay FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.patient_stay;
    CREATE POLICY tenant_isolation ON qams.patient_stay
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825223639_AddIntegrationHub') THEN
    ALTER TABLE qams.integration_endpoint ADD CONSTRAINT ck_integration_endpoint_system_domain
      CHECK (system IN ('His','Lis','Pharmacy','Hr','Other')) NOT VALID;
    ALTER TABLE qams.integration_endpoint VALIDATE CONSTRAINT ck_integration_endpoint_system_domain;

    ALTER TABLE qams.integration_endpoint ADD CONSTRAINT ck_integration_endpoint_protocol_domain
      CHECK (protocol IN ('Hl7V2','FhirR4','FileExtract','DbExtract')) NOT VALID;
    ALTER TABLE qams.integration_endpoint VALIDATE CONSTRAINT ck_integration_endpoint_protocol_domain;

    ALTER TABLE qams.integration_endpoint ADD CONSTRAINT ck_integration_endpoint_status_domain
      CHECK (status IN ('Active','Suspended')) NOT VALID;
    ALTER TABLE qams.integration_endpoint VALIDATE CONSTRAINT ck_integration_endpoint_status_domain;

    ALTER TABLE qams.integration_message ADD CONSTRAINT ck_integration_message_status_domain
      CHECK (status IN ('Received','Processed','Failed')) NOT VALID;
    ALTER TABLE qams.integration_message VALIDATE CONSTRAINT ck_integration_message_status_domain;

    ALTER TABLE qams.patient_stay ADD CONSTRAINT ck_patient_stay_status_domain
      CHECK (status IN ('Admitted','Discharged')) NOT VALID;
    ALTER TABLE qams.patient_stay VALIDATE CONSTRAINT ck_patient_stay_status_domain;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825223639_AddIntegrationHub') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260825223639_AddIntegrationHub', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825232736_AddPatientSafety') THEN
    CREATE TABLE qams.patient_safety_event (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        branch_id uuid,
        department_id uuid,
        event_ref character varying(30) NOT NULL,
        type character varying(20) NOT NULL,
        patient_ref character varying(100) NOT NULL,
        unit character varying(100) NOT NULL,
        occurred_at_utc timestamp with time zone NOT NULL,
        harm_level character varying(20) NOT NULL,
        origin character varying(20) NOT NULL,
        description text NOT NULL,
        stage character varying(20),
        status character varying(20) NOT NULL,
        reviewed_by uuid,
        review_notes text,
        reviewed_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_patient_safety_event PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825232736_AddPatientSafety') THEN
    CREATE UNIQUE INDEX ix_patient_safety_event_tenant_id_event_ref ON qams.patient_safety_event (tenant_id, event_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825232736_AddPatientSafety') THEN
    CREATE INDEX ix_patient_safety_event_tenant_id_occurred_at_utc ON qams.patient_safety_event (tenant_id, occurred_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825232736_AddPatientSafety') THEN
    CREATE INDEX ix_patient_safety_event_tenant_id_type_status ON qams.patient_safety_event (tenant_id, type, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825232736_AddPatientSafety') THEN
    ALTER TABLE qams.patient_safety_event ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.patient_safety_event FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.patient_safety_event;
    CREATE POLICY tenant_isolation ON qams.patient_safety_event
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825232736_AddPatientSafety') THEN
    ALTER TABLE qams.patient_safety_event ADD CONSTRAINT ck_patient_safety_event_type_domain
      CHECK (type IN ('Fall','PressureInjury')) NOT VALID;
    ALTER TABLE qams.patient_safety_event VALIDATE CONSTRAINT ck_patient_safety_event_type_domain;

    ALTER TABLE qams.patient_safety_event ADD CONSTRAINT ck_patient_safety_event_harm_level_domain
      CHECK (harm_level IN ('None','Minor','Moderate','Severe','Death')) NOT VALID;
    ALTER TABLE qams.patient_safety_event VALIDATE CONSTRAINT ck_patient_safety_event_harm_level_domain;

    ALTER TABLE qams.patient_safety_event ADD CONSTRAINT ck_patient_safety_event_origin_domain
      CHECK (origin IN ('PresentOnAdmission','HospitalAcquired')) NOT VALID;
    ALTER TABLE qams.patient_safety_event VALIDATE CONSTRAINT ck_patient_safety_event_origin_domain;

    ALTER TABLE qams.patient_safety_event ADD CONSTRAINT ck_patient_safety_event_stage_domain
      CHECK (stage IS NULL OR stage IN ('Stage1','Stage2','Stage3','Stage4','Unstageable','DeepTissueInjury')) NOT VALID;
    ALTER TABLE qams.patient_safety_event VALIDATE CONSTRAINT ck_patient_safety_event_stage_domain;

    ALTER TABLE qams.patient_safety_event ADD CONSTRAINT ck_patient_safety_event_status_domain
      CHECK (status IN ('Reported','Reviewed','Closed')) NOT VALID;
    ALTER TABLE qams.patient_safety_event VALIDATE CONSTRAINT ck_patient_safety_event_status_domain;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260825232736_AddPatientSafety') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260825232736_AddPatientSafety', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826161611_AddInfectionControl') THEN
    CREATE TABLE qams.device_exposure (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        branch_id uuid,
        department_id uuid,
        patient_ref character varying(100) NOT NULL,
        unit character varying(100) NOT NULL,
        device_type character varying(20) NOT NULL,
        inserted_at_utc timestamp with time zone NOT NULL,
        removed_at_utc timestamp with time zone,
        status character varying(20) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_device_exposure PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826161611_AddInfectionControl') THEN
    CREATE TABLE qams.hai_case (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        branch_id uuid,
        department_id uuid,
        case_ref character varying(30) NOT NULL,
        type character varying(20) NOT NULL,
        patient_ref character varying(100) NOT NULL,
        unit character varying(100) NOT NULL,
        onset_date_utc timestamp with time zone NOT NULL,
        organism character varying(200),
        description text NOT NULL,
        status character varying(20) NOT NULL,
        reviewed_by uuid,
        review_notes text,
        reviewed_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_hai_case PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826161611_AddInfectionControl') THEN
    CREATE INDEX ix_device_exposure_tenant_id_device_type_status ON qams.device_exposure (tenant_id, device_type, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826161611_AddInfectionControl') THEN
    CREATE INDEX ix_device_exposure_tenant_id_inserted_at_utc ON qams.device_exposure (tenant_id, inserted_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826161611_AddInfectionControl') THEN
    CREATE UNIQUE INDEX ix_hai_case_tenant_id_case_ref ON qams.hai_case (tenant_id, case_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826161611_AddInfectionControl') THEN
    CREATE INDEX ix_hai_case_tenant_id_onset_date_utc ON qams.hai_case (tenant_id, onset_date_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826161611_AddInfectionControl') THEN
    CREATE INDEX ix_hai_case_tenant_id_type_status ON qams.hai_case (tenant_id, type, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826161611_AddInfectionControl') THEN
    ALTER TABLE qams.device_exposure ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.device_exposure FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.device_exposure;
    CREATE POLICY tenant_isolation ON qams.device_exposure
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');

    ALTER TABLE qams.hai_case ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.hai_case FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.hai_case;
    CREATE POLICY tenant_isolation ON qams.hai_case
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826161611_AddInfectionControl') THEN
    ALTER TABLE qams.device_exposure ADD CONSTRAINT ck_device_exposure_device_type_domain
      CHECK (device_type IN ('CentralLine','UrinaryCatheter','Ventilator')) NOT VALID;
    ALTER TABLE qams.device_exposure VALIDATE CONSTRAINT ck_device_exposure_device_type_domain;

    ALTER TABLE qams.device_exposure ADD CONSTRAINT ck_device_exposure_status_domain
      CHECK (status IN ('InPlace','Removed')) NOT VALID;
    ALTER TABLE qams.device_exposure VALIDATE CONSTRAINT ck_device_exposure_status_domain;

    ALTER TABLE qams.hai_case ADD CONSTRAINT ck_hai_case_type_domain
      CHECK (type IN ('Clabsi','Cauti','Vap','Ssi')) NOT VALID;
    ALTER TABLE qams.hai_case VALIDATE CONSTRAINT ck_hai_case_type_domain;

    ALTER TABLE qams.hai_case ADD CONSTRAINT ck_hai_case_status_domain
      CHECK (status IN ('Reported','Reviewed','Closed')) NOT VALID;
    ALTER TABLE qams.hai_case VALIDATE CONSTRAINT ck_hai_case_status_domain;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826161611_AddInfectionControl') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260826161611_AddInfectionControl', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826171401_AddTrainingManagement') THEN
    CREATE TABLE qams.training_course (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        course_ref character varying(30) NOT NULL,
        title character varying(200) NOT NULL,
        category character varying(20) NOT NULL,
        description text NOT NULL,
        duration_hours numeric(6,2) NOT NULL,
        validity_months integer,
        pass_mark integer NOT NULL,
        status character varying(20) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_training_course PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826171401_AddTrainingManagement') THEN
    CREATE TABLE qams.training_session (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        course_id uuid NOT NULL,
        session_ref character varying(30) NOT NULL,
        scheduled_at_utc timestamp with time zone NOT NULL,
        location character varying(200) NOT NULL,
        trainer_name character varying(200) NOT NULL,
        status character varying(20) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_training_session PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826171401_AddTrainingManagement') THEN
    CREATE TABLE qams.training_session_attendance (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        trainee_id uuid NOT NULL,
        attended boolean NOT NULL,
        pre_score integer,
        post_score integer,
        passed boolean NOT NULL,
        training_session_id uuid NOT NULL,
        CONSTRAINT pk_training_session_attendance PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_training_session_attendance_training_session_tenant_id_trai FOREIGN KEY (tenant_id, training_session_id) REFERENCES qams.training_session (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826171401_AddTrainingManagement') THEN
    CREATE INDEX ix_training_course_tenant_id_category_status ON qams.training_course (tenant_id, category, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826171401_AddTrainingManagement') THEN
    CREATE UNIQUE INDEX ix_training_course_tenant_id_course_ref ON qams.training_course (tenant_id, course_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826171401_AddTrainingManagement') THEN
    CREATE INDEX ix_training_session_tenant_id_course_id ON qams.training_session (tenant_id, course_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826171401_AddTrainingManagement') THEN
    CREATE UNIQUE INDEX ix_training_session_tenant_id_session_ref ON qams.training_session (tenant_id, session_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826171401_AddTrainingManagement') THEN
    CREATE INDEX ix_training_session_tenant_id_status ON qams.training_session (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826171401_AddTrainingManagement') THEN
    CREATE INDEX ix_training_session_attendance_tenant_id_training_session_id ON qams.training_session_attendance (tenant_id, training_session_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826171401_AddTrainingManagement') THEN
    ALTER TABLE qams.training_course ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.training_course FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.training_course;
    CREATE POLICY tenant_isolation ON qams.training_course
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826171401_AddTrainingManagement') THEN
    ALTER TABLE qams.training_session ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.training_session FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.training_session;
    CREATE POLICY tenant_isolation ON qams.training_session
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826171401_AddTrainingManagement') THEN
    ALTER TABLE qams.training_session_attendance ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.training_session_attendance FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.training_session_attendance;
    CREATE POLICY tenant_isolation ON qams.training_session_attendance
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826171401_AddTrainingManagement') THEN
    ALTER TABLE qams.training_course ADD CONSTRAINT ck_training_course_category_domain
      CHECK (category IN ('Mandatory','Clinical','Safety','Orientation','Cme')) NOT VALID;
    ALTER TABLE qams.training_course VALIDATE CONSTRAINT ck_training_course_category_domain;

    ALTER TABLE qams.training_course ADD CONSTRAINT ck_training_course_status_domain
      CHECK (status IN ('Draft','Active','Retired')) NOT VALID;
    ALTER TABLE qams.training_course VALIDATE CONSTRAINT ck_training_course_status_domain;

    ALTER TABLE qams.training_course ADD CONSTRAINT ck_training_course_pass_mark_range
      CHECK (pass_mark BETWEEN 0 AND 100) NOT VALID;
    ALTER TABLE qams.training_course VALIDATE CONSTRAINT ck_training_course_pass_mark_range;

    ALTER TABLE qams.training_session ADD CONSTRAINT ck_training_session_status_domain
      CHECK (status IN ('Scheduled','Held','Closed','Cancelled')) NOT VALID;
    ALTER TABLE qams.training_session VALIDATE CONSTRAINT ck_training_session_status_domain;

    ALTER TABLE qams.training_session_attendance ADD CONSTRAINT ck_training_session_attendance_score_range
      CHECK ((pre_score IS NULL OR pre_score BETWEEN 0 AND 100)
             AND (post_score IS NULL OR post_score BETWEEN 0 AND 100)) NOT VALID;
    ALTER TABLE qams.training_session_attendance VALIDATE CONSTRAINT ck_training_session_attendance_score_range;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826171401_AddTrainingManagement') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260826171401_AddTrainingManagement', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826173836_AddMortalityReview') THEN
    CREATE TABLE qams.complication_case (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        branch_id uuid,
        department_id uuid,
        case_ref character varying(30) NOT NULL,
        patient_ref character varying(100) NOT NULL,
        unit character varying(100) NOT NULL,
        type character varying(30) NOT NULL,
        severity character varying(20) NOT NULL,
        occurred_date_utc timestamp with time zone NOT NULL,
        description text NOT NULL,
        status character varying(20) NOT NULL,
        reviewed_by uuid,
        review_notes text,
        preventable boolean,
        reviewed_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_complication_case PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826173836_AddMortalityReview') THEN
    CREATE TABLE qams.mortality_review (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        branch_id uuid,
        department_id uuid,
        review_ref character varying(30) NOT NULL,
        patient_ref character varying(100) NOT NULL,
        unit character varying(100) NOT NULL,
        death_date_utc timestamp with time zone NOT NULL,
        primary_diagnosis character varying(300),
        status character varying(20) NOT NULL,
        classification character varying(30),
        first_reviewer_id uuid,
        classification_findings text,
        second_reviewer_id uuid,
        second_review_notes text,
        second_reviewer_concurs boolean,
        committee_learnings text,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_mortality_review PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826173836_AddMortalityReview') THEN
    CREATE UNIQUE INDEX ix_complication_case_tenant_id_case_ref ON qams.complication_case (tenant_id, case_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826173836_AddMortalityReview') THEN
    CREATE INDEX ix_complication_case_tenant_id_occurred_date_utc ON qams.complication_case (tenant_id, occurred_date_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826173836_AddMortalityReview') THEN
    CREATE INDEX ix_complication_case_tenant_id_type_status ON qams.complication_case (tenant_id, type, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826173836_AddMortalityReview') THEN
    CREATE INDEX ix_mortality_review_tenant_id_death_date_utc ON qams.mortality_review (tenant_id, death_date_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826173836_AddMortalityReview') THEN
    CREATE UNIQUE INDEX ix_mortality_review_tenant_id_review_ref ON qams.mortality_review (tenant_id, review_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826173836_AddMortalityReview') THEN
    CREATE INDEX ix_mortality_review_tenant_id_status ON qams.mortality_review (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826173836_AddMortalityReview') THEN
    ALTER TABLE qams.mortality_review ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.mortality_review FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.mortality_review;
    CREATE POLICY tenant_isolation ON qams.mortality_review
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826173836_AddMortalityReview') THEN
    ALTER TABLE qams.complication_case ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.complication_case FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.complication_case;
    CREATE POLICY tenant_isolation ON qams.complication_case
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826173836_AddMortalityReview') THEN
    ALTER TABLE qams.mortality_review ADD CONSTRAINT ck_mortality_review_classification_domain
      CHECK (classification IS NULL OR classification IN ('Expected','Unexpected','PotentiallyPreventable','Preventable')) NOT VALID;
    ALTER TABLE qams.mortality_review VALIDATE CONSTRAINT ck_mortality_review_classification_domain;

    ALTER TABLE qams.mortality_review ADD CONSTRAINT ck_mortality_review_status_domain
      CHECK (status IN ('Reported','Classified','SecondReviewed','CommitteeDiscussed','Closed')) NOT VALID;
    ALTER TABLE qams.mortality_review VALIDATE CONSTRAINT ck_mortality_review_status_domain;

    ALTER TABLE qams.complication_case ADD CONSTRAINT ck_complication_case_type_domain
      CHECK (type IN ('ReturnToTheatre','UnplannedIcuAdmission','UnplannedReadmission','HospitalAcquiredCondition','Other')) NOT VALID;
    ALTER TABLE qams.complication_case VALIDATE CONSTRAINT ck_complication_case_type_domain;

    ALTER TABLE qams.complication_case ADD CONSTRAINT ck_complication_case_severity_domain
      CHECK (severity IN ('Minor','Moderate','Severe','LifeThreatening')) NOT VALID;
    ALTER TABLE qams.complication_case VALIDATE CONSTRAINT ck_complication_case_severity_domain;

    ALTER TABLE qams.complication_case ADD CONSTRAINT ck_complication_case_status_domain
      CHECK (status IN ('Reported','Reviewed','Closed')) NOT VALID;
    ALTER TABLE qams.complication_case VALIDATE CONSTRAINT ck_complication_case_status_domain;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826173836_AddMortalityReview') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260826173836_AddMortalityReview', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826191030_AddCredentialing') THEN
    CREATE TABLE qams.practitioner (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        practitioner_ref character varying(30) NOT NULL,
        full_name character varying(200) NOT NULL,
        specialty character varying(150) NOT NULL,
        status character varying(20) NOT NULL,
        appointed_until date,
        suspension_reason character varying(1000),
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_practitioner PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826191030_AddCredentialing') THEN
    CREATE TABLE qams.practitioner_licence (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        type character varying(30) NOT NULL,
        identifier character varying(100) NOT NULL,
        issuer character varying(150) NOT NULL,
        expires_on date NOT NULL,
        verification_status character varying(20) NOT NULL,
        verified_by uuid,
        verification_source character varying(300),
        verified_at_utc timestamp with time zone,
        practitioner_id uuid NOT NULL,
        CONSTRAINT pk_practitioner_licence PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_practitioner_licence_practitioner_tenant_id_practitioner_id FOREIGN KEY (tenant_id, practitioner_id) REFERENCES qams.practitioner (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826191030_AddCredentialing') THEN
    CREATE TABLE qams.practitioner_privilege (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        name character varying(200) NOT NULL,
        status character varying(20) NOT NULL,
        granted_until date,
        denial_reason character varying(1000),
        practitioner_id uuid NOT NULL,
        CONSTRAINT pk_practitioner_privilege PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_practitioner_privilege_practitioner_tenant_id_practitioner_ FOREIGN KEY (tenant_id, practitioner_id) REFERENCES qams.practitioner (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826191030_AddCredentialing') THEN
    CREATE UNIQUE INDEX ix_practitioner_tenant_id_practitioner_ref ON qams.practitioner (tenant_id, practitioner_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826191030_AddCredentialing') THEN
    CREATE INDEX ix_practitioner_tenant_id_specialty ON qams.practitioner (tenant_id, specialty);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826191030_AddCredentialing') THEN
    CREATE INDEX ix_practitioner_tenant_id_status ON qams.practitioner (tenant_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826191030_AddCredentialing') THEN
    CREATE INDEX ix_practitioner_licence_tenant_id_practitioner_id ON qams.practitioner_licence (tenant_id, practitioner_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826191030_AddCredentialing') THEN
    CREATE INDEX ix_practitioner_privilege_tenant_id_practitioner_id ON qams.practitioner_privilege (tenant_id, practitioner_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826191030_AddCredentialing') THEN
    ALTER TABLE qams.practitioner ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.practitioner FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.practitioner;
    CREATE POLICY tenant_isolation ON qams.practitioner
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826191030_AddCredentialing') THEN
    ALTER TABLE qams.practitioner_licence ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.practitioner_licence FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.practitioner_licence;
    CREATE POLICY tenant_isolation ON qams.practitioner_licence
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826191030_AddCredentialing') THEN
    ALTER TABLE qams.practitioner_privilege ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.practitioner_privilege FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.practitioner_privilege;
    CREATE POLICY tenant_isolation ON qams.practitioner_privilege
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826191030_AddCredentialing') THEN
    ALTER TABLE qams.practitioner ADD CONSTRAINT ck_practitioner_status_domain
      CHECK (status IN ('Pending','Credentialed','Suspended')) NOT VALID;
    ALTER TABLE qams.practitioner VALIDATE CONSTRAINT ck_practitioner_status_domain;

    ALTER TABLE qams.practitioner_licence ADD CONSTRAINT ck_practitioner_licence_type_domain
      CHECK (type IN ('MedicalLicence','NursingLicence','BoardCertification','Bls','Acls','Other')) NOT VALID;
    ALTER TABLE qams.practitioner_licence VALIDATE CONSTRAINT ck_practitioner_licence_type_domain;

    ALTER TABLE qams.practitioner_licence ADD CONSTRAINT ck_practitioner_licence_verification_domain
      CHECK (verification_status IN ('Pending','Verified')) NOT VALID;
    ALTER TABLE qams.practitioner_licence VALIDATE CONSTRAINT ck_practitioner_licence_verification_domain;

    ALTER TABLE qams.practitioner_privilege ADD CONSTRAINT ck_practitioner_privilege_status_domain
      CHECK (status IN ('Requested','Granted','Denied','Expired')) NOT VALID;
    ALTER TABLE qams.practitioner_privilege VALIDATE CONSTRAINT ck_practitioner_privilege_status_domain;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826191030_AddCredentialing') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260826191030_AddCredentialing', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826203856_AddEnvironmentOfCare') THEN
    CREATE TABLE qams.drill (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        branch_id uuid,
        drill_ref character varying(30) NOT NULL,
        type character varying(20) NOT NULL,
        location character varying(150) NOT NULL,
        scheduled_date date NOT NULL,
        status character varying(20) NOT NULL,
        executed_at_utc timestamp with time zone,
        participant_count integer,
        evaluation_score integer,
        improvement_notes text,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_drill PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826203856_AddEnvironmentOfCare') THEN
    CREATE TABLE qams.safety_round (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        branch_id uuid,
        round_ref character varying(30) NOT NULL,
        area character varying(150) NOT NULL,
        type character varying(30) NOT NULL,
        scheduled_date date NOT NULL,
        status character varying(20) NOT NULL,
        conducted_by uuid,
        completed_at_utc timestamp with time zone,
        created_at_utc timestamp with time zone NOT NULL,
        created_by text,
        created_by_user_id uuid,
        modified_at_utc timestamp with time zone,
        modified_by text,
        CONSTRAINT pk_safety_round PRIMARY KEY (tenant_id, id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826203856_AddEnvironmentOfCare') THEN
    CREATE TABLE qams.safety_round_finding (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        description character varying(2000) NOT NULL,
        severity character varying(20) NOT NULL,
        status character varying(20) NOT NULL,
        corrective_note character varying(2000),
        resolved_at_utc timestamp with time zone,
        safety_round_id uuid NOT NULL,
        CONSTRAINT pk_safety_round_finding PRIMARY KEY (tenant_id, id),
        CONSTRAINT fk_safety_round_finding_safety_round_tenant_id_safety_round_id FOREIGN KEY (tenant_id, safety_round_id) REFERENCES qams.safety_round (tenant_id, id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826203856_AddEnvironmentOfCare') THEN
    CREATE UNIQUE INDEX ix_drill_tenant_id_drill_ref ON qams.drill (tenant_id, drill_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826203856_AddEnvironmentOfCare') THEN
    CREATE INDEX ix_drill_tenant_id_scheduled_date ON qams.drill (tenant_id, scheduled_date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826203856_AddEnvironmentOfCare') THEN
    CREATE INDEX ix_drill_tenant_id_type_status ON qams.drill (tenant_id, type, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826203856_AddEnvironmentOfCare') THEN
    CREATE UNIQUE INDEX ix_safety_round_tenant_id_round_ref ON qams.safety_round (tenant_id, round_ref);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826203856_AddEnvironmentOfCare') THEN
    CREATE INDEX ix_safety_round_tenant_id_scheduled_date ON qams.safety_round (tenant_id, scheduled_date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826203856_AddEnvironmentOfCare') THEN
    CREATE INDEX ix_safety_round_tenant_id_type_status ON qams.safety_round (tenant_id, type, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826203856_AddEnvironmentOfCare') THEN
    CREATE INDEX ix_safety_round_finding_tenant_id_safety_round_id ON qams.safety_round_finding (tenant_id, safety_round_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826203856_AddEnvironmentOfCare') THEN
    ALTER TABLE qams.safety_round ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.safety_round FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.safety_round;
    CREATE POLICY tenant_isolation ON qams.safety_round
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826203856_AddEnvironmentOfCare') THEN
    ALTER TABLE qams.safety_round_finding ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.safety_round_finding FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.safety_round_finding;
    CREATE POLICY tenant_isolation ON qams.safety_round_finding
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826203856_AddEnvironmentOfCare') THEN
    ALTER TABLE qams.drill ENABLE ROW LEVEL SECURITY;
    ALTER TABLE qams.drill FORCE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON qams.drill;
    CREATE POLICY tenant_isolation ON qams.drill
      FOR ALL
      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on')
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
             OR current_setting('app.bypass_rls', true) = 'on');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826203856_AddEnvironmentOfCare') THEN
    ALTER TABLE qams.safety_round ADD CONSTRAINT ck_safety_round_type_domain
      CHECK (type IN ('FireSafety','InfectionControl','GeneralSafety','HazardousMaterials','Utilities','Security')) NOT VALID;
    ALTER TABLE qams.safety_round VALIDATE CONSTRAINT ck_safety_round_type_domain;

    ALTER TABLE qams.safety_round ADD CONSTRAINT ck_safety_round_status_domain
      CHECK (status IN ('Scheduled','InProgress','Completed')) NOT VALID;
    ALTER TABLE qams.safety_round VALIDATE CONSTRAINT ck_safety_round_status_domain;

    ALTER TABLE qams.safety_round_finding ADD CONSTRAINT ck_safety_round_finding_severity_domain
      CHECK (severity IN ('Low','Medium','High','Critical')) NOT VALID;
    ALTER TABLE qams.safety_round_finding VALIDATE CONSTRAINT ck_safety_round_finding_severity_domain;

    ALTER TABLE qams.safety_round_finding ADD CONSTRAINT ck_safety_round_finding_status_domain
      CHECK (status IN ('Open','Resolved')) NOT VALID;
    ALTER TABLE qams.safety_round_finding VALIDATE CONSTRAINT ck_safety_round_finding_status_domain;

    ALTER TABLE qams.drill ADD CONSTRAINT ck_drill_type_domain
      CHECK (type IN ('Fire','Evacuation','CodeBlue','Disaster','Hazmat','ActiveShooter')) NOT VALID;
    ALTER TABLE qams.drill VALIDATE CONSTRAINT ck_drill_type_domain;

    ALTER TABLE qams.drill ADD CONSTRAINT ck_drill_status_domain
      CHECK (status IN ('Scheduled','Executed','Evaluated')) NOT VALID;
    ALTER TABLE qams.drill VALIDATE CONSTRAINT ck_drill_status_domain;

    ALTER TABLE qams.drill ADD CONSTRAINT ck_drill_score_range
      CHECK (evaluation_score IS NULL OR evaluation_score BETWEEN 0 AND 100) NOT VALID;
    ALTER TABLE qams.drill VALIDATE CONSTRAINT ck_drill_score_range;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826203856_AddEnvironmentOfCare') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260826203856_AddEnvironmentOfCare', '9.0.19');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826210742_ChangeControlEmergencyPathway') THEN
    ALTER TABLE qams.change_request ALTER COLUMN status TYPE character varying(30);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826210742_ChangeControlEmergencyPathway') THEN
    ALTER TABLE qams.change_request ADD impact_level character varying(10) NOT NULL DEFAULT 'Medium';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826210742_ChangeControlEmergencyPathway') THEN
    ALTER TABLE qams.change_request ADD is_emergency boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826210742_ChangeControlEmergencyPathway') THEN
    ALTER TABLE qams.change_request ADD ratified_at_utc timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826210742_ChangeControlEmergencyPathway') THEN
    ALTER TABLE qams.change_request ADD ratified_by uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826210742_ChangeControlEmergencyPathway') THEN
    ALTER TABLE qams.change_request ADD retrospective_deadline date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826210742_ChangeControlEmergencyPathway') THEN
    ALTER TABLE qams.change_request DROP CONSTRAINT IF EXISTS ck_change_request_status_domain;
    ALTER TABLE qams.change_request ADD CONSTRAINT ck_change_request_status_domain
      CHECK (status IN ('Proposed','Approved','Rejected','Closed','Reviewed','ImplementedPendingRatification')) NOT VALID;
    ALTER TABLE qams.change_request VALIDATE CONSTRAINT ck_change_request_status_domain;

    ALTER TABLE qams.change_request ADD CONSTRAINT ck_change_request_impact_level_domain
      CHECK (impact_level IN ('Low','Medium','High')) NOT VALID;
    ALTER TABLE qams.change_request VALIDATE CONSTRAINT ck_change_request_impact_level_domain;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260826210742_ChangeControlEmergencyPathway') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260826210742_ChangeControlEmergencyPathway', '9.0.19');
    END IF;
END $EF$;
COMMIT;

