---
applyTo: "**/*.cs,**/*.json,**/*.csproj"
---

# Arquitectura - Framework Playwright .NET

Reglas genericas reutilizables por cualquier proyecto Playwright .NET + MSTest (solo API, solo Web, o hibrido con E2E). Las particularidades de cada proyecto se documentan en `project.instructions.md`, que prevalece ante conflicto.

## Principios

- Aplicar SOLID (SRP, OCP, LSP, DIP e ISP).
- Cada clase de pruebas debe heredar de `TestBase` (Web/E2E) o de `ApiTestBase` (solo API, sin navegador).
- Cada Page Object debe heredar de `PageBase` e implementar `IPageBase`.
- Cada cliente API debe heredar de `ApiClientBase` e implementar su interfaz `I<Module>Client`.
- Las pruebas no construyen `HttpClient`, URLs ni JSON: consumen clientes de `Api/Clients/`.
- Reutilizar los helpers proporcionados por las clases base; no duplicar su comportamiento.
- Mantener separados clientes API, Page Objects, pruebas, datos y archivos de prueba.

## Estructura del proyecto

```text
<Proyecto>/
+-- Api/
|   +-- Base/
|   |   +-- ApiClientBase.cs          (HttpClient, Bearer, serializacion BaseResponse<T>)
|   |   +-- ApiCallRecorder.cs        (api-calls.json/.html sanitizados)
|   +-- Clients/<Module>Client.cs
|   +-- Models/
|       +-- Requests/
|       +-- Responses/                (BaseResponse<T> y DTOs del Swagger)
+-- Base/
|   +-- ApiTestBase.cs
|   +-- PageBase.cs
|   +-- TestBase.cs
|   +-- EvidenceComposer.cs           (flow.png)
+-- Data/
|   +-- <Module>Data.json
+-- Docs/
|   +-- CasosDePrueba.md
|   +-- AzureTestPlans/
+-- Evidence/                         (generada en ejecucion; no se versiona)
+-- Helpers/
|   +-- DataHelpers.cs
|   +-- FileHelper.cs                 (lectura de TestFiles)
|   +-- TokenProvider.cs              (login unico y cache del token; solo API)
+-- Interfaces/
|   +-- IDataHelper.cs
|   +-- IPageBase.cs
|   +-- I<Module>Client.cs
+-- Pages/
|   +-- <Module>/<Name>Page.cs
+-- Reports/
|   +-- extent-report.ps1
|   +-- Publish-AzureTestPlan.ps1
+-- TestFiles/                        (archivos de entrada para las pruebas; opcional)
+-- Tests/
|   +-- [<Capa>/]<Module>/[<Operation>/]<Module><Operation>Tests.cs
|   +-- [E2E/<Flow>E2ETests.cs]       (opcional)
+-- Traceability/
+-- playwright.runsettings
+-- <Proyecto>.csproj
```

Reglas:

- `Api/`, `Pages/` y `Tests/E2E/` son opcionales: incluir solo las capas que el proyecto use.
- Proyecto de una sola capa: `Tests/<Module>/[<Operation>/]`. Proyecto mixto: `Tests/<Capa>/<Module>/[<Operation>/]` con `<Capa>` = `Api`, `Web`, `E2E`.
- La subcarpeta `<Operation>` (ej: `Consultar`, `Cargar`) se usa cuando una operacion tiene mas de una clase de pruebas.
- No agrupar operaciones distintas (iniciar, consultar, emitir, descargar) en una sola clase de pruebas.
- Cada archivo y clase de pruebas debe identificar modulo y operacion en ingles (ej: `<Module><Operation>Tests.cs`).
- Namespaces sin submodulo: `<Proyecto>.Api`, `<Proyecto>.Pages`, `<Proyecto>.Tests`.
- Los archivos de `TestFiles/` se registran con `CopyToOutputDirectory=PreserveNewest`.

## Clientes API

- El contrato fuente (Postman, Swagger u otro) se define en `project.instructions.md`.
- Cada metodo del cliente corresponde a un endpoint del contrato fuente.
- El token se obtiene una sola vez y se reutiliza; las pruebas de seguridad (sin token, token invalido) usan un cliente sin autenticacion explicito.
- Las rutas de endpoint se declaran como `private const string` en cada cliente.
- Toda llamada HTTP debe pasar por `ApiClientBase`, que registra automaticamente request/response sanitizados; no registrar manualmente desde los tests.

## Page Objects

- Ubicar cada Page Object en `Pages/<Module>/`.
- Declarar los selectores CSS como constantes `private const string` y exponerlos mediante propiedades computadas `private ILocator`.
- No usar directamente el string del selector en los metodos.
- No usar `Thread.Sleep`; utilizar `ILocator.WaitForAsync`, `WaitForSelectorAsync`, `Page.WaitForURLAsync` u otras esperas de Playwright.
- Preferir `ILocator.WaitForAsync` cuando ya exista una propiedad `ILocator`; usar `WaitForSelectorAsync` cuando se trabaje con el selector CSS constante.
- Reutilizar los helpers heredados de `PageBase` cuando correspondan, sin duplicar su lógica:

| Metodo                                    | Descripcion                                                           |
|-------------------------------------------|-----------------------------------------------------------------------|
| `WaitForSelectorAsync(selector, timeout)` | Espera un selector visible; el timeout predeterminado es 30 000 ms.   |
| `TakeScreenshotAsync(name)`               | Captura en `Evidence/<Method>/<DisplayName-slug>/<name>.png` y la registra con `TestContext.AddResultFile`. |
| `NavigateAsync()`                         | Navega a `BaseUrl`; debe sobrescribirse para esperar la carga final.  |

Cada Page Object debe sobrescribir `NavigateAsync()` según el tipo de navegación.

Navegacion directa por URL:

```csharp
public override async Task NavigateAsync()
{
    await base.NavigateAsync();
    await UsuarioInput.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
}
```

Navegacion mediante menu lateral:

```csharp
public override async Task NavigateAsync()
{
    await Page.Locator(LnkModulo_Link).ClickAsync();
    await WaitForSelectorAsync(SelectorContenidoPrincipal);
}
```

## Clases de pruebas

- Ubicar cada clase en `Tests/<Capa>/<Module>/`; excepcion: los flujos E2E van directamente en `Tests/E2E/`.
- Organizar cada test con Arrange, Act y Assert y sus comentarios explicitos.
- Envolver cada accion web o API en `ExtentStepAsync`; no invocar directamente acciones de Page Objects desde el test.
- Acumular los fallos en `List<string>` antes de realizar un unico `Assert.AreEqual`.
- Todo `[DataRow]` debe declarar `DisplayName` de forma obligatoria (opcion B de Azure Test Plans: un Test Case por fila):
  - Unico dentro del metodo, descriptivo, estable y sin emojis.
  - No basarlo en el indice de la fila ni en valores volatiles (fechas, GUID, aleatorios).
  - No modificarlo una vez sincronizado: es la identidad de la fila en Azure (`CaseKey` = clave del metodo + `DisplayName`); cambiarlo crea un caso nuevo y deja el anterior huerfano.
  - `[Description("TC-MODULE-##: ...")]` se mantiene a nivel de metodo como referencia base; `DisplayName` diferencia cada fila.
  - `[DynamicData]` debe definir `DynamicDataDisplayName` con el mismo criterio.

```csharp
[TestMethod]
[DataRow(12, DisplayName = "PDF 12 MB - supera el limite de 10 MB")]
[Description("TC-DOCUMENT-08: Carga de PDF de 12 MB (supera el limite de 10 MB)")]
public async Task Issue_WithGeneratedPdf_ReportsResult(int megabytes)
```
- Separar visualmente el setup y los tests:

```csharp
// ── Setup ──────────────────────────────────────────────────────
// ── Tests ──────────────────────────────────────────────────────
```

## Ciclo de vida de TestBase

1. `ApiTestBase.InitializeAsync` (`[TestInitialize]`, no virtual) crea la entrada de ExtentReports y llama a `SetupAsync`. `ApiTestBase.CleanupAsync` (`[TestCleanup]`, no virtual) llama a `TearDownAsync` y registra el resultado. No sobrescribirlos.
(MSTest ejecutaria dos veces un hook virtual sobrescrito).
3. `TearDownAsync`
4. Evidencias: carpeta
5. `ApiClientBase` registra

```csharp
protected override async Task SetupAsync()
{
    await base.SetupAsync();
    _miPage = new MiPage(Page, WebAppUrl);
}
```

el header `Accept-Language` definido en `project.instructions.md`, evidencias

Propiedades protegidas disponibles:

| Propiedad             | Tipo          | Parametro en `.runsettings` |
|-----------------------|---------------|-----------------------------|
| `RunSettingsBrowser`  | `string`      | `browser`                   |
| `Headless`            | `bool`        | `headless`                  |
| `SlowMo`              | `float`       | `slowMo`                    |
| `EvidenceLevel`       | `string`      | `evidenceLevel` (`OnFailure`) |
| `CaptureTrace`        | `string`      | `captureTrace` (`OnFailure`) |
| `CaptureVideo`        | `bool`        | `captureVideo` (`true`)     |
| `MaxEvidenceMbPerTest`| `int`         | `maxEvidenceMbPerTest`      |
| `DataHelper`          | `IDataHelper` | -                           |

Los parametros propios del proyecto (URLs, credenciales, etc.) se documentan en `project.instructions.md`.

## Datos de prueba

- Crear un unico archivo `Data/<Module>Data.json` por modulo.
- Obtener todos los datos mediante `IDataHelper`; no declarar valores de prueba directamente en las clases.
- Resolver la ruta desde cada clase de pruebas con `Path.Combine(AppContext.BaseDirectory, "Data/<Module>Data.json")`; no usar runsettings para rutas de datos.
- Cada clave raiz corresponde al `testName` enviado a `ReadJsonData` y debe contener un array, incluso para un solo caso.
- Los arrays anidados se serializan como JSON string en el `DataTable`.

```json
{
  "_comentario": "Datos de prueba para el modulo X.",
  "nombreSeccion": [
    { "campo1": "valor1", "campo2": "valor2" }
  ]
}
```

Patron de consumo:

```csharp
var datos = DataHelper.ReadJsonData(ModuloDataPath, "nombreSeccion");
string valor = datos.Rows[0]["campo1"].ToString()!;
foreach (DataRow fila in datos.Rows) { ... }
```

Registrar cada archivo nuevo en el proyecto:

```xml
<None Update="Data\<Module>Data.json">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

## Tests E2E (opcional)

- Preparar el escenario por API, ejecutar la accion en Web y validar el resultado final nuevamente por API.
- El flujo concreto de cada proyecto se documenta en `project.instructions.md`.
- Envolver cada llamada API y cada accion web en `ExtentStepAsync`.
- Los secretos (contrasena, token) solo se leen de `.runsettings` o variables de entorno; nunca en `Data/*.json`.

```csharp
var (http, body) = await ExtentStepAsync("<Descripcion del paso>", () => _moduleClient.OperationAsync(request));
```
