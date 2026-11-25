# Guia: nuevo proyecto compatible con ExtentReports y Azure Test Plans

## 1. Dependencias (.NET 9 / C# 13)
- `MSTest.TestFramework`, `MSTest.TestAdapter`, `Microsoft.NET.Test.Sdk`
- `Microsoft.Playwright.MSTest`
- `ExtentReports`
- `SixLabors.ImageSharp` (composicion de `flow.png`)

## 2. Estructura
Las capas `Api/`, `Pages/` y `Tests/E2E/` son opcionales; incluir solo las que use el proyecto.
```
<Proyecto>/
  Api/Base/ApiClientBase.cs         (solo API)
  Api/Base/ApiCallRecorder.cs       (solo API)
  Api/Clients/<Module>Client.cs     (solo API)
  Base/ApiTestBase.cs
  Base/TestBase.cs                  (solo Web/E2E)
  Base/EvidenceComposer.cs
  Base/PageBase.cs                  (solo Web)
  Interfaces/IPageBase.cs           (solo Web)
  Pages/<Module>/<Page>.cs          (solo Web)
  Tests/<Module>/[<Operation>/]                 (una sola capa)
  Tests/<Capa>/<Module>/[<Operation>/]          (mixto: Api | Web | E2E)
  Data/<Module>Data.json
  Reports/extent-report.ps1
  Reports/Publish-AzureTestPlan.ps1
  <Proyecto>.runsettings
  copilot-instructions.md
  architecture.instructions.md      (generico, se copia sin cambios)
  project.instructions.md           (especifico del proyecto; prevalece)
```

## 3. Responsabilidades de las clases base
| Clase | Responsabilidad |
|---|---|
| `ApiTestBase` | Entrada ExtentReports, `SetupAsync`/`TearDownAsync`, carpeta `Evidence/<Method>/<DisplayName-slug>/`, `resumen.md`/`diagnostico.md`, `TestContext.AddResultFile`. |
| `TestBase` | Navegador, viewport 1920x1080, video (activo por defecto), tracing (guardado solo en fallo), `failure.png`, `console.log`. |
| `PageBase` | Esperas, navegacion y capturas comunes; no crea pasos (el test envuelve cada accion en `ExtentStepAsync`). |
| `ApiClientBase` | HTTP, log y registro en `ApiCallRecorder`. |
| `ApiCallRecorder` | `api-calls.json`/`.html`; enmascara `Authorization` y token de login; trunca Base64. |
| `EvidenceComposer` | Mosaico numerado `flow.png`, paso fallido con borde rojo. |
| `Publish-AzureTestPlan.ps1` | Publica resultados y adjunta solo los `ResultFiles` del `.trx`. |

## 4. `.runsettings`
```xml
<RunSettings>
  <TestRunParameters>
	<!-- Parametros propios del proyecto (URLs, credenciales...): documentarlos en project.instructions.md -->
	<Parameter name="browser"
	<Parameter name="headless" value="true" />
	<Parameter name="slowMo" value="0" />
	<Parameter name="evidenceLevel" value="OnFailure" />
	<Parameter name="captureTrace" value="OnFailure" />
	<Parameter name="captureVideo" value="true" />
	<Parameter name="maxEvidenceMbPerTest" value="5" />
  </TestRunParameters>
</RunSettings>
```
No inventar valores pendientes; registrarlos como pendientes en `project.instructions.md`.

## 5. Convenciones de tests
- Nombre `Action_Condition_ExpectedResult`.
- `[Description("TC-MODULE-##: ...")]` y `[TestCategory("Module")]`.
- `[DataRow]` siempre con `DisplayName` unico y estable (cada fila es un Test Case en Azure).
- Sin `[TestInitialize]`/`[TestCleanup]` en las clases de prueba; usar `SetupAsync`/`TearDownAsync`.
- Pasos con `ExtentStepAsync`; comentarios `// Arrange`, `// Act`, `// Assert`.

## 6. Matriz de evidencias
| Adjunto | Correcto | Fallido |
|---|---|---|
| `resumen.md` | Si | - |
| `diagnostico.md` | - | Si |
| `api-calls.json`/`.html` | Resumen | Completo |
| `flow.png` | Si `evidenceLevel=Full` | Si |
| `failure.png` | - | Si |
| `trace.zip` | - | Si |
| `console.log` | - | Si |
| `video.webm` | Si (`captureVideo=true`) | Si (`captureVideo=true`) |

## 7. Verificacion
1. `dotnet build` sin errores.
2. `dotnet test --settings <Proyecto>.runsettings --logger trx`.
3. Comprobar `ResultFiles` en el `.trx` y la carpeta `Evidence/`.
4. Ejecutar `Reports/extent-report.ps1` y revisar el reporte.
5. Ejecutar `Reports/Publish-AzureTestPlan.ps1` y verificar adjuntos en Azure Test Plans.
