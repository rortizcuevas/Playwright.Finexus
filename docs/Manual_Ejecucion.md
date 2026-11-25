# Manual: formas de ejecucion

Las pruebas nunca publican por si solas: la publicacion en Azure es siempre explicita.

## 1. Resumen

| Modo | Comando | Publica |
|---|---|---|
| A. dotnet test / Test Explorer | `dotnet test --filter "FullyQualifiedName~AuthLoginTests"` | No |
| B. Run-AndPublish | `.\Run-AndPublish.ps1 -Filter AuthLoginTests` | No |
| C. Run-AndPublish con publicacion | `.\Run-AndPublish.ps1 -Filter AuthLoginTests -Publish` | Si |
| D. Publicar un .trx existente | `.\Publish-AzureTestPlan.ps1 -TrxPath <trx> -AssemblyPath <dll>` | Si |
| E. Simular publicacion | igual que D con `-DryRun` | No |

## 2. Modo A: dotnet test

```powershell
dotnet test Playwright.Finexus.csproj --settings playwright.runsettings --filter "FullyQualifiedName~AuthLoginTests"
```

Tambien desde el Test Explorer de Visual Studio.

## 3. Modos B y C: Run-AndPublish.ps1

Parametros: `-Filter` (lista; se une como `FullyQualifiedName~x|y`; sin filtro ejecuta todo), `-CasePrefix` (limita el alcance por prefijo de ID), `-Configuration` (Debug por defecto), `-Publish`.

Pasos: compila, valida el mapeo, genera un runsettings temporal con `captureStepEvidence=true`, ejecuta `dotnet test --no-build` generando `output/trx/run-<fecha>.trx`, y solo con `-Publish` llama a Publish-AzureTestPlan.ps1. El codigo de salida es el de `dotnet test`.

```powershell
.\Run-AndPublish.ps1
.\Run-AndPublish.ps1 -Filter AuthLoginTests,SigningPdfValidE2ETests
.\Run-AndPublish.ps1 -CasePrefix TC-AUTH- -Publish
```

## 4. Modos D y E: Publish-AzureTestPlan.ps1

```powershell
$env:AZURE_DEVOPS_PAT = '<token>'
.\Publish-AzureTestPlan.ps1 -TrxPath output\trx\run-XXXX.trx -AssemblyPath ..\..\bin\Debug\net9.0\Playwright.Finexus.dll -DryRun
.\Publish-AzureTestPlan.ps1 -TrxPath output\trx\run-XXXX.trx -AssemblyPath ..\..\bin\Debug\net9.0\Playwright.Finexus.dll
```

Parametros: `-TrxPath` y `-AssemblyPath` (obligatorios), `-SettingsPath`, `-MappingPath`, `-ExtractorDll`, `-EvidencePath`, `-CasePrefix`, `-ResultPolicy Abort|Last|First` (Abort por defecto), `-Token`, `-TokenVariable` (AZURE_DEVOPS_PAT), `-RunName`, `-ResumeLog`, `-DryRun`, `-Force`.

- Resultados: Passed -> Passed; Failed/Error/Timeout/Aborted -> Failed; Inconclusive -> Inconclusive; el resto se omite.
- Si un ID no se resuelve o no esta en el mapeo, aborta. Con varios resultados del mismo caso aborta salvo `-ResultPolicy Last|First`.
- Evidencias: por caso se adjunta un zip (seguimiento.zip), un video (.webm) y una captura (failure.png) desde `Evidence/<metodo>/`.

## 5. Orden recomendado

1. Validar el mapeo y ejecutar (modo B).
2. Revisar el .trx.
3. Simular la publicacion (modo E).
4. Publicar (modo C o D).

## 6. Errores frecuentes

| Error | Causa |
|---|---|
| La validacion previa ha fallado | Mapeo incompleto o IDs duplicados; ver el manual de mapeo |
| Falta el PAT | Definir `AZURE_DEVOPS_PAT` o usar `-Token` |
| Varios resultados para un caso | Usar `-ResultPolicy Last` o `First` |
| El .trx no contiene resultados publicables | Revisar `-CasePrefix` y el filtro |

Mapeo de casos: [Manual_Extraccion_Casos.md](Manual_Extraccion_Casos.md).
