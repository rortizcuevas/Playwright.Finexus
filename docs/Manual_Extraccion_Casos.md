# Manual: mapeo de los casos de prueba

Explica como pasar de los tests en codigo a casos y suites en Azure Test Plans, y como mantener el mapeo `TC-... -> Azure`.

## 1. Conceptos

- Cada **carpeta** bajo `Tests` es una suite; cada **archivo .cs** es una suite hija; cada **metodo** (o `[DataRow]`) es un caso.
- El **ID funcional** (`TC-MODULO-##`) vive en el atributo `[Description]` y es la clave del mapeo.

## 2. Convencion en el codigo

```csharp
[TestMethod]
[Description("TC-AUTH-01: Login con credenciales validas genera token")]
public async Task Login_Valido() { ... }
```

- Patron del ID: `^(?<id>TC-[A-Z0-9]+(?:-[A-Z0-9]+)*-\d+)\b`. Debe ser unico.
- Pasos: `ExtentStepAsync("texto")` o comentarios `// Arrange`, `// Act`, `// Assert`.
- `[DataRow]` exige `DisplayName` (genera un caso por fila).
- Los tests con `[Ignore]` se omiten. Se excluyen `bin` y `obj`.
- Atributos reconocidos: TestMethod, DataTestMethod, Test, TestCase, Fact, Theory.

## 3. Configuracion (`AzureTestPlanSettings.json`)

| Clave | Descripcion |
|---|---|
| `organization`, `project` | `olimpiait`, `Olimpia Agil` |
| `testPlanId` | `273777` |
| `configurationId` | `36` |
| `discovery.*` | Reglas de descubrimiento (testsPath vacio = autodeteccion) |
| `testCase.areaPath / iterationPath / assignedTo / state / tags` | Datos de los casos importados |
| `suites` | Lo rellena el script (ruta -> ID de suite). No editar a mano |

## 4. Flujo de trabajo

```powershell
cd Playwright.Finexus\docs\AzureTestPlans
dotnet build ..\..\Playwright.Finexus.csproj
.\Get-TestCatalog.ps1 -Filter AuthLogin                  # revisar el catalogo
.\Create-AzureTestCases.ps1 -DryRun                      # ver que se crearia
$env:AZURE_DEVOPS_PAT = '<token>'
.\Create-AzureTestCases.ps1                              # crear suites y casos
.\Create-AzureTestCases.ps1 -UpdateExisting              # actualizar casos cuyo ContentHash cambio
.\Export-TestCases.ps1                                   # alternativa: XLSX en Grouped/ para importar
```

Parametros: `Get-TestCatalog.ps1` [-TestsPath -AssemblyPath -OutputPath -Filter]; `Create-AzureTestCases.ps1` [-DryRun -UpdateExisting -Filter -SettingsPath -MappingPath -TestsPath -AssemblyPath]; `Export-TestCases.ps1` [-ProjectPath -SettingsPath -AreaPath -AssignedTo -State -OutputPath].

## 4.1 Creacion automatica en un Test Plan de Azure

`Create-AzureTestCases.ps1` crea en Azure las suites y los casos a partir del codigo, y registra el resultado en el mapeo.

### Requisitos previos

1. El plan debe existir en Azure DevOps: copiar su ID a `testPlanId` en `AzureTestPlanSettings.json` (`organization`, `project` y `configurationId` tambien).
2. Completar los datos de los casos en `testCase` (`areaPath`, `iterationPath`, `assignedTo`, `state`, `tags`).
3. Definir el PAT en la variable de entorno (nunca en el JSON). El token necesita permisos de lectura/escritura sobre Test Management y Work Items:

```powershell
$env:AZURE_DEVOPS_PAT = '<token>'
```

4. Compilar el proyecto para que el ensamblado este actualizado: `dotnet build`.
5. Cada test debe tener su `[Description("TC-MODULO-##: titulo")]` unico (ver seccion 2).

### Pasos

```powershell
cd Playwright.Finexus\docs\AzureTestPlans

# 1. Simular: muestra suites y casos que se crearian, sin tocar Azure
.\Create-AzureTestCases.ps1 -DryRun

# 2. Crear solo un subconjunto (opcional)
.\Create-AzureTestCases.ps1 -Filter AuthLoginTests

# 3. Crear todo
.\Create-AzureTestCases.ps1

# 4. Tras cambiar titulo o pasos de tests ya creados
.\Create-AzureTestCases.ps1 -UpdateExisting
```

### Que hace el script

| Etapa | Resultado |
|---|---|
| Descubre los tests | Construye el catalogo (igual que `Get-TestCatalog.ps1`) |
| Suites | Una por carpeta y una por archivo `.cs`, bajo el plan `testPlanId`. Las suites existentes se reutilizan |
| Casos | Un caso por metodo (o por `[DataRow]`), con titulo, pasos, area, iteracion, responsable, estado y etiquetas de `testCase` |
| Asociacion | El caso se anade a su suite con la `configurationId` indicada |
| Mapeo | Escribe en `AzureTestPlanMapping.json` el `azureTestCaseId`, `suiteId` y `contentHash` de cada caso |
| Settings | Rellena la seccion `suites` de `AzureTestPlanSettings.json` con los IDs de suite |

### Reglas

- Es idempotente: un caso que ya esta en el mapeo no se duplica. Solo se modifica con `-UpdateExisting` (cuando cambia su `contentHash`).
- Ejecutar siempre `-DryRun` antes de la primera creacion.
- Versionar (commit) `AzureTestPlanMapping.json` y `AzureTestPlanSettings.json` tras crear casos.
- Despues de crear, validar con `Validate-AzureTestPlanMapping.ps1` (seccion 6).
- Alternativa sin API: `Export-TestCases.ps1` genera XLSX en `Grouped/` para importarlos manualmente desde Azure Test Plans.

### Problemas habituales

| Problema | Solucion |
|---|---|
| 401 / 403 | PAT ausente, caducado o sin permisos |
| `testPlanId` no configurado | Informar el ID del plan en Settings |
| Se crean casos duplicados | Falta la entrada en el mapeo; no borrar `AzureTestPlanMapping.json` |
| Suite no encontrada | No editar `suites` a mano; volver a ejecutar el script |

## 5. Mapeo (`AzureTestPlanMapping.json`)

Cada caso tiene una entrada en `azureTestPlanMapping`:

```json
{ "caseKey": "TC-AUTH-01", "caseReference": "TC-AUTH-01", "title": "TC-AUTH-01: ...", "azureTestCaseId": 273888, "suiteId": 273887, "configurationId": "", "contentHash": "..." }
```

`configurationId` vacio usa el de Settings. No incluir nombres de metodos.

## 6. Validacion

```powershell
.\Validate-AzureTestPlanMapping.ps1 -AssemblyPath ..\..\bin\Debug\net9.0\Playwright.Finexus.dll
.\Validate-AzureTestPlanMapping.ps1 -AssemblyPath <dll> -CasePrefix TC-AUTH-
```

Compila `TestMetadataExtractor` si hace falta. No ejecuta pruebas ni contacta con Azure.

## 7. Errores frecuentes

| Error | Solucion |
|---|---|
| Metodo sin ID `TC-MODULO-##` | Anadir el ID al inicio de `[Description]` |
| ID duplicado | Usar IDs unicos |
| Resultado sin entrada en el mapeo | Crear el caso y registrar su ID en el mapeo |
| `testPlanId` no configurado / falta PAT | Revisar Settings y `AZURE_DEVOPS_PAT` |
| DataRow sin DisplayName | Anadir `DisplayName` |

Ver ejecucion y publicacion en [Manual_Ejecucion.md](Manual_Ejecucion.md).
