/* ============================================================================
   QA Guardian — Script 4/4: datos de ejemplo (opcional; ambientes demo/QA).
   ============================================================================ */
USE QAGuardian;
GO

IF EXISTS (SELECT 1 FROM dbo.Projects WHERE Code = N'DEMO') RETURN;

DECLARE @ProjectId UNIQUEIDENTIFIER = NEWID();
DECLARE @ModuleId  UNIQUEIDENTIFIER = NEWID();
DECLARE @ReqId     UNIQUEIDENTIFIER = NEWID();
DECLARE @StoryId   UNIQUEIDENTIFIER = NEWID();
DECLARE @Tc1       UNIQUEIDENTIFIER = NEWID();
DECLARE @Tc2       UNIQUEIDENTIFIER = NEWID();
DECLARE @GateId    UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.QualityGates WHERE IsDefault = 1);

INSERT INTO dbo.Projects (Id, Code, Name, Description, RepositoryUrl, QualityGateId)
VALUES (@ProjectId, N'DEMO', N'Proyecto Demostración',
        N'Proyecto de ejemplo con módulos, casos de prueba y una ejecución completa.',
        N'https://github.com/sumimedical/demo', @GateId);

INSERT INTO dbo.Modules (Id, ProjectId, Name, Description)
VALUES (@ModuleId, @ProjectId, N'Autenticación', N'Login, registro y recuperación de contraseña');

INSERT INTO dbo.Requirements (Id, ModuleId, Code, Title, Description)
VALUES (@ReqId, @ModuleId, N'REQ-001', N'Inicio de sesión seguro',
        N'El sistema debe autenticar usuarios con credenciales válidas y bloquear ataques de fuerza bruta.');

INSERT INTO dbo.UserStories (Id, RequirementId, Title, AcceptanceCriteria)
VALUES (@StoryId, @ReqId, N'Como usuario quiero iniciar sesión para acceder a mi cuenta',
        N'Dado un usuario registrado, cuando ingresa credenciales válidas, entonces accede al dashboard.');

INSERT INTO dbo.TestCases (Id, ProjectId, ModuleId, UserStoryId, Code, Title, Type, Priority, Status, Framework, AutomationScriptPath)
VALUES
    (@Tc1, @ProjectId, @ModuleId, @StoryId, N'TC-0001', N'Login con credenciales válidas', 1, 4, 2, 1, N'tests/e2e/login.spec.ts'),
    (@Tc2, @ProjectId, @ModuleId, @StoryId, N'TC-0002', N'Login bloquea tras 5 intentos fallidos', 5, 4, 2, 1, N'tests/e2e/lockout.spec.ts');

INSERT INTO dbo.TestSteps (Id, TestCaseId, [Order], Action, ExpectedResult) VALUES
    (NEWID(), @Tc1, 1, N'Navegar a /login', N'Se muestra el formulario de acceso'),
    (NEWID(), @Tc1, 2, N'Ingresar usuario y contraseña válidos', N'Se redirige al dashboard'),
    (NEWID(), @Tc2, 1, N'Intentar login con contraseña errónea 5 veces', N'La cuenta se bloquea 15 minutos');

-- Ejecución de ejemplo completada
DECLARE @RunId UNIQUEIDENTIFIER = NEWID();
DECLARE @Res1  UNIQUEIDENTIFIER = NEWID();
DECLARE @Res2  UNIQUEIDENTIFIER = NEWID();

INSERT INTO dbo.TestRuns (Id, ProjectId, RunType, Environment, Status, TriggeredBy, StartedAt, CompletedAt)
VALUES (@RunId, @ProjectId, 2, 2, 3, N'demo@qaguardian.local',
        DATEADD(MINUTE, -12, SYSUTCDATETIME()), DATEADD(MINUTE, -2, SYSUTCDATETIME()));

INSERT INTO dbo.TestResults (Id, TestRunId, TestCaseId, Name, Status, DurationMs) VALUES
    (@Res1, @RunId, @Tc1, N'Login con credenciales válidas', 1, 3200),
    (@Res2, @RunId, @Tc2, N'Login bloquea tras 5 intentos fallidos', 1, 8400);

INSERT INTO dbo.QualityGateEvaluations (Id, TestRunId, QualityGateId, Status, DetailsJson)
VALUES (NEWID(), @RunId, @GateId, 1,
        N'[{"Metric":1,"Operator":1,"Threshold":95,"ActualValue":100,"Satisfied":true,"IsBlocking":true}]');
GO
