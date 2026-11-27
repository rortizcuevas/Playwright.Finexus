# Azure Test Plans - Finexus

Sincroniza los tests de Playwright (`Tests/**`) con Azure DevOps Test Plans.

- Organizacion: `olimpiait` / Proyecto: `Olimpia Agil`
- Plan: `Test plan Automatizacion Finexus` (ID `273777`)

## Manuales

| Manual | Contenido |
|---|---|
| [Manual_Extraccion_Casos.md](../Manual_Extraccion_Casos.md) | Mapeo de los casos de prueba (codigo, catalogo, Azure, mapeo) |
| [Manual_Ejecucion.md](../Manual_Ejecucion.md) | Formas de ejecutar las pruebas y publicar resultados |

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

## Pipeline de GitHub Actions

El workflow [Run and publish Azure Test Plans](../../.github/workflows/run-and-publish.yml)
se inicia con cada push a `Prueba_pipeline`, nunca por un push a `main`. Compila con .NET 9,
instala Chromium, ejecuta todas las pruebas en modo headless y publica los resultados en el
plan configurado. No se puede iniciar manualmente desde Actions mientras el workflow no exista
en la rama predeterminada. Se serializan las ejecuciones para evitar pruebas simultaneas sobre
el mismo buzon; GitHub puede sustituir una ejecucion pendiente por otra mas reciente.

Antes del primer push, configurar en GitHub: **Settings > Secrets and variables > Actions >
Secrets > New repository secret**:

| Secret | Valor |
|---|---|
| `FINEXUS_WEB_API_URL` | URL base de la API de pruebas |
| `FINEXUS_WEB_APP_URL` | URL de la aplicacion de pruebas |
| `FINEXUS_API_USER` | Usuario de la API |
| `FINEXUS_API_PASSWORD` | Contrasena de la API |
| `FINEXUS_API_CLIENT_CODE` | Codigo de cliente de la API |
| `MAIL_USER` | Buzon IMAP que recibe los OTP |
| `MAIL_PASSWORD` | Contrasena de aplicacion del buzon Gmail |
| `AZURE_DEVOPS_PAT` | PAT de Azure DevOps con **Test Management: Read & write** y acceso al proyecto y plan configurados |

El PAT se crea en Azure DevOps, desde **User settings > Personal access tokens**. El buzon
debe tener IMAP habilitado y acceso a los mensajes OTP de los datos de prueba; las pruebas
mueven a la papelera los mensajes OTP anteriores. Las URL y el buzon deben ser accesibles
desde los runners hospedados de GitHub; si la red es privada, se necesita un runner
autohospedado con acceso a ella y ajustar `runs-on`. `GITHUB_TOKEN` lo proporciona Actions
automaticamente con permiso de lectura; no hay que crear otro token de GitHub.

El workflow crea `playwright.runsettings` a partir del ejemplo sin escribir las contrasenas
en el archivo. Tras cada ejecucion, Actions conserva el TRX y el registro de publicacion
durante siete dias, incluso si hay un fallo. Estos archivos pueden contener datos de prueba:
restringir el acceso al repositorio y a sus artefactos. Las evidencias seleccionadas se
publican en Azure Test Plans. Si falla la publicacion o alguna prueba, el job falla; una
publicacion interrumpida puede ser parcial y el registro permite reanudarla con `-ResumeLog`.

## Reglas

- Las pruebas nunca publican solas: usar `-Publish` o `Publish-AzureTestPlan.ps1`.
- No guardar el PAT en ningun JSON.
- Usar `-DryRun` antes de operar contra Azure.
- No editar a mano la seccion `suites` de `AzureTestPlanSettings.json`.
