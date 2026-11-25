# Azure Test Plans - Finexus

Sincroniza los tests de Playwright (`Tests/**`) con Azure DevOps Test Plans.

- Organizacion: `olimpiait` / Proyecto: `Olimpia Agil`
- Plan: `Test plan Automatizacion Finexus` (ID `273777`)

## Manuales

| Manual | Contenido |
|---|---|
| [Manual_Extraccion_Casos.md](Manual_Extraccion_Casos.md) | Mapeo de los casos de prueba (codigo, catalogo, Azure, mapeo) |
| [Manual_Ejecucion.md](Manual_Ejecucion.md) | Formas de ejecutar las pruebas y publicar resultados |

## Flujo general

`[Description("TC-XXX-##: titulo")]` -> `Get-TestCatalog` -> `Create-AzureTestCases` -> `AzureTestPlanMapping.json` -> `Validate` -> `Run-AndPublish` (.trx) -> `Publish-AzureTestPlan`

## Inicio rapido

```powershell
cd Playwright.Finexus\docs\AzureTestPlans
.\Create-AzureTestCases.ps1 -DryRun                      # simular creacion (sin PAT)
.\Run-AndPublish.ps1 -Filter AuthLoginTests              # ejecutar sin publicar
$env:AZURE_DEVOPS_PAT = '<token>'
.\Run-AndPublish.ps1 -Filter AuthLoginTests -Publish     # ejecutar y publicar
```

## Contenido de la carpeta

| Archivo | Funcion |
|---|---|
| `AzureTestPlanSettings.json` | Configuracion unica (organizacion, plan, discovery, datos de casos, suites) |
| `AzureTestPlanMapping.json` | ID funcional `TC-...` -> caso y suite de Azure |
| `Get-TestCatalog.ps1` | Genera el catalogo de casos desde `Tests` |
| `Create-AzureTestCases.ps1` | Crea/actualiza suites y casos en Azure |
| `Export-TestCases.ps1` | Exporta casos a XLSX (`Grouped/`) |
| `Validate-AzureTestPlanMapping.ps1` | Valida el mapeo contra el ensamblado compilado |
| `Run-AndPublish.ps1` | Compila, valida, ejecuta y opcionalmente publica |
| `Publish-AzureTestPlan.ps1` | Publica un .trx con evidencias |
| `AzureDevOpsClient.ps1` | Cliente REST de Azure DevOps |
| `TestMetadataExtractor/` | Extrae metadatos del ensamblado |
| `output/` | Salidas generadas (trx, extractor, logs) |

## Requisitos

- .NET 9 SDK y PowerShell.
- PAT de Azure DevOps en `AZURE_DEVOPS_PAT` (solo para crear casos o publicar).

## Reglas

- Las pruebas nunca publican solas: usar `-Publish` o `Publish-AzureTestPlan.ps1`.
- No guardar el PAT en ningun JSON.
- Usar `-DryRun` antes de operar contra Azure.
- No editar a mano la seccion `suites` de `AzureTestPlanSettings.json`.
