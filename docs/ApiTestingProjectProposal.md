# Propuesta: proyecto de pruebas de API - Playwright.PortalBackoffice.Api

## 1. Contexto y objetivo

Hoy la lógica de API vive en `Helpers/DocumentApiClient.cs` del proyecto de UI y se usa solo para preparar o limpiar datos. El objetivo es cubrir el 100 % de los endpoints del Backoffice con pruebas automatizadas de API. Se medirán el código de estado, el contrato (esquema) y las reglas de negocio de cada endpoint.

**Decisión:** crear un proyecto independiente, `Playwright.PortalBackoffice.Api`, y extraer el código común a una librería compartida, `Playwright.PortalBackoffice.Common`.

```text
Playwright.PortalBackoffice.slnx
+-- Playwright.PortalBackoffice          (UI E2E - existente)
+-- Playwright.PortalBackoffice.Api      (API - nuevo)
+-- Playwright.PortalBackoffice.Common   (compartido - nuevo, opcional en fase 1)
```

| Criterio | Beneficio |
|---|---|
| Velocidad | No arranca navegador; la suite completa se ejecuta en segundos o pocos minutos. |
| Pipeline | Se ejecuta como etapa previa a la UI (fail-fast). |
| Responsabilidad | Separa la UI de los contratos de API (SRP a nivel de proyecto). |
| Reutilización | La UI consume los clientes API para crear y limpiar datos (setup y teardown). |

---

## 2. Estructura propuesta

```text
Playwright.PortalBackoffice.Common/
+-- Configuration/
|   +-- TestSettings.cs              # Lectura tipada de TestRunParameters
+-- Helpers/
|   +-- DataHelpers.cs               # Movido desde el proyecto UI
|   +-- ExtentReportManager.cs       # Movido desde el proyecto UI
+-- Interfaces/
	+-- IDataHelper.cs

Playwright.PortalBackoffice.Api/
+-- Base/
|   +-- ApiTestBase.cs               # Ciclo de vida, ExtentStepAsync, clientes
|   +-- ApiClientBase.cs             # HttpClient, auth, serialización, logging
+-- Clients/
|   +-- Auth/AuthApiClient.cs
|   +-- Documents/DocumentApiClient.cs
|   +-- Images/ImageApiClient.cs
+-- Models/
|   +-- ApiResponse.cs               # Sobre genérico { data, mensaje, ... }
|   +-- ApiResult.cs                 # StatusCode + Body + Data tipada + tiempo
|   +-- Requests/<Module>/...
|   +-- Responses/<Module>/...
+-- Interfaces/
|   +-- IApiClient.cs
|   +-- ITokenProvider.cs
+-- Helpers/
|   +-- TokenProvider.cs             # Caché de token por ejecución
|   +-- SchemaValidator.cs           # Validación JSON Schema
|   +-- ApiAssertions.cs             # Helpers que acumulan fallos
+-- Schemas/
|   +-- <Module>/<Operation>Response.schema.json
+-- Data/
|   +-- <Module>ApiData.json
+-- Tests/
|   +-- <Module>/<Module><Operation>ApiTests.cs
+-- Traceability/
|   +-- TC-API-<MODULE>-##.json
+-- Reports/
|   +-- extent-report.ps1
+-- Coverage/
|   +-- EndpointCoverageMatrix.md
+-- api.runsettings
+-- Playwright.PortalBackoffice.Api.csproj
```

Namespaces planos, alineados con el proyecto actual:
- `Playwright.PortalBackoffice.Api.Clients`
- `Playwright.PortalBackoffice.Api.Models`
- `Playwright.PortalBackoffice.Api.Tests`

---

## 3. Mejoras de arquitectura

| # | Situación actual | Mejora propuesta | Principio |
|---|---|---|---|
| 1 | `DocumentApiClient` mezcla login, borrado de documentos y borrado de imágenes. | Un cliente por recurso, cada uno heredando de `ApiClientBase`. | SRP / OCP |
| 2 | Se hace login en cada uso. | `ITokenProvider` con caché y renovación: un solo login por ejecución (`[AssemblyInitialize]`). | DIP / rendimiento |
| 3 | Las rutas están en constantes privadas dispersas. | Clase `ApiRoutes` por módulo, con todas las rutas centralizadas. | Mantenibilidad |
| 4 | Se usan objetos anónimos para el body. | DTOs tipados en `Models/Requests` y `Models/Responses`. | Contrato explícito |
| 5 | Si la respuesta no es 2xx, se lanza una excepción. | Devolver `ApiResult<T>` sin lanzar excepciones, para poder probar 400, 401, 404, etc. | Testabilidad |
| 6 | No hay validación de contrato. | `SchemaValidator` con NJsonSchema y esquemas versionados en `Schemas/`. | Pruebas de contrato |
| 7 | No hay trazas HTTP en el reporte. | `ApiClientBase` registra método, URL, body, status y tiempo en ExtentReports (con datos sensibles enmascarados). | Observabilidad |
| 8 | Se aceptan todos los certificados sin control. | Parámetro `ignoreSslErrors` en runsettings (solo QAS). | Seguridad |
| 9 | La cobertura no es medible. | `EndpointCoverageMatrix.md` y `[TestCategory]` por código de estado. | Trazabilidad |
| 10 | Los datos de prueba pueden quedar residuales. | `TestCleanup` con un registro de recursos creados (patrón *cleanup stack*). | Idempotencia |
| 11 | La ejecución es secuencial. | `[assembly: Parallelize(Scope = ExecutionScope.ClassLevel)]`. | Rendimiento |
| 12 | El código está duplicado entre UI y API. | Librería `Common`; la UI referencia los clientes API para su setup. | DRY |

---

## 4. Convenciones

- **Clases de prueba:** `<Module><Operation>ApiTests` (por ejemplo, `DocumentsDeleteApiTests`).
- **Nombres de test:** `Action_Condition_ExpectedResult` (por ejemplo, `DeleteDocument_WithoutToken_ShouldReturn401`).
- **ID de caso:** `[Description("TC-API-DOCUMENTS-01: ...")]`.
- **Categorías:** `[TestCategory("Api")]` + `[TestCategory("Documents")]` + `[TestCategory("Negative")]` o `[TestCategory("Positive")]`.
- **Datos:** un archivo `Data/<Module>ApiData.json` por módulo, leído con `IDataHelper`.
- **Estructura del test:** Arrange / Act / Assert, cada acción dentro de `ExtentStepAsync`, fallos acumulados en una `List<string>` y un único `Assert.AreEqual`.

### Cobertura mínima por endpoint

| Escenario | Código | Obligatorio |
|---|---|---|
| Petición válida | 2xx | Sí |
| Validación de contrato (schema) | 2xx | Sí |
| Body o parámetros inválidos | 400 | Sí |
| Sin token o con token inválido | 401 | Sí |
| Sin permisos | 403 | Si aplica |
| Recurso inexistente | 404 | Si aplica |
| Duplicado o conflicto | 409 | Si aplica |

---

## 5. Código de referencia

### ApiClientBase

```csharp
namespace Playwright.PortalBackoffice.Api.Clients;

/// <summary>Base HTTP client with authentication, serialization and report logging.</summary>
public abstract class ApiClientBase(HttpClient httpClient, ITokenProvider tokenProvider)
{
	protected async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string route, object? body = null, bool authenticate = true)
	{
		using HttpRequestMessage request = new(method, route);
		if (body is not null)
			request.Content = JsonContent.Create(body);
		if (authenticate)
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider.GetTokenAsync());

		var stopwatch = Stopwatch.StartNew();
		using HttpResponseMessage response = await httpClient.SendAsync(request);
		string raw = await response.Content.ReadAsStringAsync();
		stopwatch.Stop();

		return ApiResult<T>.From(response.StatusCode, raw, stopwatch.Elapsed);
	}
}
```

### Cliente por recurso

```csharp
/// <summary>Operations over the documents resource.</summary>
public sealed class DocumentApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
	: ApiClientBase(httpClient, tokenProvider)
{
	public Task<ApiResult<object>> DeleteAsync(DeleteFileRequest request, bool authenticate = true) =>
		SendAsync<object>(HttpMethod.Delete, ApiRoutes.Documents.Delete, request, authenticate);
}
```

### Test

```csharp
[TestClass]
[TestCategory("Api")]
[TestCategory("Documents")]
public class DocumentsDeleteApiTests : ApiTestBase
{
	private static readonly string DocumentsDataPath =
		Path.Combine(AppContext.BaseDirectory, "Data/DocumentsApiData.json");

	private DocumentApiClient _documentApi = null!;

	// ── Setup ──────────────────────────────────────────────────────
	[TestInitialize]
	public override async Task SetupAsync()
	{
		await base.SetupAsync();
		_documentApi = CreateClient<DocumentApiClient>();
	}

	// ── Tests ──────────────────────────────────────────────────────
	/// <summary>
	/// TC-API-DOCUMENTS-02
	/// 1. Send delete request without token.
	/// 2. Validate 401 status.
	/// </summary>
	[TestMethod]
	[TestCategory("Negative")]
	[Description("TC-API-DOCUMENTS-02: Delete document without token returns 401")]
	public async Task DeleteDocument_WithoutToken_ShouldReturn401()
	{
		// Arrange
		var failures = new List<string>();
		var data = DataHelper.ReadJsonData(DocumentsDataPath, "deleteDocument").Rows[0];
		var request = new DeleteFileRequest(data["basePath"].ToString()!, data["fileName"].ToString()!);

		// Act
		var result = await ExtentStepAsync("Send delete request without token",
			() => _documentApi.DeleteAsync(request, authenticate: false));

		// Assert
		ApiAssertions.StatusCode(result, HttpStatusCode.Unauthorized, failures);
		Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
	}
}
```

### api.runsettings

```xml
<RunSettings>
  <TestRunParameters>
	<Parameter name="webApiUrl" value="https://..." />
	<Parameter name="apiCode" value="" />
	<Parameter name="apiUser" value="" />
	<Parameter name="apiPassword" value="" />
	<Parameter name="ignoreSslErrors" value="true" />
	<Parameter name="requestTimeoutSeconds" value="60" />
  </TestRunParameters>
</RunSettings>
```

> Las credenciales se inyectan desde Azure DevOps Library (variables secretas). No se versionan.

---

## 6. Matriz de cobertura (plantilla)

| Módulo | Método | Ruta | 2xx | Schema | 400 | 401 | 403 | 404 | TC IDs | Estado |
|---|---|---|---|---|---|---|---|---|---|---|
| Auth | POST | `Auth/Login` | ☐ | ☐ | ☐ | ☐ | N/A | N/A | TC-API-AUTH-01..04 | Pendiente |
| Documents | DELETE | `Documento/Eliminar` | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | TC-API-DOCUMENTS-01..06 | Pendiente |
| Images | DELETE | `Imagen/Eliminar` | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | TC-API-IMAGES-01..06 | Pendiente |

**Cobertura = escenarios automatizados / escenarios aplicables.** Meta: 100 %.

> Para el inventario completo de endpoints se recomienda partir del Swagger/OpenAPI del backend.

---

## 7. Creación del proyecto

```powershell
cd C:\Users\rortiz\source\repos\PortalBackoffice\Playwright.Backoffice
dotnet new mstest -n Playwright.PortalBackoffice.Api -f net9.0
dotnet new classlib -n Playwright.PortalBackoffice.Common -f net9.0
dotnet sln Playwright.PortalBackoffice.slnx add Playwright.PortalBackoffice.Api\Playwright.PortalBackoffice.Api.csproj
dotnet sln Playwright.PortalBackoffice.slnx add Playwright.PortalBackoffice.Common\Playwright.PortalBackoffice.Common.csproj
dotnet add Playwright.PortalBackoffice.Api reference Playwright.PortalBackoffice.Common
dotnet add Playwright.PortalBackoffice reference Playwright.PortalBackoffice.Common
dotnet add Playwright.PortalBackoffice.Api package ExtentReports
dotnet add Playwright.PortalBackoffice.Api package NJsonSchema
dotnet add Playwright.PortalBackoffice.Api package Microsoft.Extensions.Http
```

Configuración adicional en el `.csproj`:

```xml
<ItemGroup>
  <None Update="Data\**\*.json;Schemas\**\*.json;api.runsettings">
	<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

---

## 8. Hoja de ruta

| Fase | Entregable | Criterio de salida |
|---|---|---|
| 1. Fundación | Proyecto Api, `ApiTestBase`, `ApiClientBase`, `TokenProvider`, logging en Extent | Pipeline ejecuta 1 test de Auth |
| 2. Migración | `DocumentApiClient` refactorizado; la UI lo consume desde el nuevo proyecto o desde Common | Tests UI en verde |
| 3. Inventario | Matriz de endpoints a partir de Swagger | 100 % de endpoints listados |
| 4. Cobertura | Tests positivos, negativos y de schema por módulo | Matriz ≥ 90 % |
| 5. CI/CD | Etapa API antes de UI, publicación de reportes | Gate de calidad activo |
| 6. Cierre | 100 % de la matriz, trazabilidad TC-API | Cobertura 100 % |

### Pipeline (Azure DevOps)

```yaml
- stage: ApiTests
  jobs:
	- job: Api
	  steps:
		- script: dotnet test Playwright.PortalBackoffice.Api --settings api.runsettings --logger trx
- stage: UiTests
  dependsOn: ApiTests
```

---

## 9. Riesgos y mitigación

| Riesgo | Mitigación |
|---|---|
| No hay un Swagger actualizado | Inventariar con el equipo de backend o capturar el tráfico de la UI. |
| Datos residuales en QAS | Cleanup stack en `TestCleanup` y prefijo `AUTO_` en los datos. |
| Token expirado en ejecuciones largas | `TokenProvider` con renovación por expiración. |
| Credenciales expuestas | Variables secretas en el pipeline y enmascaramiento en el reporte. |
| Romper las pruebas UI al mover helpers | Migración incremental en la fase 2, con regresión UI. |
