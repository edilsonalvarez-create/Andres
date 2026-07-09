namespace QAGuardian.Domain.Enums;

public enum TestType
{
    Functional = 1,
    Regression = 2,
    Api = 3,
    Performance = 4,
    Security = 5,
    Visual = 6,
    Database = 7,
    Smoke = 8,
    Unit = 9,
    Integration = 10,
    EndToEnd = 11,
    Component = 12
}

public enum AutomationFramework
{
    Manual = 0,
    Playwright = 1,
    Postman = 2,
    JMeter = 3,
    OwaspZap = 4,
    SqlValidator = 5,
    VisualRegression = 6
}

public enum TestCaseStatus
{
    Draft = 1,
    Active = 2,
    Deprecated = 3
}

public enum TestPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum RunStatus
{
    Pending = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

public enum ResultStatus
{
    Passed = 1,
    Failed = 2,
    Skipped = 3,
    Blocked = 4,
    Flaky = 5
}

public enum EnvironmentType
{
    Development = 1,
    QA = 2,
    Staging = 3,
    Production = 4
}

public enum EvidenceType
{
    Screenshot = 1,
    Video = 2,
    Log = 3,
    HtmlReport = 4,
    PdfReport = 5,
    JsonReport = 6,
    XmlReport = 7,
    HarFile = 8
}

public enum DefectSeverity
{
    Trivial = 1,
    Minor = 2,
    Major = 3,
    Critical = 4,
    Blocker = 5
}

public enum DefectPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4
}

public enum DefectStatus
{
    New = 1,
    Assigned = 2,
    InProgress = 3,
    Resolved = 4,
    Verified = 5,
    Closed = 6,
    Reopened = 7,
    Rejected = 8
}

public enum QualityGateStatus
{
    Passed = 1,
    Warning = 2,
    Failed = 3
}

public enum GateMetric
{
    PassRatePercent = 1,
    CoveragePercent = 2,
    CriticalVulnerabilities = 3,
    HighVulnerabilities = 4,
    Bugs = 5,
    CodeSmells = 6,
    DuplicationPercent = 7,
    SecurityHotspots = 8,
    AvgResponseTimeMs = 9,
    ErrorRatePercent = 10,
    CriticalDefectsOpen = 11
}

public enum GateOperator
{
    GreaterOrEqual = 1,
    LessOrEqual = 2,
    Equal = 3
}

public enum RiskLevel
{
    Informational = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum PipelineProvider
{
    GitHubActions = 1,
    AzureDevOps = 2
}

public enum NotificationChannel
{
    Email = 1,
    MicrosoftTeams = 2,
    Slack = 3,
    Discord = 4,
    Telegram = 5
}

[Flags]
public enum NotificationEvents
{
    None = 0,
    TestFailed = 1,
    VulnerabilityFound = 2,
    DeploymentRejected = 4,
    RunCompleted = 8,
    DefectCreated = 16,
    All = TestFailed | VulnerabilityFound | DeploymentRejected | RunCompleted | DefectCreated
}

public enum IntegrationType
{
    SonarQube = 1,
    GitHub = 2,
    OwaspZap = 3,
    JMeter = 4,
    Postman = 5,
    Playwright = 6,
    SqlServer = 7
}
