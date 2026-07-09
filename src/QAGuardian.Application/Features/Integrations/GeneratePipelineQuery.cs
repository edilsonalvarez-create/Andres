using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;

namespace QAGuardian.Application.Features.Integrations;

public record GeneratedPipelineDto(string FileName, string Yaml);

/// <summary>
/// Genera automáticamente el pipeline de GitHub Actions de un proyecto con el flujo
/// Build → Unit Test → Playwright → API Test → Security → Performance → Deploy Staging
/// → Smoke Test → Producción, cableado contra la API de QA Guardian como quality gate.
/// </summary>
public record GenerateGitHubActionsPipelineQuery(Guid ProjectId) : IRequest<GeneratedPipelineDto>;

public class GenerateGitHubActionsPipelineQueryHandler
    : IRequestHandler<GenerateGitHubActionsPipelineQuery, GeneratedPipelineDto>
{
    private readonly IProjectRepository _projects;

    public GenerateGitHubActionsPipelineQueryHandler(IProjectRepository projects) => _projects = projects;

    public async Task<GeneratedPipelineDto> Handle(GenerateGitHubActionsPipelineQuery request, CancellationToken ct)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), request.ProjectId);

        var yaml = BuildYaml(project);
        return new GeneratedPipelineDto($"qa-guardian-{project.Code.ToLowerInvariant()}.yml", yaml);
    }

    private static string BuildYaml(Project project)
    {
        // Se usan placeholders + Replace para no colisionar con la sintaxis ${{ }} de
        // GitHub Actions. Los tipos de ejecución: 1=Funcional 3=API 5=Seguridad 4=Rendimiento 8=Smoke.
        const string template = """
            # ============================================================================
            # QA Guardian — Quality Gate para el proyecto __PROJECT_NAME__ (__PROJECT_CODE__)
            # Generado automáticamente por QA Guardian.
            # Secrets requeridos en el repositorio:
            #   QAGUARDIAN_URL, QAGUARDIAN_TOKEN
            # ============================================================================
            name: Quality Gate — __PROJECT_CODE__

            on:
              push:
                branches: [main]
              pull_request:

            env:
              QAG: ${{ secrets.QAGUARDIAN_URL }}
              QAG_PROJECT: "__PROJECT_ID__"
              QAG_TOKEN: ${{ secrets.QAGUARDIAN_TOKEN }}

            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@v4
                  - name: Build
                    run: echo "Reemplace con el build real del proyecto"

              unit-test:
                needs: build
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@v4
                  - name: Unit Test
                    run: echo "Reemplace con las pruebas unitarias del proyecto"

              qa-guardian-gate:
                name: "QA Guardian: Playwright + API + Security + Performance"
                needs: unit-test
                runs-on: ubuntu-latest
                strategy:
                  fail-fast: true
                  matrix:
                    # 1=Funcional/Playwright  3=API  5=Seguridad  4=Rendimiento
                    runType: [1, 3, 5, 4]
                steps:
                  - name: Disparar ejecución en QA Guardian
                    id: trigger
                    run: |
                      RUN_ID=$(curl -fsS -X POST "$QAG/api/v1/testruns" \
                        -H "Authorization: Bearer $QAG_TOKEN" \
                        -H "Content-Type: application/json" \
                        -d "{\"projectId\":\"$QAG_PROJECT\",\"runType\":${{ matrix.runType }},\"environment\":2,\"commitSha\":\"${{ github.sha }}\"}" \
                        | jq -r '.id')
                      echo "runId=$RUN_ID" >> "$GITHUB_OUTPUT"
                  - name: Esperar veredicto del quality gate
                    run: |
                      for i in $(seq 1 60); do
                        RUN=$(curl -fsS "$QAG/api/v1/testruns/${{ steps.trigger.outputs.runId }}" -H "Authorization: Bearer $QAG_TOKEN")
                        STATUS=$(echo "$RUN" | jq -r '.run.status')
                        if [ "$STATUS" = "3" ]; then
                          APPROVED=$(echo "$RUN" | jq -r '.run.deploymentApproved')
                          echo "Quality Gate: $(echo "$RUN" | jq -r '.run.gateStatus') (aprobado: $APPROVED)"
                          [ "$APPROVED" != "false" ] && exit 0 || { echo "::error::Despliegue bloqueado por QA Guardian"; exit 1; }
                        elif [ "$STATUS" = "4" ] || [ "$STATUS" = "5" ]; then
                          echo "::error::La ejecución falló o fue cancelada"; exit 1
                        fi
                        sleep 30
                      done
                      echo "::error::Timeout esperando a QA Guardian"; exit 1

              deploy-staging:
                needs: qa-guardian-gate
                if: github.ref == 'refs/heads/main'
                runs-on: ubuntu-latest
                environment: staging
                steps:
                  - name: Deploy Staging
                    run: echo "Reemplace con el despliegue real a staging"

              smoke-test:
                needs: deploy-staging
                runs-on: ubuntu-latest
                steps:
                  - name: Smoke Test en Staging
                    run: |
                      RUN_ID=$(curl -fsS -X POST "$QAG/api/v1/testruns" \
                        -H "Authorization: Bearer $QAG_TOKEN" -H "Content-Type: application/json" \
                        -d "{\"projectId\":\"$QAG_PROJECT\",\"runType\":8,\"environment\":3,\"commitSha\":\"${{ github.sha }}\"}" \
                        | jq -r '.id')
                      for i in $(seq 1 30); do
                        RUN=$(curl -fsS "$QAG/api/v1/testruns/$RUN_ID" -H "Authorization: Bearer $QAG_TOKEN")
                        STATUS=$(echo "$RUN" | jq -r '.run.status')
                        [ "$STATUS" = "3" ] && exit $([ "$(echo "$RUN" | jq -r '.run.failed')" = "0" ] && echo 0 || echo 1)
                        { [ "$STATUS" = "4" ] || [ "$STATUS" = "5" ]; } && exit 1
                        sleep 20
                      done
                      exit 1

              deploy-production:
                needs: smoke-test
                runs-on: ubuntu-latest
                environment: production
                steps:
                  - name: Deploy Producción
                    run: echo "Reemplace con el despliegue real a producción"

            """;

        return template
            .Replace("__PROJECT_NAME__", project.Name)
            .Replace("__PROJECT_CODE__", project.Code)
            .Replace("__PROJECT_ID__", project.Id.ToString());
    }
}
