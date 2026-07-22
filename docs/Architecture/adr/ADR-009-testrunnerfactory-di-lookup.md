# ADR-009: `TestRunnerFactory` resuelve por diccionario inyectado, no por `switch` + Service Locator

**Estado**: Aceptado · **Fecha**: 2026-07-16 · **Sprint**: 4

## Contexto

`TestRunnerFactory.Resolve(AutomationFramework)` usaba un `switch` que llamaba
`_services.GetRequiredService<TipoConcreto>()` por cada framework:

```csharp
public ITestRunner Resolve(AutomationFramework framework) => framework switch
{
    AutomationFramework.Playwright => _services.GetRequiredService<PlaywrightTestRunner>(),
    // ... 5 casos más
    _ => throw new NotSupportedException(...)
};
```

Esto viola el Principio Abierto/Cerrado (OCP): agregar un framework nuevo (ej. Cypress, k6)
exige editar esta clase además de crear el runner y registrarlo en DI. También usa el
anti-patrón *Service Locator* (`IServiceProvider` inyectado directamente): las dependencias
reales de la fábrica no son visibles en su constructor, solo descubribles leyendo el cuerpo
del método.

## Decisión

`TestRunnerFactory` recibe `IEnumerable<ITestRunner>` en el constructor (cada runner ya expone
`Framework` como propiedad de su contrato) y arma un diccionario indexado por ese valor:

```csharp
public TestRunnerFactory(IEnumerable<ITestRunner> runners)
    => _runnersByFramework = runners.ToDictionary(r => r.Framework);

public ITestRunner Resolve(AutomationFramework framework)
    => _runnersByFramework.TryGetValue(framework, out var runner)
        ? runner
        : throw new NotSupportedException($"Framework no soportado: {framework}");
```

Los 6 runners se registran como `ITestRunner` (no por tipo concreto) en
`DependencyInjection.cs`. Agregar un framework nuevo pasa a ser: implementar `ITestRunner` +
una línea de registro — cero cambios en esta clase.

## Alternativas consideradas

**Mantener el `switch` pero eliminar solo el Service Locator** (inyectar cada runner concreto
por constructor y mapear con `switch` sobre variables locales). Se descartó: seguiría
requiriendo editar la fábrica por cada runner nuevo, sin resolver la violación de OCP que es
el problema de fondo.

**Un `IDictionary<AutomationFramework, ITestRunner>` registrado directamente en DI** (en vez
de construirlo en el constructor de la fábrica). Se descartó por ser equivalente pero mover la
responsabilidad de armar el diccionario a `DependencyInjection.cs`, dispersando la lógica de
resolución fuera de la clase que la usa.

## Consecuencias

**Positivas**: cumple OCP (extensión sin modificación); las dependencias reales son explícitas
en el constructor; la clase ganó su primera cobertura de tests (`TestRunnerFactoryTests.cs`,
10 casos — no existía ninguno antes de este sprint).

**Negativas / trade-offs**: ninguna identificada — el cambio es estrictamente una mejora sin
costo de complejidad adicional (un diccionario es más simple de leer que un `switch` de 6
casos con Service Locator).

**Verificación**: `TestRunnerFactoryTests.cs` fija que agregar un framework a la colección
inyectada (simulado con `AutomationFramework.SqlValidator`, que antes no tenía runner) se
resuelve sin ningún cambio en la fábrica.
