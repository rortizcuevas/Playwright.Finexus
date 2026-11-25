# Plan: sincronización automática de casos con Azure Test Plans

## 1. Objetivo

Crear `Create-AzureTestCases.ps1`, un script **genérico y reutilizable en cualquier proyecto**, que:

1. Descubra automáticamente todos los tests de la carpeta `Tests` del proyecto, a cualquier profundidad.
2. Replique en el Test Plan indicado la **misma estructura de carpetas**.
3. Cree **una suite por cada archivo de tests** y, dentro de ella, **un Test Case por cada método de prueba**.
4. Escriba automáticamente los IDs resultantes en `AzureTestPlanMapping.json`.

No se escriben a mano casos, suites, módulos ni IDs. La única fuente de verdad es el código.

## 2. Principios

| Principio | Implicación |
|---|---|
| Autocontenido | Todo vive en `docs/AzureTestPlans` (scripts, extractor, settings, mapping, salida) |
| Portable | Sin nombres de Finexus, módulos ni categorías en el código; todo se lee de settings o parámetros |
| Sin datos manuales | Suites, títulos, pasos y claves se derivan de rutas, clases, métodos y atributos |
| Estructura física | Carpeta → suite; archivo `.cs` → suite hija; método → Test Case |
| Idempotente | Varias ejecuciones no duplican suites ni casos |
| Seguro | `-DryRun` por defecto recomendado; PAT solo en `AZURE_DEVOPS_PAT` |

## 3. Estructura resultante en Azure

Ejemplo con el proyecto actual (generado, no configurado):

```text
<Test Plan> (root suite)
└── Tests                                    (opcional, ver includeRootFolder)
	├── Api
	│   └── Document
	│       ├── DocumentIssueInvalidFileTests    ← suite del archivo
	│       │   ├── TC-DOCUMENT-01: Carga de PDF protegido con contrasena
	│       │   └── ...
	│       └── DocumentIssueLargeFileTests
	└── UI
		└── ...
```

Regla: la ruta relativa `Api/Document/DocumentIssueInvalidFileTests.cs` produce la ruta de suites `Api › Document › DocumentIssueInvalidFileTests`. Si un archivo contiene varias clases de prueba, todas sus pruebas van a la suite del archivo.

## 4. Descubrimiento de casos (`Get-TestCatalog.ps1`)

Se combinan dos fuentes para obtener metadatos fiables **y** la ruta física:

1. **Fuente (`Tests/**/*.cs`)**: recorrido recursivo que obtiene, por archivo, `relativePath`, `namespace`, clases y métodos marcados con los atributos configurados.
2. **Ensamblado (opcional, `TestMetadataExtractor`)**: si se indica `-AssemblyPath`, el extractor existente aporta `fullName` y `[Description]` resueltos por reflexión. Se cruza con la fuente por `Namespace.Clase.Método`. Se amplía para emitir también atributos de prueba configurables (no solo MSTest).

Si no hay ensamblado, el parser de fuente basta: detecta atributos (`[TestMethod]`, `[Test]`, `[Fact]`, `[Theory]`, configurables) y lee `[Description("...")]` / `DisplayName` cuando existan.

### 4.1 Objeto normalizado

```text
CaseKey        clave estable (ver 4.2)
CaseReference  ID explícito si el texto cumple idPattern (opcional)
Title          Description (+ " - " + DisplayName si es DataRow) > nombre del método humanizado
FullName       Namespace.Clase.Método
RelativePath   Api/Document/DocumentIssueInvalidFileTests.cs
SuitePath      [Api, Document, DocumentIssueInvalidFileTests]
Steps[]        { Action, Expected }
ContentHash    hash de Title + Steps (detecta cambios)
```

### 4.2 Clave estable

- Si existe `CaseReference` (p. ej. `TC-DOCUMENT-01`), se usa.
- Si no, `FullName` (`Namespace.Clase.Método`).
- Métodos con `[DataRow]` (opción B): `CaseKey = <clave del método>#<DisplayName normalizado>`.

### 4.2.1 Casos parametrizados (opción B: un Test Case por `DataRow`)

- Método sin `[DataRow]` → un Test Case.
- Método con `[DataRow]` → un Test Case independiente por fila, dentro de la suite del archivo.
- `DisplayName` es **obligatorio** en cada `[DataRow]`: único dentro del método, descriptivo, estable y sin valores volátiles (fechas, GUID, aleatorios).
- Si una fila no tiene `DisplayName` o hay duplicados en el mismo método, el script se detiene antes de llamar a Azure.
- `Title = <Description del método> - <DisplayName>`; `[Description]` sigue siendo la referencia `TC-*` a nivel de método.
- El índice de la fila (`DataRowIndex`) y los valores (`DataRowValues`) son solo diagnóstico; nunca forman parte de la clave.
- Renombrar un `DisplayName` ya sincronizado genera una identidad nueva; el caso anterior queda huérfano y se informa en el resumen.

Campos adicionales del objeto normalizado:

```text
DataRowDisplayName  DisplayName de la fila (null si no es parametrizado)
DataRowIndex        posición de la fila (diagnóstico)
DataRowValues[]     valores literales de la fila (diagnóstico, se añaden a la descripción del caso)
```

Se valida que no haya claves duplicadas; si las hay, el script se detiene antes de llamar a Azure.

### 4.3 Pasos

Por prioridad, sin intervención manual:

1. Comentarios `// Arrange`, `// Act`, `// Assert` y llamadas `ExtentStepAsync("...")` / `Step("...")` (patrones configurables) dentro del método o del helper que invoca.
2. Fallback de un paso: Acción = `Ejecutar prueba automatizada <FullName>`, Resultado = `La prueba finaliza correctamente`.

### 4.4 Exclusiones

Configurables: carpetas (`bin`, `obj`, `Base`, `Helpers`), patrones de archivo y métodos con `[Ignore]` (opcionalmente incluidos).

## 5. Configuración (`AzureTestPlanSettings.json`)

```json
{
  "organization": "olimpiait",
  "project": "Olimpia Agil",
  "testPlanId": 0,
  "discovery": {
	"testsPath": "",
	"includeRootFolder": false,
	"testAttributes": ["TestMethod", "DataTestMethod", "Test", "TestCase", "Fact", "Theory"],
	"titleAttributes": ["Description", "DisplayName"],
	"idPattern": "^(?<id>TC-[A-Z0-9]+(?:-[A-Z0-9]+)*-\\d+)\\b",
	"stepPatterns": ["ExtentStepAsync\\(\"(?<step>[^\"]+)\"", "//\\s*(?<step>Arrange|Act|Assert)\\b"],
	"excludeFolders": ["bin", "obj"],
	"includeIgnored": false,
	"dataRowMode": "perRow",
	"requireDataRowDisplayName": true
  },
  "testCase": {
	"areaPath": "",
	"iterationPath": "",
	"assignedTo": "",
	"state": "Design",
	"tags": ["Automatizado"]
  },
  "suites": {}
}
```

- `testsPath` vacío: se busca automáticamente la primera carpeta `Tests` subiendo desde `docs/AzureTestPlans` hasta la raíz del repositorio.
- `suites` lo rellena el script: `{ "Api/Document/DocumentIssueInvalidFileTests": 2010, ... }`.

## 6. Parámetros del script

```powershell
.\Create-AzureTestCases.ps1 `
	-TestPlanId 12345 `
	[-TestsPath ..\..\Tests] `
	[-AssemblyPath <dll de tests>] `
	[-Organization ...] [-Project ...] `
	[-SettingsPath .\AzureTestPlanSettings.json] `
	[-MappingPath .\AzureTestPlanMapping.json] `
	[-Filter 'Api/Document/*'] `
	[-UpdateExisting] `
	[-DryRun]
```

`-Filter` admite comodines sobre `RelativePath` o `CaseKey`.

## 7. Idempotencia

**Suites**: se buscan por nombre y suite padre; solo se crean si faltan. Los IDs se cachean en `settings.suites` y se revalidan.

**Test Cases**, en orden:

1. `CaseKey` en el mapping → se reutiliza. Con `-UpdateExisting` y `ContentHash` distinto → PATCH de título y pasos.
2. WIQL por etiqueta `AutoKey:<hash corto de CaseKey>` → se reutiliza.
3. Si no existe → se crea con etiquetas `<testCase.tags>; AutoKey:<hash>`.

Si un caso ya mapeado cambió de archivo (`SuitePath` distinto), se añade a la nueva suite y se informa (no se borra de la anterior).

## 8. Endpoints REST (api-version 7.1)

| Acción | Método | Endpoint |
|---|---|---|
| Obtener plan | GET | `/testplan/plans/{planId}` |
| Listar suites | GET | `/testplan/Plans/{planId}/suites?asTreeView=false` |
| Crear suite | POST | `/testplan/Plans/{planId}/suites` |
| Validar area/iteration | GET | `/wit/classificationnodes/{Areas\|Iterations}/{path}` |
| Buscar por etiqueta | POST | `/wit/wiql` |
| Crear Test Case | POST | `/wit/workitems/$Test%20Case` |
| Actualizar Test Case | PATCH | `/wit/workitems/{id}` |
| Casos de una suite | GET | `/testplan/Plans/{planId}/Suites/{suiteId}/TestCase` |
| Añadir a suite | POST | `/testplan/Plans/{planId}/Suites/{suiteId}/TestCase` |

Autenticación: `Authorization: Basic base64(":" + $env:AZURE_DEVOPS_PAT)`.

Campos del Test Case: `System.Title`, `Microsoft.VSTS.TCM.Steps` (XML con IDs desde 2 y texto escapado), `System.AreaPath`, `System.IterationPath`, `System.AssignedTo`, `System.State`, `System.Tags`, `Microsoft.VSTS.TCM.AutomatedTestName` = `FullName` (los vacíos se omiten).

## 9. Mapping (`AzureTestPlanMapping.json`)

Upsert por `CaseKey`, compatible con el formato actual:

```json
{
  "caseKey": "TC-DOCUMENT-01",
  "caseReference": "TC-DOCUMENT-01",
  "fullName": "Playwright.Finexus.Tests.DocumentIssueInvalidFileTests.Issue_WithPasswordProtectedPdf_ShouldBeRejected",
  "relativePath": "Api/Document/DocumentIssueInvalidFileTests.cs",
  "azureTestCaseId": 1001,
  "suiteId": 2010,
  "contentHash": "...",
  "dataRowDisplayName": null,
  "dataRowIndex": null,
  "dataRowValues": null
}
```

Para filas `DataRow`, `caseKey` es `<clave del método>#<DisplayName normalizado>` y `dataRowDisplayName`, `dataRowIndex` y `dataRowValues` se rellenan solo con fines diagnósticos (nunca de identidad).

`Publish-AzureTestPlan.ps1` y `Validate-AzureTestPlanMapping.ps1` se adaptan para resolver por `caseReference` o, en su defecto, por `fullName`. Se escribe en UTF-8 al final; con errores se guarda el progreso parcial.

## 10. Flujo

```text
1. Cargar settings, resolver testsPath, comprobar AZURE_DEVOPS_PAT (salvo DryRun sin red)
2. Get-TestCatalog → casos normalizados, validar duplicados, aplicar Filter
3. GET plan, validar area/iteration
4. Construir árbol de suites a partir de SuitePath (crear las que falten)
5. Por cada caso: resolver ID → crear/actualizar → añadir a suite
6. Escribir mapping y settings
7. Resumen: suites creadas, casos creados/actualizados/reutilizados, errores
```

Con `-DryRun` solo se hacen GET y se imprime el árbol y las acciones previstas.

## 11. Errores

- `Invoke-AzDo` con 3 reintentos y backoff ante 429/5xx, respetando `Retry-After`.
- 401/403 detienen el script con mensaje sobre los ámbitos del PAT.
- Fallo en un caso: se registra y se continúa; código de salida ≠ 0.
- El PAT nunca se imprime.
- `DataRow` sin `DisplayName` o con `DisplayName` duplicado dentro del mismo método: el script aborta antes de cualquier llamada a Azure y lista archivo, método e índice de fila.

## 12. Archivos (todos en `docs/AzureTestPlans`)

| Archivo | Cambio |
|---|---|
| `Get-TestCatalog.ps1` | **Nuevo**: descubrimiento recursivo y normalización |
| `AzureDevOpsClient.ps1` | **Nuevo**: `Invoke-AzDo` y funciones REST |
| `Create-AzureTestCases.ps1` | **Nuevo**: sincronización |
| `TestMetadataExtractor/Program.cs` | Atributos de prueba configurables y salida de namespace |
| `Export-TestCases.ps1` | Usa el catálogo; un XLSX por archivo replicando la estructura de carpetas |
| `Publish-AzureTestPlan.ps1` / `Validate-AzureTestPlanMapping.ps1` | Resolución por `caseReference` o `fullName` |
| `AzureTestPlanSettings.json` / `AzureTestPlanMapping.json` | Nuevo esquema genérico |
| `README.md` | Uso en cualquier proyecto |

## 13. Fases

| Fase | Entregable | Validación |
|---|---|---|
| 1 | `Get-TestCatalog.ps1` | Lista todos los tests del proyecto con su `SuitePath`, sin duplicados; un caso por `DataRow` y error si falta `DisplayName` |
| 2 | Refactor de `Export-TestCases.ps1` | XLSX por archivo con la estructura de carpetas |
| 3 | `AzureDevOpsClient.ps1` y `-DryRun` | Árbol previsto contra el plan real, sin escrituras |
| 4 | Creación de suites | Segunda ejecución no duplica |
| 5 | Creación de casos y asociación | Ejecución con `-Filter` sobre un solo archivo |
| 6 | Mapping, `-UpdateExisting`, adaptación de publicación | `Validate-AzureTestPlanMapping.ps1` y `Run-AndPublish.ps1 -Publish` |
| 7 | README | Prueba en otro proyecto copiando solo `docs/AzureTestPlans` |

## 14. Riesgos

| Riesgo | Mitigación |
|---|---|
| Duplicados por re-ejecución | Mapping + etiqueta `AutoKey` + WIQL |
| Renombrar métodos sin `CaseReference` cambia la clave | Recomendar IDs en `[Description]`; informe de casos huérfanos |
| Parser de fuente no reconoce sintaxis poco común | Uso opcional del ensamblado vía extractor |
| Paths de área/iteración inválidos | Validación previa |
| Límites de la API | Reintentos con backoff |

## 15. Datos necesarios antes de ejecutar contra Azure

1. ID del Test Plan.
2. `areaPath`, `iterationPath` y `assignedTo` (opcionales).
3. PAT en `AZURE_DEVOPS_PAT` con Work Items y Test Management (Read & Write).

## 16. Uso

```powershell
$env:AZURE_DEVOPS_PAT = '<pat>'
cd docs\AzureTestPlans
.\Create-AzureTestCases.ps1 -TestPlanId 12345 -DryRun
.\Create-AzureTestCases.ps1 -TestPlanId 12345
.\Run-AndPublish.ps1 -Publish
```
