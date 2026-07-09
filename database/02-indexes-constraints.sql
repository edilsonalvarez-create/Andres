/* ============================================================================
   QA Guardian — Script 2/4: índices y restricciones únicas.
   ============================================================================ */
USE QAGuardian;
GO

-- Unicidad de negocio
CREATE UNIQUE INDEX UX_Roles_Name              ON dbo.Roles(Name);
CREATE UNIQUE INDEX UX_Users_Email             ON dbo.Users(Email);
CREATE UNIQUE INDEX UX_Projects_Code           ON dbo.Projects(Code);
CREATE UNIQUE INDEX UX_ProjectVersions_Number  ON dbo.ProjectVersions(ProjectId, Number);
CREATE UNIQUE INDEX UX_TestCases_Code          ON dbo.TestCases(ProjectId, Code);
CREATE UNIQUE INDEX UX_Defects_Code            ON dbo.Defects(ProjectId, Code);
CREATE UNIQUE INDEX UX_IntegrationSettings     ON dbo.IntegrationSettings(ProjectId, Type);
CREATE UNIQUE INDEX UX_VisualBaselines_Key     ON dbo.VisualBaselines(ProjectId, BaselineKey);
CREATE UNIQUE INDEX UX_GateEvaluations_Run     ON dbo.QualityGateEvaluations(TestRunId);

-- Índices de consulta frecuente (dashboards y listados)
CREATE INDEX IX_RefreshTokens_Token       ON dbo.RefreshTokens(Token);
CREATE INDEX IX_AuditLogs_Timestamp       ON dbo.AuditLogs(Timestamp);
CREATE INDEX IX_AuditLogs_EntityName      ON dbo.AuditLogs(EntityName);
CREATE INDEX IX_Modules_Project           ON dbo.Modules(ProjectId, Name);
CREATE INDEX IX_TestCases_Project         ON dbo.TestCases(ProjectId) INCLUDE (Type, Status, Framework);
CREATE INDEX IX_TestCases_Type            ON dbo.TestCases(Type);
CREATE INDEX IX_TestRuns_Project          ON dbo.TestRuns(ProjectId) INCLUDE (Status, RunType, CreatedAt);
CREATE INDEX IX_TestRuns_Status           ON dbo.TestRuns(Status);
CREATE INDEX IX_TestRuns_CreatedAt        ON dbo.TestRuns(CreatedAt);
CREATE INDEX IX_TestResults_Run           ON dbo.TestResults(TestRunId) INCLUDE (Status, DurationMs);
CREATE INDEX IX_TestResults_Status        ON dbo.TestResults(Status);
CREATE INDEX IX_SecurityFindings_Run      ON dbo.SecurityFindings(TestRunId, Risk);
CREATE INDEX IX_Defects_Status            ON dbo.Defects(Status);
CREATE INDEX IX_Defects_Severity          ON dbo.Defects(Severity);
CREATE INDEX IX_Defects_Project           ON dbo.Defects(ProjectId) INCLUDE (Status, Severity, Priority);
CREATE INDEX IX_AiAnalyses_TestResult     ON dbo.AiAnalyses(TestResultId);
GO

-- Restricciones de dominio
ALTER TABLE dbo.QualityGateConditions
    ADD CONSTRAINT CK_GateConditions_Operator CHECK (Operator IN (1, 2, 3));
ALTER TABLE dbo.TestRuns
    ADD CONSTRAINT CK_TestRuns_Environment CHECK (Environment BETWEEN 1 AND 4);
ALTER TABLE dbo.Defects
    ADD CONSTRAINT CK_Defects_Severity CHECK (Severity BETWEEN 1 AND 5),
        CONSTRAINT CK_Defects_Priority CHECK (Priority BETWEEN 1 AND 4),
        CONSTRAINT CK_Defects_Status   CHECK (Status BETWEEN 1 AND 8);
GO
