param(
	[string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
	[string]$SettingsPath = (Join-Path $PSScriptRoot 'AzureTestPlanSettings.json'),
	[string]$AreaPath,
	[string]$AssignedTo,
	[string]$State,
	[ValidateNotNullOrEmpty()][string]$OutputPath = (Join-Path $PSScriptRoot 'Grouped')
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Write-TestCasesWorkbook.ps1')
$settings = Get-Content $SettingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $AreaPath) { $AreaPath = $settings.testCase.areaPath }
if (-not $AssignedTo) { $AssignedTo = $settings.testCase.assignedTo }
if (-not $State) { $State = $settings.testCase.state }
foreach ($required in @{ AreaPath = $AreaPath; AssignedTo = $AssignedTo; State = $State }.GetEnumerator()) {
	if (-not $required.Value) { throw "Falta el valor '$($required.Key)' en parametros o en $SettingsPath." }
}

# Fuente de verdad: los atributos [Description("TC-...: titulo")] del codigo.
$sourceTitles = @{}
foreach ($file in Get-ChildItem (Join-Path $ProjectPath 'Tests') -Recurse -Filter '*.cs') {
	$source = Get-Content $file.FullName -Raw -Encoding UTF8
	foreach ($match in [regex]::Matches($source, '\[Description\("(?<id>TC-[A-Z0-9-]+):\s*(?<title>[^"]+)"\)\]')) {
		$sourceTitles[$match.Groups['id'].Value] = $match.Groups['title'].Value
	}
}
$sourceIds = @($sourceTitles.Keys)

$cases = [System.Collections.Generic.List[object]]::new()
function Add-Case([string]$group, [string]$id, [string[]]$steps) {
	if (-not $sourceTitles.ContainsKey($id)) { throw "El caso $id no existe en el codigo." }
	$cases.Add([pscustomobject]@{ Group = $group; Id = $id; Title = "$id`: $($sourceTitles[$id])"; Steps = $steps })
}

Add-Case 'Auth/AuthLoginTests' 'TC-AUTH-01' @(
	'Enviar la solicitud de login a la API con apiUser, apiPassword y apiClientCode configurados.|||La API responde con codigo 200.',
	'Revisar el cuerpo de la respuesta.|||Se devuelve un token de acceso no vacio.')

$docGroupInvalid = 'Document/DocumentIssueInvalidFileTests'
$docSetup = 'Autenticarse en la API y preparar la solicitud de emision de documento.|||Se obtiene un token valido.'
Add-Case $docGroupInvalid 'TC-DOCUMENT-01' @($docSetup, 'Enviar un PDF protegido con contrasena.|||La API rechaza el archivo con el mensaje de error esperado.')
Add-Case $docGroupInvalid 'TC-DOCUMENT-02' @($docSetup, 'Enviar un PDF corrupto.|||La API rechaza el archivo con el mensaje de error esperado.')
Add-Case $docGroupInvalid 'TC-DOCUMENT-03' @($docSetup, 'Enviar la solicitud sin adjuntar archivo.|||La API responde con error de validacion por archivo requerido.')
Add-Case $docGroupInvalid 'TC-DOCUMENT-04' @($docSetup, 'Enviar un archivo Word.|||La API rechaza el archivo por formato no permitido.')
Add-Case $docGroupInvalid 'TC-DOCUMENT-05' @($docSetup, 'Enviar un archivo Excel.|||La API rechaza el archivo por formato no permitido.')

$docGroupLarge = 'Document/DocumentIssueLargeFileTests'
Add-Case $docGroupLarge 'TC-DOCUMENT-06' @($docSetup, 'Enviar un PDF de mas de 5 MB.|||La API procesa la solicitud segun el comportamiento esperado para ese tamano.')
Add-Case $docGroupLarge 'TC-DOCUMENT-07' @($docSetup, 'Enviar un PDF de 10 MB.|||La API acepta el archivo, ya que esta en el limite permitido.')
Add-Case $docGroupLarge 'TC-DOCUMENT-08' @($docSetup, 'Enviar un PDF de 12 MB.|||La API rechaza el archivo por superar el limite de 10 MB.')

$e2eSetup = @(
	'Autenticarse en la API, iniciar el proceso de firma y emitir el PDF del documento.|||La API responde correctamente y se obtiene el enlace del portal de firma.',
	'Abrir el portal de firma con la URL del enlace y continuar la pantalla de bienvenida.|||Se carga el portal y se muestra la pantalla de lectura de documentos.',
	'Registrar la linea base de correos, solicitar el OTP y obtenerlo del correo (otpSource).|||Se recibe un codigo OTP nuevo y valido.',
	'Introducir el OTP en la pagina de validacion y confirmar.|||El OTP es aceptado y se accede a los documentos a firmar.')
Add-Case 'Firma/SigningPdfValidE2ETests' 'TC-FIRMA-01' ($e2eSetup + @(
	'Abrir el PDF valido, leerlo y confirmar la firma.|||Se muestra la pantalla de firma completada y el documento queda firmado.'))

foreach ($n in 1..9) {
	$id = 'TC-FIRMA-DOC-{0:00}' -f $n
	Add-Case 'Firma/SigningDocumentsDisplayTests' $id ($e2eSetup + @(
		'Abrir el documento indicado en el titulo del caso y recorrer sus paginas.|||El documento se muestra con el numero de paginas indicado en el titulo.'))
}
foreach ($n in 1..9) {
	$id = 'TC-FIRMA-DOC-SIGN-{0:00}' -f $n
	Add-Case 'Firma/SigningAllDocumentsE2ETests' $id ($e2eSetup + @(
		'Abrir el documento indicado en el titulo del caso, leerlo y confirmar la firma.|||La firma se aplica y se muestra la pantalla de firma completada.'))
}

if ($cases.Count -ne $sourceIds.Count) { throw "Exportacion: $($cases.Count) casos; codigo: $($sourceIds.Count)." }
if (@($cases | Group-Object Id | Where-Object Count -ne 1).Count) { throw 'IDs duplicados en la exportacion.' }
$diff = @(Compare-Object ($sourceIds | Sort-Object) ($cases.Id | Sort-Object))
if ($diff.Count) { throw "La exportacion no coincide con los IDs del codigo: $($diff | Out-String)" }

foreach ($group in ($cases | Group-Object Group)) {
	$rows = [System.Collections.Generic.List[object]]::new()
	foreach ($case in $group.Group) {
		$rows.Add([pscustomobject][ordered]@{
			ID = ''
			'Work Item Type' = 'Test Case'
			Title = $case.Title
			'Test Step' = ''
			'Step Action' = ''
			'Step Expected' = ''
			'Area Path' = $AreaPath
			'Assigned To' = $AssignedTo
			State = $State
		})
		for ($i = 0; $i -lt $case.Steps.Count; $i++) {
			$parts = $case.Steps[$i] -split '\|\|\|', 2
			if ($parts.Count -ne 2 -or !$parts[0]) { throw "Paso invalido: $($case.Id)" }
			$rows.Add([pscustomobject][ordered]@{
				ID = ''
				'Work Item Type' = ''
				Title = ''
				'Test Step' = ($i + 1).ToString()
				'Step Action' = $parts[0]
				'Step Expected' = $parts[1]
				'Area Path' = ''
				'Assigned To' = ''
				State = ''
			})
		}
	}
	$target = Join-Path $OutputPath ($group.Name + '.xlsx')
	[void][System.IO.Directory]::CreateDirectory((Split-Path $target))
	$headers = @($rows | Where-Object { $_.'Work Item Type' -eq 'Test Case' })
	if ($headers.Count -ne $group.Count) { throw "Validacion fallida: $target" }
	Write-TestCasesWorkbook -Path $target -Rows $rows.ToArray()
	Write-Output "$($group.Name): $($headers.Count) cabeceras de caso; $($rows.Count - $headers.Count) pasos asociados."
}
Write-Output "Total: $($cases.Count) casos. No se ha enviado informacion a Azure."
