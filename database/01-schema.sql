/* ============================================================================
   QA Guardian — Esquema de base de datos (SQL Server 2019+)
   Script 1/4: creación de base de datos y tablas.
   Nota: la aplicación también puede crear el esquema automáticamente vía
   EF Core; estos scripts permiten un despliegue controlado por DBA.
   ============================================================================ */

IF DB_ID('QAGuardian') IS NULL
    CREATE DATABASE QAGuardian;
GO
USE QAGuardian;
GO

/* ─────────────────────────── Seguridad y usuarios ─────────────────────────── */

CREATE TABLE dbo.Roles (
    Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Roles PRIMARY KEY DEFAULT NEWID(),
    Name        NVARCHAR(50)     NOT NULL,
    Description NVARCHAR(300)    NOT NULL
);

CREATE TABLE dbo.Users (
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY DEFAULT NEWID(),
    Email               NVARCHAR(256)    NOT NULL,
    FullName            NVARCHAR(200)    NOT NULL,
    PasswordHash        NVARCHAR(500)    NOT NULL,
    IsActive            BIT              NOT NULL DEFAULT 1,
    LastLoginAt         DATETIME2        NULL,
    FailedLoginAttempts INT              NOT NULL DEFAULT 0,
    LockedUntil         DATETIME2        NULL,
    CreatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(256)    NULL,
    UpdatedAt           DATETIME2        NULL,
    UpdatedBy           NVARCHAR(256)    NULL,
    IsDeleted           BIT              NOT NULL DEFAULT 0
);

CREATE TABLE dbo.UserRoles (
    UserId  UNIQUEIDENTIFIER NOT NULL,
    RolesId UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RolesId),
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RolesId) REFERENCES dbo.Roles(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.RefreshTokens (
    Id        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY DEFAULT NEWID(),
    UserId    UNIQUEIDENTIFIER NOT NULL,
    Token     NVARCHAR(200)    NOT NULL,
    ExpiresAt DATETIME2        NOT NULL,
    CreatedAt DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    RevokedAt DATETIME2        NULL,
    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.AuditLogs (
    Id         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY DEFAULT NEWID(),
    UserId     UNIQUEIDENTIFIER NULL,
    UserEmail  NVARCHAR(256)    NOT NULL,
    Action     NVARCHAR(50)     NOT NULL,
    EntityName NVARCHAR(100)    NOT NULL,
    EntityId   NVARCHAR(50)     NULL,
    OldValues  NVARCHAR(MAX)    NULL,
    NewValues  NVARCHAR(MAX)    NULL,
    IpAddress  NVARCHAR(50)     NULL,
    Timestamp  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME()
);

/* ─────────────────────────── Quality Gates ─────────────────────────── */

CREATE TABLE dbo.QualityGates (
    Id        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_QualityGates PRIMARY KEY DEFAULT NEWID(),
    Name      NVARCHAR(150)    NOT NULL,
    IsDefault BIT              NOT NULL DEFAULT 0,
    CreatedAt DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(256)    NULL,
    UpdatedAt DATETIME2        NULL,
    UpdatedBy NVARCHAR(256)    NULL,
    IsDeleted BIT              NOT NULL DEFAULT 0
);

CREATE TABLE dbo.QualityGateConditions (
    Id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_QualityGateConditions PRIMARY KEY DEFAULT NEWID(),
    QualityGateId UNIQUEIDENTIFIER NOT NULL,
    Metric        INT              NOT NULL,  -- 1=PassRate% 2=Cobertura% 3=VulnCríticas 4=VulnAltas 5=Bugs 6=CodeSmells 7=Duplicación% 8=Hotspots 9=TiempoRespuestaMs 10=TasaError% 11=DefectosCríticosAbiertos
    Operator      INT              NOT NULL,  -- 1=>= 2=<= 3==
    Threshold     DECIMAL(18,4)    NOT NULL,
    IsBlocking    BIT              NOT NULL DEFAULT 1,
    CONSTRAINT FK_GateConditions_Gates FOREIGN KEY (QualityGateId)
        REFERENCES dbo.QualityGates(Id) ON DELETE CASCADE
);

/* ─────────────────────────── Proyectos ─────────────────────────── */

CREATE TABLE dbo.Projects (
    Id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Projects PRIMARY KEY DEFAULT NEWID(),
    Code          NVARCHAR(20)     NOT NULL,
    Name          NVARCHAR(200)    NOT NULL,
    Description   NVARCHAR(2000)   NULL,
    RepositoryUrl NVARCHAR(500)    NULL,
    IsActive      BIT              NOT NULL DEFAULT 1,
    QualityGateId UNIQUEIDENTIFIER NULL,
    CreatedAt     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy     NVARCHAR(256)    NULL,
    UpdatedAt     DATETIME2        NULL,
    UpdatedBy     NVARCHAR(256)    NULL,
    IsDeleted     BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Projects_QualityGates FOREIGN KEY (QualityGateId)
        REFERENCES dbo.QualityGates(Id) ON DELETE SET NULL
);

CREATE TABLE dbo.Modules (
    Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Modules PRIMARY KEY DEFAULT NEWID(),
    ProjectId   UNIQUEIDENTIFIER NOT NULL,
    Name        NVARCHAR(150)    NOT NULL,
    Description NVARCHAR(MAX)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NULL,
    UpdatedBy   NVARCHAR(256)    NULL,
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Modules_Projects FOREIGN KEY (ProjectId) REFERENCES dbo.Projects(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.ProjectVersions (
    Id         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProjectVersions PRIMARY KEY DEFAULT NEWID(),
    ProjectId  UNIQUEIDENTIFIER NOT NULL,
    Number     NVARCHAR(50)     NOT NULL,
    Notes      NVARCHAR(MAX)    NULL,
    ReleasedAt DATETIME2        NULL,
    CreatedAt  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(256)    NULL,
    UpdatedAt  DATETIME2        NULL,
    UpdatedBy  NVARCHAR(256)    NULL,
    IsDeleted  BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_ProjectVersions_Projects FOREIGN KEY (ProjectId) REFERENCES dbo.Projects(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.Requirements (
    Id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Requirements PRIMARY KEY DEFAULT NEWID(),
    ModuleId    UNIQUEIDENTIFIER NOT NULL,
    Code        NVARCHAR(30)     NOT NULL,
    Title       NVARCHAR(300)    NOT NULL,
    Description NVARCHAR(MAX)    NULL,
    CreatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy   NVARCHAR(256)    NULL,
    UpdatedAt   DATETIME2        NULL,
    UpdatedBy   NVARCHAR(256)    NULL,
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Requirements_Modules FOREIGN KEY (ModuleId) REFERENCES dbo.Modules(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.UserStories (
    Id                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UserStories PRIMARY KEY DEFAULT NEWID(),
    RequirementId      UNIQUEIDENTIFIER NOT NULL,
    Title              NVARCHAR(300)    NOT NULL,
    AcceptanceCriteria NVARCHAR(MAX)    NULL,
    CreatedAt          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy          NVARCHAR(256)    NULL,
    UpdatedAt          DATETIME2        NULL,
    UpdatedBy          NVARCHAR(256)    NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_UserStories_Requirements FOREIGN KEY (RequirementId)
        REFERENCES dbo.Requirements(Id) ON DELETE CASCADE
);

/* ─────────────────────────── Casos de prueba ─────────────────────────── */

CREATE TABLE dbo.TestCases (
    Id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TestCases PRIMARY KEY DEFAULT NEWID(),
    ProjectId            UNIQUEIDENTIFIER NOT NULL,
    ModuleId             UNIQUEIDENTIFIER NULL,
    UserStoryId          UNIQUEIDENTIFIER NULL,
    Code                 NVARCHAR(30)     NOT NULL,
    Title                NVARCHAR(300)    NOT NULL,
    Preconditions        NVARCHAR(MAX)    NULL,
    Type                 INT              NOT NULL,  -- 1=Funcional 2=Regresión 3=API 4=Rendimiento 5=Seguridad 6=Visual 7=BD 8=Smoke ...
    Priority             INT              NOT NULL,  -- 1=Baja 2=Media 3=Alta 4=Crítica
    Status               INT              NOT NULL,  -- 1=Borrador 2=Activo 3=Obsoleto
    Framework            INT              NOT NULL,  -- 0=Manual 1=Playwright 2=Postman 3=JMeter 4=ZAP 5=SQL
    AutomationScriptPath NVARCHAR(500)    NULL,
    Tags                 NVARCHAR(500)    NULL,
    CreatedAt            DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy            NVARCHAR(256)    NULL,
    UpdatedAt            DATETIME2        NULL,
    UpdatedBy            NVARCHAR(256)    NULL,
    IsDeleted            BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_TestCases_Projects FOREIGN KEY (ProjectId) REFERENCES dbo.Projects(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.TestSteps (
    Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TestSteps PRIMARY KEY DEFAULT NEWID(),
    TestCaseId     UNIQUEIDENTIFIER NOT NULL,
    [Order]        INT              NOT NULL,
    Action         NVARCHAR(1000)   NOT NULL,
    ExpectedResult NVARCHAR(1000)   NOT NULL,
    CONSTRAINT FK_TestSteps_TestCases FOREIGN KEY (TestCaseId) REFERENCES dbo.TestCases(Id) ON DELETE CASCADE
);

/* ─────────────────────────── Ejecuciones y resultados ─────────────────────────── */

CREATE TABLE dbo.TestRuns (
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TestRuns PRIMARY KEY DEFAULT NEWID(),
    ProjectId         UNIQUEIDENTIFIER NOT NULL,
    VersionId         UNIQUEIDENTIFIER NULL,
    RunType           INT              NOT NULL,
    Environment       INT              NOT NULL,  -- 1=Dev 2=QA 3=Staging 4=Prod
    Status            INT              NOT NULL,  -- 1=Pendiente 2=EnCurso 3=Completada 4=Fallida 5=Cancelada
    TriggeredBy       NVARCHAR(256)    NOT NULL,
    CommitSha         NVARCHAR(64)     NULL,
    PullRequestNumber INT              NULL,
    StartedAt         DATETIME2        NULL,
    CompletedAt       DATETIME2        NULL,
    MetricsJson       NVARCHAR(MAX)    NULL,
    ErrorMessage      NVARCHAR(MAX)    NULL,
    CreatedAt         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy         NVARCHAR(256)    NULL,
    UpdatedAt         DATETIME2        NULL,
    UpdatedBy         NVARCHAR(256)    NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_TestRuns_Projects FOREIGN KEY (ProjectId) REFERENCES dbo.Projects(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.TestResults (
    Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TestResults PRIMARY KEY DEFAULT NEWID(),
    TestRunId    UNIQUEIDENTIFIER NOT NULL,
    TestCaseId   UNIQUEIDENTIFIER NULL,
    Name         NVARCHAR(500)    NOT NULL,
    Status       INT              NOT NULL,  -- 1=Exitosa 2=Fallida 3=Omitida 4=Bloqueada 5=Inestable
    DurationMs   BIGINT           NOT NULL,
    ErrorMessage NVARCHAR(MAX)    NULL,
    StackTrace   NVARCHAR(MAX)    NULL,
    MetricsJson  NVARCHAR(MAX)    NULL,
    ExecutedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_TestResults_TestRuns FOREIGN KEY (TestRunId) REFERENCES dbo.TestRuns(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.Evidences (
    Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Evidences PRIMARY KEY DEFAULT NEWID(),
    TestResultId UNIQUEIDENTIFIER NOT NULL,
    Type         INT              NOT NULL,  -- 1=Screenshot 2=Video 3=Log 4=HTML 5=PDF 6=JSON 7=XML 8=HAR
    FilePath     NVARCHAR(600)    NOT NULL,
    ContentType  NVARCHAR(100)    NOT NULL,
    SizeBytes    BIGINT           NOT NULL,
    CreatedAt    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Evidences_TestResults FOREIGN KEY (TestResultId)
        REFERENCES dbo.TestResults(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.QualityGateEvaluations (
    Id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_QualityGateEvaluations PRIMARY KEY DEFAULT NEWID(),
    TestRunId     UNIQUEIDENTIFIER NOT NULL,
    QualityGateId UNIQUEIDENTIFIER NOT NULL,
    Status        INT              NOT NULL,  -- 1=Aprobado 2=Advertencia 3=Rechazado
    EvaluatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    DetailsJson   NVARCHAR(MAX)    NOT NULL,
    CONSTRAINT FK_GateEvaluations_TestRuns FOREIGN KEY (TestRunId)
        REFERENCES dbo.TestRuns(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.SecurityFindings (
    Id         UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SecurityFindings PRIMARY KEY DEFAULT NEWID(),
    TestRunId  UNIQUEIDENTIFIER NOT NULL,
    Name       NVARCHAR(300)    NOT NULL,
    Risk       INT              NOT NULL,  -- 0=Info 1=Bajo 2=Medio 3=Alto 4=Crítico
    Category   NVARCHAR(100)    NOT NULL,  -- SQL Injection, XSS, CSRF, Headers, Cookies, General
    Url        NVARCHAR(1000)   NULL,
    Parameter  NVARCHAR(MAX)    NULL,
    Evidence   NVARCHAR(MAX)    NULL,
    Solution   NVARCHAR(MAX)    NULL,
    CweId      NVARCHAR(20)     NULL,
    DetectedAt DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_SecurityFindings_TestRuns FOREIGN KEY (TestRunId)
        REFERENCES dbo.TestRuns(Id) ON DELETE CASCADE
);

/* ─────────────────────────── Defectos e IA ─────────────────────────── */

CREATE TABLE dbo.Defects (
    Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Defects PRIMARY KEY DEFAULT NEWID(),
    ProjectId        UNIQUEIDENTIFIER NOT NULL,
    ModuleId         UNIQUEIDENTIFIER NULL,
    TestResultId     UNIQUEIDENTIFIER NULL,
    Code             NVARCHAR(30)     NOT NULL,
    Title            NVARCHAR(300)    NOT NULL,
    Description      NVARCHAR(MAX)    NOT NULL,
    Severity         INT              NOT NULL,  -- 1=Trivial 2=Menor 3=Mayor 4=Crítico 5=Bloqueante
    Priority         INT              NOT NULL,  -- 1=Baja 2=Media 3=Alta 4=Urgente
    Status           INT              NOT NULL,  -- 1=Nuevo 2=Asignado 3=EnProgreso 4=Resuelto 5=Verificado 6=Cerrado 7=Reabierto 8=Rechazado
    ReportedByUserId UNIQUEIDENTIFIER NOT NULL,
    AssignedToUserId UNIQUEIDENTIFIER NULL,
    Sprint           NVARCHAR(50)     NULL,
    Version          NVARCHAR(50)     NULL,
    StackTrace       NVARCHAR(MAX)    NULL,
    ResolvedAt       DATETIME2        NULL,
    ClosedAt         DATETIME2        NULL,
    CreatedAt        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy        NVARCHAR(256)    NULL,
    UpdatedAt        DATETIME2        NULL,
    UpdatedBy        NVARCHAR(256)    NULL,
    IsDeleted        BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Defects_Projects FOREIGN KEY (ProjectId) REFERENCES dbo.Projects(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.AiAnalyses (
    Id                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiAnalyses PRIMARY KEY DEFAULT NEWID(),
    TestResultId       UNIQUEIDENTIFIER NULL,
    DefectId           UNIQUEIDENTIFIER NULL,
    Diagnosis          NVARCHAR(MAX)    NOT NULL,
    ProbableCause      NVARCHAR(MAX)    NOT NULL,
    Criticality        INT              NOT NULL,
    Recommendation     NVARCHAR(MAX)    NOT NULL,
    SuggestedPriority  INT              NOT NULL,
    EstimatedHours     DECIMAL(8,2)     NOT NULL,
    SuggestedOwnerRole NVARCHAR(50)     NOT NULL,
    ModelUsed          NVARCHAR(100)    NOT NULL,
    CreatedAt          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME()
);

/* ─────────────────────────── Integraciones y operación ─────────────────────────── */

CREATE TABLE dbo.IntegrationSettings (
    Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_IntegrationSettings PRIMARY KEY DEFAULT NEWID(),
    ProjectId      UNIQUEIDENTIFIER NOT NULL,
    Type           INT              NOT NULL,  -- 1=SonarQube 2=GitHub 3=ZAP 4=JMeter 5=Postman 6=Playwright 7=SqlServer
    BaseUrl        NVARCHAR(500)    NOT NULL,
    EncryptedToken NVARCHAR(MAX)    NULL,
    ExtraJson      NVARCHAR(MAX)    NULL,
    IsEnabled      BIT              NOT NULL DEFAULT 1,
    CreatedAt      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy      NVARCHAR(256)    NULL,
    UpdatedAt      DATETIME2        NULL,
    UpdatedBy      NVARCHAR(256)    NULL,
    IsDeleted      BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_IntegrationSettings_Projects FOREIGN KEY (ProjectId)
        REFERENCES dbo.Projects(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.NotificationChannels (
    Id        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NotificationChannels PRIMARY KEY DEFAULT NEWID(),
    ProjectId UNIQUEIDENTIFIER NULL,   -- NULL = canal global
    Channel   INT              NOT NULL,  -- 1=Email 2=Teams 3=Slack 4=Discord 5=Telegram
    Target    NVARCHAR(500)    NOT NULL,
    Events    INT              NOT NULL,  -- Flags: 1=PruebaFallida 2=Vulnerabilidad 4=DespliegueRechazado 8=RunCompletado 16=DefectoCreado
    IsEnabled BIT              NOT NULL DEFAULT 1,
    CreatedAt DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(256)    NULL,
    UpdatedAt DATETIME2        NULL,
    UpdatedBy NVARCHAR(256)    NULL,
    IsDeleted BIT              NOT NULL DEFAULT 0
);

CREATE TABLE dbo.PipelineExecutions (
    Id                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PipelineExecutions PRIMARY KEY DEFAULT NEWID(),
    ProjectId          UNIQUEIDENTIFIER NOT NULL,
    Provider           INT              NOT NULL,  -- 1=GitHubActions 2=AzureDevOps
    ExternalRunId      NVARCHAR(100)    NOT NULL,
    Branch             NVARCHAR(200)    NOT NULL,
    CommitSha          NVARCHAR(64)     NOT NULL,
    Url                NVARCHAR(500)    NULL,
    Status             INT              NOT NULL,
    StartedAt          DATETIME2        NOT NULL,
    FinishedAt         DATETIME2        NULL,
    DeploymentApproved BIT              NULL,
    CreatedAt          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy          NVARCHAR(256)    NULL,
    UpdatedAt          DATETIME2        NULL,
    UpdatedBy          NVARCHAR(256)    NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_PipelineExecutions_Projects FOREIGN KEY (ProjectId)
        REFERENCES dbo.Projects(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.VisualBaselines (
    Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_VisualBaselines PRIMARY KEY DEFAULT NEWID(),
    ProjectId        UNIQUEIDENTIFIER NOT NULL,
    BaselineKey      NVARCHAR(100)    NOT NULL,  -- id del caso de prueba o nombre del escenario visual
    BaselinePath     NVARCHAR(600)    NOT NULL,  -- ruta relativa de la imagen de referencia
    Width            INT              NOT NULL,
    Height           INT              NOT NULL,
    ThresholdPercent DECIMAL(6,4)     NOT NULL DEFAULT 0.10,  -- % máximo de píxeles distintos tolerado
    PixelTolerance   INT              NOT NULL DEFAULT 30,    -- |ΔR|+|ΔG|+|ΔB| a partir del cual cuenta como distinto
    CreatedAt        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy        NVARCHAR(256)    NULL,
    UpdatedAt        DATETIME2        NULL,
    UpdatedBy        NVARCHAR(256)    NULL,
    IsDeleted        BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_VisualBaselines_Projects FOREIGN KEY (ProjectId)
        REFERENCES dbo.Projects(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.DatabaseValidationRuns (
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DatabaseValidationRuns PRIMARY KEY DEFAULT NEWID(),
    ProjectId         UNIQUEIDENTIFIER NOT NULL,
    SourceEnvironment NVARCHAR(50)     NOT NULL,
    TargetEnvironment NVARCHAR(50)     NOT NULL,
    Status            INT              NOT NULL,
    StartedAt         DATETIME2        NOT NULL,
    CompletedAt       DATETIME2        NULL,
    DifferencesCount  INT              NOT NULL DEFAULT 0,
    DifferencesJson   NVARCHAR(MAX)    NULL,
    ErrorMessage      NVARCHAR(MAX)    NULL,
    CreatedAt         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy         NVARCHAR(256)    NULL,
    UpdatedAt         DATETIME2        NULL,
    UpdatedBy         NVARCHAR(256)    NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);
GO
