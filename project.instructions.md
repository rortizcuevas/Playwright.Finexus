---
applyTo: "**/*.cs,**/*.json,**/*.csproj"
---

# Proyecto - Playwright.Finexus

Especificaciones de Playwright.Finexus. Complementa `architecture.instructions.md` (generico); ante conflicto prevalece este archivo.

Proyecto hibrido: pruebas API (API Integracion Firma de documentos) y pruebas Web (flujo de firma), combinadas en flujos E2E.

## Estructura del proyecto

```text
Playwright.Finexus/
+-- Api/
|   +-- Base/
|   |   +-- ApiClientBase.cs          (HttpClient, Bearer, serializacion BaseResponse<T>)
|   |   +-- ApiCallRecorder.cs        (api-calls.json/.html sanitizados)
|   +-- Clients/
|   |   +-- AuthClient.cs
|   |   +-- ProcessClient.cs          (Procesos/Iniciar, Procesos/Consultar)
|   |   +-- DocumentClient.cs         (Emitir, EmitirMultiples, Consultar, Descargar)
|   |   +-- IdentityClient.cs         (Identidad/Iniciar, Identidad/Consultar)
|   +-- Models/
|       +-- Requests/
|       +-- Responses/                (BaseResponse<T> y DTOs del Swagger)
+-- Base/
|   +-- ApiTestBase.cs
|   +-- PageBase.cs
|   +-- TestBase.cs
|   +-- EvidenceComposer.cs           (flow.png; pendiente de implementar)
+-- Data/
+-- Docs/
|   +-- CasosDePrueba.md
|   +-- AzureTestPlans/
+-- Evidence/                         (generada en ejecucion; no se versiona)
+-- Helpers/
|   +-- DataHelpers.cs
|   +-- FileHelper.cs                 (lectura de TestFiles y conversion a Base64/ZIP)
|   +-- TokenProvider.cs              (login unico y cache del token)
|   +-- OtpProvider.cs                (obtencion del OTP: pendiente de definir fuente)
+-- Interfaces/
+-- Pages/
|   +-- Signing/
|       +-- WelcomePage.cs
|       +-- ReadingPage.cs
|       +-- OtpPage.cs
|       +-- AcceptancePage.cs
+-- Reports/
|   +-- extent-report.ps1
|   +-- Publish-AzureTestPlan.ps1     (pendiente de implementar)
+-- TestFiles/                        (PDF validos, limites de tamano, protegidos, corruptos, Word, Excel, ZIP)
+-- Tests/
|   +-- Api/<Module>/<Module><Operation>Tests.cs
|   +-- Web/Signing/Signing<Step>Tests.cs
|   +-- E2E/Signing<Flow>E2ETests.cs
+-- Traceability/
+-- playwright.runsettings
+-- Playwright.Finexus.csproj
```

- El proyecto mezcla API y Web, por lo que usa el nivel `<Capa>` (`Api`, `Web`, `E2E`).
- Ejemplos de nombres: `DocumentIssueTests.cs`, `DocumentIssueMultipleTests.cs`, `ProcessStartTests.cs`.
- Namespaces: `Playwright.Finexus.Api`, `Playwright.Finexus.Pages`, `Playwright.Finexus.Tests`.
- Los archivos de `TestFiles/` se registran con `CopyToOutputDirectory=PreserveNewest`; los archivos grandes (limite de tamano) se generan en tiempo de ejecucion con `FileHelper`, no se versionan.

## Clientes API

- El contrato fuente es la coleccion Postman; ante diferencias con el Swagger prevalece Postman (rutas, codigos HTTP, nombres de campos).
- Documentos no ZIP se envian en Base64 (`FileHelper`); el ZIP de `EmitirMultiples` se envia como archivo en `MultipartFormDataContent`.
- `grafoX`/`grafoY` (coordenadas dentro del documento) y `grafoPagina` (numero de pagina) se leen siempre de `Data/*.json`; no declarar valores por defecto hasta que sean entregados.
- Los casos de limite de tamano estan pendientes: no implementarlos ni generar archivos grandes hasta recibir los limites.
- El token se obtiene una sola vez con `TokenProvider` y se reutiliza; las pruebas de seguridad (sin token, token invalido) usan un cliente sin autenticacion explicito.

## Parametros especificos de `.runsettings`

| Propiedad       | Tipo      | Parametro en `.runsettings`  |
|-----------------|-----------|------------------------------|
| `WebAppUrl`     | `string`  | `webAppUrl` (web de firma)   |
| `WebApiUrl`     | `string`  | `webApiUrl`                  |
| `ApiUser`       | `string`  | `apiUser`                    |
| `ApiPassword`   | `string`  | `apiPassword`                |
| `ApiClientCode` | `string`  | `apiClientCode` (`codigo`)   |
| `MaxFileSizeMb` | pendiente | `maxFileSizeMb` (pendiente)  |
| `OtpSource`     | `string`  | `otpSource`                  |

`TestBase` envia el header `Accept-Language: es-CO,es;q=0.9`.

## Tests E2E hibridos

- Preparar el escenario por API (Login → Procesos/Iniciar → Documento/Emitir o EmitirMultiples → Identidad/Iniciar) y ejecutar la firma en Web (Bienvenida → Lectura → OTP → Aceptacion).
- Validar el resultado final nuevamente por API (Procesos/Consultar, Documento/Consultar, Documento/Descargar).

```csharp
var (http, body) = await ExtentStepAsync("Iniciar proceso", () => _processClient.StartAsync(request));
```
