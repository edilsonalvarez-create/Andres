using FluentValidation.Results;

namespace QAGuardian.Application.Common.Exceptions;

/// <summary>Agrupa errores de validación de FluentValidation para respuesta HTTP 400.</summary>
public class ValidationException : Exception
{
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("Se produjeron uno o más errores de validación.")
    {
        Errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());
    }

    public IDictionary<string, string[]> Errors { get; }
}

/// <summary>Operación no permitida para el usuario actual.</summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message = "No tiene permisos para realizar esta operación.")
        : base(message) { }
}
