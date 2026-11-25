# Publica en Azure DevOps Test Plans los resultados de un .trx. Se ejecuta de forma explicita; las pruebas nunca publican por si solas.
# Uso:  .\Publish-AzureTestPlan.ps1 -TrxPath <.trx> -AssemblyPath <dll> -CasePrefix 'TC-DOCUMENTOS-CONSULTADOCUMENTOS-' -DryRun
#       $env:AZURE_DEVOPS_PAT = '<token>'; .\Publish-AzureTestPlan.ps1 -TrxPath <.trx> -AssemblyPath <dll> -CasePrefix '...'
param(
	[Parameter(Mandatory)][string]$TrxPath,
	[Parameter(Mandatory)][string]$AssemblyPath,
	[string]$SettingsPath,
	[string]$MappingPath,
	[string]$ExtractorDll,
	[string]$EvidencePath,
	[string[]]$CasePrefix = @(),
	[ValidateSet('Abort', 'Last', 'First')][string]$ResultPolicy = 'Abort',
	[string]$Token,
	[string]$TokenVariable = 'AZURE_DEVOPS_PAT',
	[string]$RunName,
	[string]$ResumeLog,
	[switch]$DryRun,
	[switch]$Force
)
$ErrorActionPreference = 'Stop'
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
if (-not $SettingsPath) { $SettingsPath = Join-Path $scriptDir 'AzureTestPlanSettings.json' }
if (-not $MappingPath) { $MappingPath = Join-Path $scriptDir 'AzureTestPlanMapping.json' }
if (-not $ExtractorDll) { $ExtractorDll = Join-Path $scriptDir 'output/extractor/TestMetadataExtractor.dll' }
if (-not $EvidencePath) { $EvidencePath = Join-Path (Split-Path -Parent (Resolve-Path $AssemblyPath).Path) 'Evidence' }
$outputDir = Join-Path $scriptDir 'output'
New-Item -ItemType Directory -Force $outputDir | Out-Null

function Fail([string]$message) { Write-Error $message -ErrorAction Continue; exit 1 }
function Test-InScope([string]$id) {
	if ($CasePrefix.Count -eq 0) { return $true }
	foreach ($p in $CasePrefix) { if ($id.StartsWith($p, [System.StringComparison]::Ordinal)) { return $true } }
	return $false
}

function Get-Slug([string]$value) {
	if ([string]::IsNullOrWhiteSpace($value)) { return 'default' }
	$sb = New-Object System.Text.StringBuilder
	foreach ($c in $value.Normalize([System.Text.NormalizationForm]::FormD).ToCharArray()) {
		if ([System.Globalization.CharUnicodeInfo]::GetUnicodeCategory($c) -eq [System.Globalization.UnicodeCategory]::NonSpacingMark) { continue }
		[void]$sb.Append($(if ([char]::IsLetterOrDigit($c)) { [char]::ToLowerInvariant($c) } else { '-' }))
	}
	$s = ([regex]::Replace($sb.ToString(), '-+', '-')).Trim('-')
	if ($s.Length -gt 60) { $s = $s.Substring(0, 60).TrimEnd('-') }
	return $s
}
function Get-EvidenceFiles([string]$method, $resultFiles = @()) {
	$dir = Join-Path $EvidencePath (Get-Slug $method)
	$candidates = @()
	if (Test-Path $dir) { $candidates += @(Get-ChildItem $dir -File -Recurse | ForEach-Object FullName) }
	$candidates += @($resultFiles | Where-Object { $_ -and (Test-Path $_) })
	$candidates = @($candidates | Sort-Object -Unique)
	$selected = @()
	$zip = @($candidates | Where-Object { $_ -match '\.zip$' } | Sort-Object { (Split-Path $_ -Leaf) -ne 'seguimiento.zip' }, { $_ })
	$video = @($candidates | Where-Object { $_ -match '\.(webm|mp4)$' } | Sort-Object { $_ -notmatch '\.webm$' }, { $_ })
	$shot = @($candidates | Where-Object { $_ -match '\.(png|jpe?g)$' } | Sort-Object { (Split-Path $_ -Leaf) -ne 'failure.png' }, { $_ })
	foreach ($g in @($zip, $video, $shot)) { if ($g.Count -gt 0) { $selected += $g[0] } }
	return @($selected)
}

# 1. Validacion previa
$validator = Join-Path $scriptDir 'Validate-AzureTestPlanMapping.ps1'
$validatorArgs = @{ AssemblyPath = $AssemblyPath; MappingPath = $MappingPath; ExtractorDll = $ExtractorDll; CasePrefix = $CasePrefix; Quiet = $true }
& $validator @validatorArgs
if ($LASTEXITCODE -ne 0) { Fail 'La validacion previa ha fallado. No se ha enviado nada a Azure.' }

$settings = Get-Content $SettingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$mapping = @((Get-Content $MappingPath -Raw -Encoding UTF8 | ConvertFrom-Json).azureTestPlanMapping)
$mapById = @{}; foreach ($e in $mapping) { $mapById[$e.caseReference] = $e }

# 2. Metodos de prueba -> ID
$tmp = [System.IO.Path]::GetTempFileName()
try {
	& dotnet $ExtractorDll (Resolve-Path $AssemblyPath).Path $tmp
	if ($LASTEXITCODE -ne 0) { Fail 'El extractor ha fallado.' }
	$methods = @(ConvertFrom-Json (Get-Content $tmp -Raw -Encoding UTF8)) | ForEach-Object { $_ }
}
finally { Remove-Item $tmp -ErrorAction SilentlyContinue }
$methodByFullName = @{}; foreach ($m in $methods) { $methodByFullName[$m.fullName] = $m }

# 3. Lectura del .trx
$trxFile = (Resolve-Path $TrxPath).Path
$trxHash = (Get-FileHash $trxFile -Algorithm SHA256).Hash
[xml]$trx = Get-Content $trxFile -Raw -Encoding UTF8
$ns = New-Object System.Xml.XmlNamespaceManager($trx.NameTable)
$ns.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
$defs = @{}
foreach ($u in $trx.SelectNodes('//t:UnitTest', $ns)) {
	$tm = $u.SelectSingleNode('t:TestMethod', $ns)
	$defs[$u.id] = "$($tm.className).$($tm.name)"
}
$outcomeMap = @{ Passed = 'Passed'; Failed = 'Failed'; Error = 'Failed'; Timeout = 'Failed'; Aborted = 'Failed'; Inconclusive = 'Inconclusive' }
$skippedOutcomes = @()
$results = [System.Collections.Generic.List[object]]::new()
foreach ($r in $trx.SelectNodes('//t:UnitTestResult', $ns)) {
	$full = $defs[$r.testId]
	$method = $methodByFullName[$full]
	$id = if ($method) { $method.caseReference } else { $null }
	if (-not $id) { Fail "Resultado sin ID TC-MODULO-## resoluble: $full" }
	if (-not (Test-InScope $id)) { continue }
	if (-not $outcomeMap.ContainsKey([string]$r.outcome)) { $skippedOutcomes += "$id ($($r.outcome))"; continue }
	if (-not $mapById.ContainsKey($id)) { Fail "Resultado ejecutado sin entrada en el mapeo: $id" }
	$msg = $r.SelectSingleNode('t:Output/t:ErrorInfo/t:Message', $ns)
	$stack = $r.SelectSingleNode('t:Output/t:ErrorInfo/t:StackTrace', $ns)
	$files = @($r.SelectNodes('t:ResultFiles/t:ResultFile', $ns) | ForEach-Object { $_.path })
	$results.Add([pscustomobject]@{
		CaseReference = $id; FullName = $full; Method = $method.method; Description = $method.description
		Outcome = $outcomeMap[[string]$r.outcome]; Duration = [TimeSpan]::Parse($r.duration)
		Start = [datetime]$r.startTime; End = [datetime]$r.endTime
		ErrorMessage = if ($msg) { $msg.InnerText } else { $null }; StackTrace = if ($stack) { $stack.InnerText } else { $null }
		ResultFiles = $files
	})
}
if ($results.Count -eq 0) { Fail 'El .trx no contiene resultados publicables dentro del alcance indicado.' }

# 4. Politica de resultados repetidos
$final = [System.Collections.Generic.List[object]]::new()
foreach ($g in ($results | Group-Object CaseReference)) {
	if ($g.Count -gt 1 -and $ResultPolicy -eq 'Abort') { Fail "Varios resultados para $($g.Name). Use -ResultPolicy Last o First." }
	$ordered = @($g.Group | Sort-Object Start)
	$final.Add($(if ($ResultPolicy -eq 'First') { $ordered[0] } else { $ordered[-1] }))
}
$executed = @($final | ForEach-Object CaseReference)
$notExecuted = @($mapping | Where-Object { (Test-InScope $_.caseReference) -and ($executed -notcontains $_.caseReference) } | ForEach-Object caseReference)

# 5. Republicacion
if (-not $DryRun -and -not $Force -and -not $ResumeLog) {
	foreach ($logFile in (Get-ChildItem $outputDir -Filter 'publication-*.json' -ErrorAction SilentlyContinue)) {
		$old = Get-Content $logFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
		if ($old.trxHash -eq $trxHash -and $old.status -eq 'completed') { Fail "Este .trx ya se publico (ejecucion Azure $($old.runId), $($logFile.Name)). Use -Force para republicar." }
	}
}

# 6. Resumen
Write-Host "Resultados a publicar: $($final.Count)"
$final | ForEach-Object { Write-Host ('  {0} -> {1} (caso {2}, suite {3})' -f $_.CaseReference, $_.Outcome, $mapById[$_.CaseReference].azureTestCaseId, $mapById[$_.CaseReference].suiteId) }
if ($notExecuted.Count -gt 0) { Write-Host "Mapeados y no ejecutados (no se publican): $($notExecuted -join ', ')" }
if ($skippedOutcomes.Count -gt 0) { Write-Host "Resultados no publicables (omitidos): $($skippedOutcomes -join ', ')" }
foreach ($r in $final) {
	$ev = @(Get-EvidenceFiles $r.Method $r.ResultFiles)
	if ($r.Outcome -eq 'Failed' -and $ev.Count -eq 0) { Write-Warning "Resultado fallido sin evidencias: $($r.CaseReference)" }
	Write-Host ('  Evidencias de {0}: {1} archivo(s): {2}' -f $r.CaseReference, $ev.Count, ((@($ev | ForEach-Object { Split-Path $_ -Leaf })) -join ', '))
}
if ($DryRun) { Write-Host 'DryRun: no se ha contactado con Azure.'; exit 0 }

# 7. Publicacion
if (-not $Token) { $Token = [Environment]::GetEnvironmentVariable($TokenVariable) }
if (-not $Token) { $Token = [Environment]::GetEnvironmentVariable($TokenVariable, 'User') }
if (-not $Token) { Fail "No hay token: defina la variable de entorno $TokenVariable o use -Token." }
$headers = @{ Authorization = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$Token")) }
$base = "https://dev.azure.com/$([uri]::EscapeDataString($settings.organization))/$([uri]::EscapeDataString($settings.project))/_apis/test"
$api = 'api-version=7.1'
function Invoke-Azure([string]$method, [string]$url, $body) {
	$params = @{ Method = $method; Uri = $url; Headers = $headers }
	if ($null -ne $body) {
		$params.Body = [Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $body -Depth 10 -Compress))
		$params.ContentType = 'application/json; charset=utf-8'
	}
	Invoke-RestMethod @params
}

$logPath = if ($ResumeLog) { (Resolve-Path $ResumeLog).Path } else { Join-Path $outputDir ("publication-{0}.json" -f (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$log = if ($ResumeLog) { Get-Content $logPath -Raw -Encoding UTF8 | ConvertFrom-Json } else {
	[pscustomobject]@{ trxHash = $trxHash; trx = $trxFile; status = 'inProgress'; runId = $null; planId = $settings.testPlanId; published = @() }
}
if ($ResumeLog -and $log.trxHash -ne $trxHash) { Fail 'El registro a reanudar corresponde a otro .trx.' }
function Save-Log { $log | ConvertTo-Json -Depth 10 | Set-Content $logPath -Encoding UTF8 }

try {
	$toPublish = @($final | Where-Object { @($log.published) -notcontains $_.CaseReference })
	if (-not $log.runId) {
		$pointIds = [System.Collections.Generic.List[int]]::new()
		$pointByCase = @{}
		foreach ($r in $toPublish) {
			$e = $mapById[$r.CaseReference]
			$res = Invoke-Azure 'GET' "$base/Plans/$($settings.testPlanId)/Suites/$($e.suiteId)/points?testCaseId=$($e.azureTestCaseId)&$api" $null
			$points = @($res.value)
			$cfg = if ($e.PSObject.Properties['configurationId'] -and $e.configurationId) { $e.configurationId } else { $settings.configurationId }
			if ($cfg) { $points = @($points | Where-Object { [string]$_.configuration.id -eq [string]$cfg }) }
			if ($points.Count -eq 0) { throw "No hay punto de prueba para $($r.CaseReference) (caso $($e.azureTestCaseId), suite $($e.suiteId))." }
			if ($points.Count -gt 1) { throw "Hay $($points.Count) puntos de prueba para $($r.CaseReference); indique configurationId." }
			$pointByCase[$r.CaseReference] = [int]$points[0].id
			$pointIds.Add([int]$points[0].id)
		}
		$prefix = if ($settings.PSObject.Properties['runNamePrefix'] -and $settings.runNamePrefix) { $settings.runNamePrefix } else { 'Playwright' }
		$name = if ($RunName) { $RunName } else { "$prefix $(Get-Date -Format 'yyyy-MM-dd HH:mm')" }
		$run = Invoke-Azure 'POST' "$base/runs?$api" @{ name = $name; plan = @{ id = [int]$settings.testPlanId }; pointIds = @($pointIds); automated = $false }
		$log.runId = $run.id; Save-Log
		Write-Host "Ejecucion creada en Azure: $($run.id)"
	}
	$runId = $log.runId
	$azureResults = @((Invoke-Azure 'GET' "$base/runs/$runId/results?$api" $null).value)

	foreach ($r in $toPublish) {
		$caseId = [string]$mapById[$r.CaseReference].azureTestCaseId
		$target = @($azureResults | Where-Object { [string]$_.testCase.id -eq $caseId })
		if ($target.Count -ne 1) { throw "La ejecucion $runId no tiene exactamente un resultado para el caso $caseId." }
		$comment = if ("$($r.Description)".StartsWith($r.CaseReference)) { "$($r.Description)" } else { "$($r.CaseReference): $($r.Description)" }
		$patch = @{
			id = $target[0].id; outcome = $r.Outcome; state = 'Completed'; comment = $comment
			automatedTestName = $r.FullName; automatedTestStorage = [IO.Path]::GetFileName($AssemblyPath)
			durationInMs = [long]$r.Duration.TotalMilliseconds
			startedDate = $r.Start.ToUniversalTime().ToString('o'); completedDate = $r.End.ToUniversalTime().ToString('o')
		}
		if ($r.Outcome -eq 'Failed') {
			$clean = {
				param($t)
				if (-not $t) { return $null }
				$t = [regex]::Replace($t, '(?i)[A-Z]:\\(?:[^\\\r\n]+\\)*?(Playwright\.Finexus\\[^\s\r\n]*)', '..\$1')
				$t = [regex]::Replace($t, '(?i)[A-Z]:\\[^\s\r\n]*\\([^\\\s\r\n]+)', '$1')
				$t
			}
			$msgText = & $clean $r.ErrorMessage
			if ($msgText) {
				$lines = @($msgText -split "\r?\n" | Where-Object { $_.Trim() })
				$msgText = ($lines | Select-Object -Unique) -join [Environment]::NewLine
			}
			$stackText = & $clean $r.StackTrace
			if ($stackText) {
				$idx = $stackText.IndexOf('--- Stack trace original ---')
				if ($idx -ge 0) { $stackText = $stackText.Substring($idx + 28).Trim() }
				$diagText = $null
				$dm = '=== DIAGNOSTICO ==='; $sm = '=== STACK TRACE ==='
				$di = $stackText.IndexOf($dm); $si = $stackText.IndexOf($sm)
				if ($di -ge 0 -and $si -gt $di) {
					$diagText = $stackText.Substring($di + $dm.Length, $si - $di - $dm.Length).Trim()
					$stackText = $stackText.Substring($si + $sm.Length)
				}
				$frames = ($stackText -split "\r?\n" | Where-Object { $_ -match '^\s*at ' } | ForEach-Object { $_.Trim() } | Select-Object -Unique) -join [Environment]::NewLine
				$stackText = if ($diagText) { "$dm$([Environment]::NewLine)$diagText$([Environment]::NewLine)$sm$([Environment]::NewLine)$frames" } else { $frames }
			}
			if ($msgText.Length -gt 3000) { $msgText = $msgText.Substring(0, 3000) }
			if ($stackText.Length -gt 3000) { $stackText = $stackText.Substring(0, 3000) }
			$patch.errorMessage = $msgText; $patch.stackTrace = $stackText
		}
		Invoke-Azure 'PATCH' "$base/runs/$runId/results?$api" @(, $patch) | Out-Null

		$tempDir = Join-Path ([IO.Path]::GetTempPath()) ('ev-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
		New-Item -ItemType Directory -Force $tempDir | Out-Null
		$attachments = @()
		foreach ($src in @(Get-EvidenceFiles $r.Method $r.ResultFiles)) {
			$leaf = if ($src -match '\.zip$') { "$($r.CaseReference)_trazabilidad.zip" } else { Split-Path $src -Leaf }
			$copy = Join-Path $tempDir $leaf
			Copy-Item $src $copy
			$attachments += $copy
		}
		foreach ($file in $attachments) {
			$stream = [Convert]::ToBase64String([IO.File]::ReadAllBytes($file))
			Invoke-Azure 'POST' "$base/runs/$runId/results/$($target[0].id)/attachments?$api" @{ stream = $stream; fileName = [IO.Path]::GetFileName($file); comment = $comment; attachmentType = 'GeneralAttachment' } | Out-Null
		}
		if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue }
		$log.published = @($log.published) + $r.CaseReference
		Save-Log
		Write-Host ('  Publicado {0}: {1} ({2} adjunto(s))' -f $r.CaseReference, $r.Outcome, $attachments.Count)
	}
	try { Invoke-Azure 'PATCH' "$base/runs/${runId}?$api" @{ state = 'Completed' } | Out-Null } catch { Write-Warning "No se pudo cerrar la ejecucion $runId (puede estar ya completada): $($_.Exception.Message)" }
	$log.status = 'completed'; Save-Log
	Write-Host "Ejecucion $runId completada. Registro: $logPath"
}
catch {
	$log.status = if (@($log.published).Count -gt 0) { 'partial' } else { 'failed' }
	Save-Log
	Write-Error "Publicacion interrumpida ($($log.status)): $($_.Exception.Message). Registro: $logPath. Para reanudar use -ResumeLog."
	exit 1
}
exit 0
