# Plan: publicacion manual de resultados en Azure DevOps Test Plans

## Objetivo

Ejecutar las pruebas de `Playwright.PortalBackoffice` de forma local y publicar sus resultados en un plan de pruebas de Azure DevOps mediante un script ejecutado manualmente, sin depender de un pipeline.

## Alcance

- Incluye: generacion del `.trx`, resolucion del ID `TC-MODULO-##` desde `[Description]`, mapeo a los casos de Azure, validacion previa, script de publicacion, piloto de evidencias por paso y documentacion de uso.
- Excluye: creacion automatica de casos en Azure, ejecucion desde pipeline y cambios en la logica de las pruebas existentes (salvo el gancho opcional de evidencias descrito en el paso 8).

## Prerrequisitos

- Alcance actual de la prueba general: Documentos (suite `DocumentosSearchTests`, 6 casos) y GlobalDashboard (suite del modulo, 4 casos). Los demas modulos se incorporan a medida que se creen sus casos en Azure.
- Los archivos `.xlsx` generados en `Grouped/` funcionan correctamente como archivo de importacion. Los casos duplicados creados por la primera importacion se eliminan y sus IDs se ignoran.
- El mapeo usa unicamente los IDs numericos definitivos de los casos vigentes (268020 a 268025, ya registrados en `AzureTestPlanMapping.json`).
Organizacion `olimpiait`, proyecto `Olimpia Agil` (el plan y los casos estan en este proyecto, no en `SICOV 2.0`),
- Licencia y permisos para gestionar Test Plans, y token personal con alcance de lectura y escritura sobre Test Management.

## Checklist previo al inicio

Alcance: los 64 casos automatizados (Documentos, GlobalDashboard y Login) estan mapeados.

### Modulos incorporados al mapeo

| Modulo | Suite Azure | Casos | IDs de caso | Estado |
|--------|-------------|-------|-------------|--------|
| Documentos / `DocumentosSearchTests` | 267973 | 6 | 268020 a 268025 | Publicado (runs 44287 y 44289) |
| GlobalDashboard (`Modulo Dashboard`) | 268410 | 4 | 268421 a 268424 | Mapeado; pendiente de publicar |
| Documentos / `DocumentTypeCreationTests` | 268347 | 2 | 268366 y 268367 | Mapeado; pendiente de publicar |
| Documentos / `Sha256Tests` | 268348 | 16 | 268350 a 268365 | Mapeado; pendiente de publicar |
| Login (`Modulo Login`) | 268409 | 6 | 268425 a 268430 | Mapeado; pendiente de publicar |
| Documentos / `DocumentosUploadTests` | 268346 | 10 | 268379 a 268388 | Mapeado; pendiente de publicar |
| Documentos / `DocumentosImageUploadTests` | 268345 | 12 | 268389 a 268400 | Mapeado; pendiente de publicar |
| Documentos / `ConsultarImagenesTests` | 268333 | 8 | 268401 a 268408 | Mapeado; pendiente de publicar |

Los 64 casos estan mapeados y el validador termina correctamente.

Para validar solo el alcance actual:

```powershell
.\Validate-AzureTestPlanMapping.ps1 -AssemblyPath <dll> -CasePrefix 'TC-DOCUMENTOS-CONSULTADOCUMENTOS-','TC-GLOBALDASHBOARD-'
```

### Azure DevOps

- [x] Casos importados en la suite 267973 (6 casos, con sus pasos).
- [x] IDs de plan y suites registrados en `AzureTestPlanSettings.json` (plan 267970, raiz 267971, modulo 267972, suite 267973).
- [x] IDs de casos obtenidos:

  | Caso | ID Azure |
  |------|----------|
  | TC-DOCUMENTOS-CONSULTADOCUMENTOS-01 | 268020 |
  | TC-DOCUMENTOS-CONSULTADOCUMENTOS-02 | 268021 |
  | TC-DOCUMENTOS-CONSULTADOCUMENTOS-03 | 268022 |
  | TC-DOCUMENTOS-CONSULTADOCUMENTOS-04 | 268023 |
  | TC-DOCUMENTOS-CONSULTADOCUMENTOS-05 | 268024 |
  | TC-DOCUMENTOS-CONSULTADOCUMENTOS-06 | 268025 |

- [x] Verificar que cada caso tiene el numero de pasos esperado (confirmado por el usuario).
- [x] Corregir el campo Assigned To (confirmado por el usuario).
- [x] Asignar la iteracion y confirmar el Area Path (confirmado por el usuario).
- [x] Eliminar los casos duplicados de la primera importacion (confirmado: no hay duplicados).
- [x] Confirmar que los 6 casos aparecen como puntos de prueba en la pestana Execute de la suite (confirmado por el usuario).
- [x] Casos en estado Ready (confirmado por el usuario).
- [ ] Verificar permisos para gestionar Test Plans (se comprobara en la primera llamada a la API).

### Acceso

- [x] Token personal (PAT) creado con alcance Test Management en lectura y escritura, y guardado en variable de entorno.
- [ ] Confirmar el nombre exacto de la variable (se asume `AZURE_DEVOPS_PAT`) y que existe tambien para sesiones nuevas, no solo para la actual. Comprobacion desde el entorno del asistente: la variable no esta visible (ni de usuario ni de proceso); debe definirse en la sesion desde la que se ejecute el script o reiniciar Visual Studio tras crearla.
- [ ] Comprobar la fecha de expiracion del token.

### Repositorio y proyecto de pruebas

- [x] Anadir `Docs/AzureTestPlans/output/` al `.gitignore` (hecho en el `.gitignore` de la raiz del repositorio).
- [x] Verificar que los 6 metodos de `DocumentosSearchTests` tienen `[Description]` con IDs `TC-DOCUMENTOS-CONSULTADOCUMENTOS-01` a `-06` (verificado en el codigo; el texto del titulo en Azure esta en espanol y el `[Description]` en ingles, la correspondencia es por ID).
- [ ] Confirmar que la suite de pruebas se ejecuta localmente con el `.runsettings` y genera `.trx`.
- [ ] Revisar el cambio en `TestBase` para evidencias con quien mantiene el codigo compartido.

### Archivos del proyecto

- [x] Extractor `TestMetadataExtractor` creado y probado (detecta 64 metodos con su ID; los 6 de `DocumentosSearchTests` incluidos).
- [x] `Validate-AzureTestPlanMapping.ps1` creado y probado. Con `-CasePrefix 'TC-DOCUMENTOS-CONSULTADOCUMENTOS-'` valida solo el alcance del piloto; sin el, exige mapear los 64 casos.
- [x] El `.csproj` de pruebas excluye `Docs\**` de la compilacion, para que el extractor no se compile dentro del proyecto de pruebas.
- [x] Crear `AzureTestPlanMapping.json` con los 6 casos (azureTestCaseId, suiteId 267973).
- [x] Borrar de `Docs/AzureTestPlans/` los archivos de importacion antiguos sin agrupar (ya no existen; `Grouped/` tampoco esta en disco y se regenera con `Export-TestCases.ps1`).
- [ ] Actualizar el README de `Docs/AzureTestPlans/` al formato agrupado.

### Decisiones

- [x] Alcance, piloto, politica de resultados repetidos y retencion de evidencias (ver Decisiones pendientes).
- [x] Configuracion de prueba: no se usa por ahora.

## Disparador de la publicacion

Ejecutar las pruebas solo genera el `.trx` local. Azure se actualiza unicamente al invocar `Docs/AzureTestPlans/Publish-AzureTestPlan.ps1` de forma explicita.

- Evita que ejecuciones parciales o depuraciones sobrescriban el plan.
- Permite revisar el `.trx` y publicar una ejecucion concreta.
- Se ofrece un script lanzador opcional que encadena validar, ejecutar (`dotnet test` con `.trx`) y, solo con un parametro explicito, publicar. Las ejecuciones directas (Test Explorer, `dotnet test`) siguen siendo independientes.

## Estrategia de identificacion de casos

El `.trx` contiene el nombre del metodo y el resultado, pero no `[Description]`. El ID `TC-MODULO-##` se resuelve por reflexion sobre el ensamblado.

- Un extractor .NET 9 pequeno, fuera del proyecto de pruebas, lee los `[Description]` y emite un JSON con metodo e ID. PowerShell 5.1 solo coordina, porque no puede cargar ensamblados .NET 9.
- Debe usarse el ensamblado de esa misma ejecucion. Los metodos se enlazan con los `TestDefinitions` del `.trx` (className y name).
- No se anaden atributos nuevos a las pruebas.
- Recomendacion: incluir el ID `TC-MODULO-##` en el titulo del caso de Azure para trazabilidad visual.

## Estructura de archivos resultante

## Carpeta autocontenida

Todo lo relacionado con Azure Test Plans vive en `Docs/AzureTestPlans/` y se trata como un proyecto aparte, reutilizable en otros proyectos. No depende de `Data/`, `Reports/` ni del `.csproj` de pruebas; solo recibe como entradas la ruta del proyecto de pruebas, el ensamblado y el `.trx`.

```text
Docs/AzureTestPlans/
+-- AzureTestPlanSettings.json        (organizacion, proyecto, plan, configuracion, valores de los casos)
+-- AzureTestPlanMapping.json         (TC-MODULO-## -> caso y suite de Azure)
+-- Export-TestCases.ps1              (existente)
+-- Write-TestCasesWorkbook.ps1       (existente)
+-- Validate-AzureTestPlanMapping.ps1
+-- Publish-AzureTestPlan.ps1
+-- Run-AndPublish.ps1                (lanzador opcional)
+-- TestMetadataExtractor/            (extractor .NET 9)
+-- Grouped/                          (XLSX de importacion)
+-- output/                           (evidencias, registros de publicacion y .trx de trabajo)
```

Reglas:

- La configuracion especifica de cada proyecto esta solo en `AzureTestPlanSettings.json`; los parametros de los scripts tienen prioridad sobre el archivo.
- El token nunca se guarda en estos archivos: variable de entorno o parametro.
- El gancho de evidencias en `TestBase` es el unico cambio fuera de la carpeta y se publica por separado (paso 8).

## Pasos

### Paso 1. Confirmar identificadores en Azure

Registrar el ID del plan, de cada suite, de cada caso y de cada configuracion de prueba. El plan y la configuracion se guardan en `AzureTestPlanSettings.json`.

### Paso 2. Crear el archivo de mapeo

Se guarda como `Docs/AzureTestPlans/AzureTestPlanMapping.json`.

```json
{
  "_comentario": "Mapeo entre los IDs TC-MODULO-## y los casos de Azure DevOps Test Plans.",
  "azureTestPlanMapping": [
	{ "caseReference": "TC-LOGIN-01", "azureTestCaseId": "0000", "suiteId": "0000" }
  ]
}
```

Los valores `0000` son marcadores y deben sustituirse. Opcionalmente cada entrada admite `configurationId`. El mapeo no contiene nombres de metodos.

### Paso 3. Desarrollar `Docs/AzureTestPlans/Validate-AzureTestPlanMapping.ps1`

No ejecuta pruebas y termina con codigo distinto de cero si detecta:

- Metodos sin `[Description]` o sin ID con formato `TC-MODULO-##`.
- IDs duplicados entre metodos distintos.
- IDs del codigo ausentes del mapeo y entradas del mapeo sin prueba.
- `azureTestCaseId` o `suiteId` vacios o con `0000`.

No se integra en MSTest (`AssemblyInitialize`) para no acoplar las pruebas a la herramienta de seguimiento.

### Paso 4. Generar el archivo de resultados

Ejecutar con el `.runsettings` del proyecto y generar un `.trx` en `TestResults/`.

### Paso 5. Desarrollar `Docs/AzureTestPlans/Publish-AzureTestPlan.ps1`

1. Repetir la validacion y abortar si falla, antes de enviar nada.
2. Leer el `.trx` y resolver el ID de cada resultado con el extractor.
3. Abortar si algun resultado ejecutado no tiene mapeo (no se omite en silencio).
4. Localizar el punto de prueba por plan + suite + caso. Si `configurationId` esta vacio, la configuracion no se usa como filtro. Abortar si no existe ningun punto o si hay varios.
5. Crear la ejecucion asociada al plan, enviar resultados, adjuntar evidencias y cerrar la ejecucion solo cuando todos los resultados se hayan publicado.
6. Mostrar un resumen por consola.

Parametros: ruta del `.trx`, ruta del ensamblado, `-SettingsPath` (organizacion, proyecto y plan se toman de `AzureTestPlanSettings.json` y pueden sobrescribirse), token (parametro o variable de entorno, nunca en el repositorio), `-Force` y `-ResultPolicy`.

### Paso 6. Informacion enviada con cada resultado

- Nombre completo del metodo en el campo de prueba automatizada.
- ID `TC-MODULO-##` y texto de `[Description]` como comentario.
- Mensaje de error y pila cuando falle.
- Evidencia asociada (paso 8).

### Paso 7. Reglas de publicacion

- Configuracion: opcional. `configurationId` (en `AzureTestPlanSettings.json`, en `-ConfigurationId` o en el mapeo) puede estar vacio y por ahora no se utiliza; en ese caso no filtra los puntos de prueba. Si mas adelante se usa, se asocia de forma explicita el navegador o ambiente del `.runsettings` con una configuracion de Azure.
- Ejecuciones parciales: se publican solo los resultados presentes en el `.trx`. Los casos mapeados y no ejecutados no se marcan como no ejecutados; se informan solo en consola.
- Resultados:
  - `Passed` -> Passed.
  - `Failed`, `Error`, `Timeout`, `Aborted` -> Failed.
  - `Inconclusive` -> Inconclusive.
  - `NotExecuted`, `NotRunnable`, `Skipped` -> no se publican, se informan.
- Varios resultados del mismo metodo (reintentos): `-ResultPolicy` admite `Abort` (por defecto, aborta sin publicar), `Last` (publica el ultimo resultado) y `First` (publica el primero). Nunca se publican varios resultados del mismo caso en una misma ejecucion.

### Paso 8. Piloto de evidencias por paso

Alcance: prueba controlada, desactivada por defecto.

(codigo compartido, requiere revision).
- Con el parametro activo se captura una imagen tras cada paso (y ante fallo) en `Docs/AzureTestPlans/output/evidence/<TestName>/`, con nombres `TC-ID_paso-01_slug.png` y un manifiesto JSON (orden, nombre del paso, estado, archivo).
- Piloto: todos los casos de `DocumentosSearchTests` (6 casos), no un unico caso. Cada `ExtentStep` debe corresponder a un paso del caso en Azure.
- Publicacion: primero como adjuntos a nivel de resultado. Despues, experimento de viabilidad con adjuntos a nivel de paso (resultados de accion).
- El video (`Videos/`, disponible al cerrar el contexto) se adjunta solo ante fallo u opcionalmente.
- Verificar los limites de tamano de adjuntos de Azure.
- Riesgo: asociar pasos del codigo con pasos de Azure por orden es fragil si se editan los pasos en Azure.

### Paso 9. Errores de publicacion

- Registro local `Docs/AzureTestPlans/output/publication-<timestamp>.json` con ID de ejecucion de Azure, hash del `.trx` y resultados enviados.
- No se republica el mismo `.trx` sin `-Force`.
- Estados: failed, partial, completed. Opcion de reanudar una publicacion parcial.

### Resultado de la publicacion piloto

- Ejecucion 44287 en Azure (proyecto `Olimpia Agil`): 6 resultados Passed para los casos 268020 a 268025, estado Completed.
- Cada resultado lleva las capturas por paso y `manifest.json` como adjuntos del resultado (no asociados a cada paso). Se aceptan asi.
- Incidencias resueltas: proyecto incorrecto en settings (404), cuerpo PATCH enviado como objeto en vez de array (500), rutas de evidencia superiores a 260 caracteres (copia temporal con `robocopy`), cierre de ejecucion ya completada (solo aviso).
- Detalles conocidos del run 44287: el caso 268024 tiene un adjunto `.json` duplicado y su comentario repite el ID. Ya corregido en el script para futuras publicaciones.

### Paso 10. Documentar el uso

Guia breve de: validar, ejecutar, revisar el `.trx` y publicar, incluyendo parametros y obtencion del token.

## Riesgos

- Importacion agrupada no verificada en Azure; si se rechaza, usar una exportacion nativa de un caso con varios pasos o importar en dos fases.
- Cambios de pasos en Azure rompen la correspondencia de evidencias por paso.
- Modificar `TestBase` afecta a todas las pruebas.

## Decisiones pendientes

Decididas:

- Valores de los casos: Area Path `Olimpia Agil\Otros\Transversal 2025`, Iteration `Olimpia Agil\Transversal\SprintTransversal`, Assigned To `rortiz@olimpiait.com`, estado Design.
- IDs de plan y suites confirmados (ver Prerrequisitos). Configuracion: opcional, vacia por ahora.
- Piloto de evidencias: todos los casos de `DocumentosSearchTests`.
- Politica de resultados repetidos: `-ResultPolicy Abort` por defecto.
- Evidencias: se guardan en `output/evidence/`, excluidas del repositorio (`.gitignore`) y se eliminan manualmente tras publicar.

Pendientes:

- Importar los casos agrupados en Azure y obtener sus IDs numericos para el mapeo (hecho para `DocumentosSearchTests`, ver checklist).
- Alinear opcionalmente las capturas con los pasos de Azure (hoy hay 3 capturas por caso y Azure tiene 4, 4, 4, 4, 3 y 4 pasos).
- Suites y IDs del resto de modulos, cuando se amplie el alcance.

## Criterios de aceptacion

- La validacion falla ante cualquier inconsistencia y no se envia nada a Azure.
- Un `.trx` parcial publica solo los casos ejecutados.
- Un fallo a mitad de publicacion deja registro y permite reanudar.
- El caso piloto muestra una evidencia por paso en Azure.
- No hay credenciales en el repositorio.
