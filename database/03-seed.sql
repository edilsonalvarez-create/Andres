/* ============================================================================
   QA Guardian — Script 3/4: datos iniciales (roles, gate por defecto, admin).
   Nota: la contraseña del administrador se siembra desde la aplicación
   (hash BCrypt); este script solo prepara roles y quality gate.
   ============================================================================ */
USE QAGuardian;
GO

-- Roles del sistema
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = N'Administrador')
INSERT INTO dbo.Roles (Id, Name, Description) VALUES
    (NEWID(), N'Administrador', N'Acceso total a la plataforma'),
    (NEWID(), N'QA',            N'Gestión y ejecución de pruebas'),
    (NEWID(), N'Desarrollador', N'Consulta de resultados y corrección de defectos'),
    (NEWID(), N'LiderTecnico',  N'Supervisión técnica y aprobación de quality gates'),
    (NEWID(), N'DevOps',        N'Gestión de pipelines y despliegues'),
    (NEWID(), N'ProductOwner',  N'Consulta de dashboards y reportes'),
    (NEWID(), N'Auditor',       N'Consulta de auditoría y evidencias (solo lectura)'),
    (NEWID(), N'Cliente',       N'Consulta de reportes ejecutivos (solo lectura)');
GO

-- Quality gate por defecto
IF NOT EXISTS (SELECT 1 FROM dbo.QualityGates WHERE IsDefault = 1)
BEGIN
    DECLARE @GateId UNIQUEIDENTIFIER = NEWID();
    INSERT INTO dbo.QualityGates (Id, Name, IsDefault) VALUES (@GateId, N'Gate Estándar QA Guardian', 1);
    INSERT INTO dbo.QualityGateConditions (Id, QualityGateId, Metric, Operator, Threshold, IsBlocking) VALUES
        (NEWID(), @GateId, 1, 1, 95.0, 1),  -- % éxito >= 95 (bloqueante)
        (NEWID(), @GateId, 3, 3, 0.0,  1),  -- vulnerabilidades críticas == 0 (bloqueante)
        (NEWID(), @GateId, 4, 2, 2.0,  0);  -- vulnerabilidades altas <= 2 (advertencia)
END
GO
