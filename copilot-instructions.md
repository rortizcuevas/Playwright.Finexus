# Copilot Instructions - Playwright.Finexus

## Contexto del proyecto
Framework de automatizacion hibrido (API + Web + E2E) con Playwright (.NET 9 / C# 13) y MSTest.
API base (PRE): https://apiintegracionfirmadocumentospre.olimpiait.com/ (Bearer JWT, respuestas `BaseResponse`).
Web de firma: flujo Bienvenida → Lectura → OTP → Aceptacion (URL pendiente de confirmar).
Estas instrucciones aplican al proyecto `Playwright.Finexus`.
Catalogo de casos: `Docs/CasosDePrueba.md` (IDs `TC-<MODULO>-##`).
Libreria de reportes: ExtentReports.
Contrato: la coleccion Postman prevalece sobre el Swagger.
Archivos: Base64 para formatos no ZIP; ZIP como archivo multipart.
Pendientes (no inventar valores): limites de tamano, coordenadas de firma, fuente del OTP y URL web de firma.

## Instrucciones especializadas
- `architecture.instructions.md`: reglas genericas reutilizables del framework (capas API, Web y E2E opcionales).
- `project.instructions.md`: particularidades de Playwright.Finexus; prevalece ante conflicto.
- `NewProjectSetup.md`: guia para crear un proyecto nuevo.

## Convenciones de nombrado
Todos los nombres de clases, metodos y elementos deben estar en ingles, reflejar su funcionalidad y no contener emojis.
- Tests: `Action_Condition_ExpectedResult` (ej: `Login_WithValidCredentials_ShouldEnterSystem`).
- Categorias MSTest con `[TestCategory("ModuleName")]`.
- IDs de caso: `TC-MODULE-##` en `[Description]`.
- `[DataRow]` siempre con `DisplayName` obligatorio, unico por metodo y estable (ej: `[DataRow(12, DisplayName = "PDF 12 MB - supera el limite de 10 MB")]`). Cada fila se sincroniza como un Test Case independiente en Azure Test Plans; no generar `DataRow` sin `DisplayName` ni renombrarlo despues de sincronizado. Ver `architecture.instructions.md`.
- Elementos: `ElementName_ElementType` (ej: `LoginButton_Button`, `UsernameInput_Input`).

## Convenciones de comentarios
- Los comentarios `///` de clase deben limitarse a una sola linea descriptiva del proposito funcional. No repetir informacion que ya esta en el `.runsettings`, en el nombre de la clase o que se derive de los principios de diseno.
- Los comentarios `///` de metodo deben incluir unicamente el ID de caso (`TC-MODULE-##`) y los pasos numerados del escenario. No describir patrones de diseno ni arquitectura.
- Los comentarios inline (`//`) dentro de los tests se limitan a las etiquetas `// Arrange`, `// Act` y `// Assert`. Solo anadir texto adicional si el paso no es autoexplicativo.
- Esta prohibido anadir comentarios que repitan lo que el codigo ya expresa claramente.

## Integracion con ExtentReports
Los atributos de MSTest alimentan el reporte ExtentReports:
- `[Description("TC-MODULE-##: description")]` - ID de caso de prueba y descripcion en el reporte.
- `[TestCategory("ModuleName")]` - Categoria MSTest y tag en el reporte.

`Reports/extent-report.ps1`.
La estructura del proyecto debe estar conforme al archivo `architecture.instructions.md` para que el reporte se genere correctamente.

## Evidencias y Azure Test Plans
- Las evidencias las generan `TestBase` y `ApiClientBase`; los tests no adjuntan archivos manualmente.
- Video activo por defecto (`captureVideo=true`); trace solo en fallos.
- `Publish-AzureTestPlan.ps1` publica unicamente los `ResultFiles` del `.trx`; no analizar rutas locales del mensaje de error.
