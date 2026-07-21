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
    VALUES ('20260721211309_InitialFoundation', '9.0.18');
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
    VALUES ('20260721214118_IdentityAndImprovement', '9.0.18');
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
    VALUES ('20260721215255_DocumentControl', '9.0.18');
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
    VALUES ('20260721220535_AuditManagement', '9.0.18');
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
    VALUES ('20260721221903_ResourcesModules', '9.0.18');
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
    VALUES ('20260721223052_GovernanceAndSuppliers', '9.0.18');
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
    VALUES ('20260721224327_OrgAndNotifications', '9.0.18');
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
    VALUES ('20260721225752_AnalyticalQuality', '9.0.18');
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
    VALUES ('20260721231005_RecordsAndSla', '9.0.18');
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

                    CREATE OR REPLACE FUNCTION audit.reject_mutation() RETURNS trigger AS $
                    BEGIN
                        RAISE EXCEPTION 'audit ledgers are append-only';
                    END;
                    $ LANGUAGE plpgsql;

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
    VALUES ('20260721232300_ComplianceAndAuth', '9.0.18');
    END IF;
END $EF$;
COMMIT;

