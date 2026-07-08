namespace QAGuardian.Domain.Common;

/// <summary>Violación de una regla de negocio del dominio.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

/// <summary>Recurso no encontrado.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string entity, object key)
        : base($"{entity} con identificador '{key}' no fue encontrado.") { }
}
