<#
.SYNOPSIS
	Genera el catalogo normalizado de casos de prueba a partir de la carpeta Tests (recursivo).
.DESCRIPTION
	Recorre Tests/**/*.cs, detecta metodos de prueba segun discovery.testAttributes, genera un caso
	por metodo o por cada [DataRow] (DisplayName obligatorio), calcula CaseKey, SuitePath, Steps y
	ContentHash. Opcionalmente cruza con TestMetadataExtractor (-AssemblyPath).
	No realiza llamadas a Azure DevOps.
#>
[CmdletBinding()]
param(
	[string]$SettingsPath = (Join-Path $PSScriptRoot 'AzureTestPlanSettings.json'),
	[string]$TestsPath,
	[string]$AssemblyPath,
	[string]$OutputPath,
	[string]$Filter
)

$ErrorActionPreference = 'Stop'
$settings = Get-Content $SettingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$d = $settings.discovery

if (-not $TestsPath) { $TestsPath = $d.testsPath }
if (-not $TestsPath) {
	$dir = $PSScriptRoot
	while ($dir -and -not (Test-Path (Join-Path $dir 'Tests'))) { $dir = Split-Path $dir -Parent }
	if (-not $dir) { throw 'No se encontro la carpeta Tests. Indique -TestsPath.' }
	$TestsPath = Join-Path $dir 'Tests'
}
$TestsPath = (Resolve-Path $TestsPath).Path.TrimEnd('\', '/')

$testAttrs = @($d.testAttributes)
$idRegex = if ($d.idPattern) { [regex]$d.idPattern } else { $null }
$stepRegexes = @($d.stepPatterns | ForEach-Object { [regex]$_ })
$excludes = @($d.excludeFolders)

function ConvertTo-Humanized([string]$name) {
	($name -replace '_', ' ' -replace '([a-z0-9])([A-Z])', '$1 $2').Trim()
}

function Get-Hash([string]$text) {
	$sha = [System.Security.Cryptography.SHA256]::Create()
	try { -join ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)) | ForEach-Object { $_.ToString('x2') }) }
	finally { $sha.Dispose() }
}

function Get-Normalized([string]$text) {
	($text.Trim() -replace '\s+', '-' -replace '[^\w\-]', '' -replace '-{2,}', '-').ToLowerInvariant()
}

function Get-Words([string]$text) {
	$plain = -join ($text.Normalize([Text.NormalizationForm]::FormD).ToCharArray() | Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne 'NonSpacingMark' })
	@($plain.ToLowerInvariant() -split '[^\p{L}\p{N}]+' | Where-Object { $_ })
}

function Test-TitleContainsDisplay([string]$title, [string]$display) {
	$titleWords = Get-Words $title
	$displayWords = Get-Words $display
	if ($displayWords.Count -eq 0) { return $false }
	-not ($displayWords | Where-Object { $_ -notin $titleWords })
}

function Get-MethodBody([string[]]$lines, [int]$start) {
	$depth = 0; $started = $false; $sb = [Text.StringBuilder]::new()
	for ($i = $start; $i -lt $lines.Count; $i++) {
		$l = $lines[$i]; [void]$sb.AppendLine($l)
		foreach ($ch in $l.ToCharArray()) {
			if ($ch -eq '{') { $depth++; $started = $true }
			elseif ($ch -eq '}') { $depth-- }
		}
		if ($started -and $depth -le 0) { break }
		if (-not $started -and $l -match '=>.*;\s*$') { break }
	}
	$sb.ToString()
}

function Get-StepsFromText([string]$body) {
	$found = foreach ($rx in $stepRegexes) {
		foreach ($m in $rx.Matches($body)) { [pscustomobject]@{ Index = $m.Index; Text = $m.Groups['step'].Value } }
	}
	@($found | Sort-Object Index | ForEach-Object { $_.Text })
}

# Metadatos opcionales del ensamblado
$asmMeta = @{}
if ($AssemblyPath) {
	$extractorProj = Join-Path $PSScriptRoot 'TestMetadataExtractor\TestMetadataExtractor.csproj'
	$tmp = [IO.Path]::GetTempFileName()
	& dotnet run --project $extractorProj -- (Resolve-Path $AssemblyPath).Path $tmp ($testAttrs -join ',') | Out-Null
	if ($LASTEXITCODE -ne 0) { throw 'Fallo TestMetadataExtractor.' }
	foreach ($e in (Get-Content $tmp -Raw -Encoding UTF8 | ConvertFrom-Json)) {
		$k = "$($e.fullName)|$($e.dataRowDisplayName)"
		$asmMeta[$k] = $e
	}
	Remove-Item $tmp -Force
}

$catalog = [System.Collections.Generic.List[object]]::new()
$errors = [System.Collections.Generic.List[string]]::new()

$files = Get-ChildItem $TestsPath -Recurse -Filter *.cs -File | Where-Object {
	$rel = $_.FullName.Substring($TestsPath.Length + 1)
	-not ($rel.Split([char[]]'\/') | Select-Object -SkipLast 1 | Where-Object { $_ -in $excludes })
}

foreach ($file in $files) {
	$rel = $file.FullName.Substring($TestsPath.Length + 1).Replace('\', '/')
	$lines = Get-Content $file.FullName -Encoding UTF8
	$text = $lines -join "`n"
	$ns = if ($text -match 'namespace\s+([\w\.]+)') { $Matches[1] } else { '' }

	# Cuerpos de todos los metodos del archivo (para resolver helpers)
	$bodies = @{}
	for ($i = 0; $i -lt $lines.Count; $i++) {
		if ($lines[$i] -match '^\s*(public|private|protected|internal)[^=;]*?\s(\w+)\s*(<[^>]*>)?\s*\(' -and $lines[$i] -notmatch '\b(class|record|struct|new)\b') {
			$bodies[$Matches[2]] = Get-MethodBody $lines $i
		}
	}

	$className = $null; $attrBuffer = [Collections.Generic.List[string]]::new()
	for ($i = 0; $i -lt $lines.Count; $i++) {
		$line = $lines[$i].Trim()
		if ($line -match '\bclass\s+(\w+)') { $className = $Matches[1]; $attrBuffer.Clear(); continue }
		if ($line.StartsWith('[')) { $attrBuffer.Add($line); continue }
		if ($line -eq '' -or $line.StartsWith('//')) { continue }

		if ($attrBuffer.Count -gt 0 -and $line -match '\s(\w+)\s*\(' ) {
			$method = $Matches[1]
			$attrs = $attrBuffer -join ' '
			$attrBuffer.Clear()
			$isTest = $testAttrs | Where-Object { $attrs -match "[\[,]\s*$_\s*(\(|\]|,)" }
			if (-not $isTest) { continue }
			if (-not $d.includeIgnored -and $attrs -match '[\[,]\s*Ignore\b') { continue }

			$fullName = (@($ns, $className, $method) | Where-Object { $_ }) -join '.'
			if ($Filter -and $fullName -notlike "*$Filter*" -and $rel -notlike "*$Filter*") { continue }

			$description = if ($attrs -match 'Description\s*\(\s*"([^"]*)"') { $Matches[1] } else { $null }
			$methodDisplay = if ($attrs -match '(?:TestMethod|DataTestMethod)\s*\(\s*"([^"]*)"') { $Matches[1] } else { $null }
			$caseRef = if ($description -and $idRegex) { $m = $idRegex.Match($description); if ($m.Success) { $m.Groups['id'].Value } }
			$baseTitle = if ($description) { $description } elseif ($methodDisplay) { $methodDisplay } else { ConvertTo-Humanized $method }
			$baseKey = if ($caseRef) { $caseRef } else { $fullName }

			# Pasos: cuerpo del metodo y helpers del mismo archivo
			$body = Get-MethodBody $lines $i
			$steps = Get-StepsFromText $body
			if ($steps.Count -eq 0) {
				foreach ($h in $bodies.Keys) {
					if ($h -ne $method -and $body -match "\b$h\s*\(") { $steps += Get-StepsFromText $bodies[$h] }
				}
			}
			$stepObjs = if ($steps.Count) {
				@($steps | ForEach-Object { [pscustomobject]@{ Action = $_; Expected = 'Paso completado correctamente' } })
			} else {
				@([pscustomobject]@{ Action = "Ejecutar prueba automatizada $fullName"; Expected = 'La prueba finaliza correctamente' })
			}

			$folders = @(($rel -split '/') | Select-Object -SkipLast 1)
			$suitePath = @($folders) + [IO.Path]::GetFileNameWithoutExtension($rel)

			$rows = @([regex]::Matches($attrs, '\[\s*DataRow\s*\((?<args>.*?)\)\s*\]'))
			$variants = if ($rows.Count -eq 0 -or $d.dataRowMode -ne 'perRow') { , @($null) } else { , @(0..($rows.Count - 1)) }

			$seenDisplay = @{}
			foreach ($v in $variants) {
				$rowDisplay = $null; $rowValues = @(); $rowIndex = $null
				if ($null -ne $v) {
					$argsText = $rows[$v].Groups['args'].Value
					$rowIndex = $v
					if ($argsText -match 'DisplayName\s*=\s*"([^"]*)"') { $rowDisplay = $Matches[1] }
					$rowValues = @(($argsText -replace ',?\s*DisplayName\s*=\s*"[^"]*"', '') -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
					if ([string]::IsNullOrWhiteSpace($rowDisplay)) {
						if ($d.requireDataRowDisplayName) { $errors.Add("DataRow sin DisplayName: $fullName (fila $v) en $rel"); continue }
						$rowDisplay = "Fila $v"
					}
					if ($seenDisplay.ContainsKey($rowDisplay)) { $errors.Add("DisplayName duplicado '$rowDisplay' en $fullName"); continue }
					$seenDisplay[$rowDisplay] = $true
				}

				$meta = $asmMeta["$fullName|$rowDisplay"]
				if ($meta -and $meta.description -and -not $description) { $baseTitle = $meta.description }

				$title = if (-not $rowDisplay -or ($rows.Count -eq 1 -and (Test-TitleContainsDisplay $baseTitle $rowDisplay))) { $baseTitle } else { "$baseTitle - $rowDisplay" }
				$caseKey = if ($rowDisplay) { "$baseKey#$(Get-Normalized $rowDisplay)" } else { $baseKey }
				$hashSrc = $title + '|' + (($stepObjs | ForEach-Object { "$($_.Action)>$($_.Expected)" }) -join '|') + '|' + ($rowValues -join ',')

				$catalog.Add([pscustomobject]@{
					CaseKey            = $caseKey
					CaseReference      = $caseRef
					Title              = $title
					FullName           = $fullName
					RelativePath       = $rel
					SuitePath          = $suitePath
					Steps              = $stepObjs
					ContentHash        = Get-Hash $hashSrc
					DataRowDisplayName = $rowDisplay
					DataRowIndex       = $rowIndex
					DataRowValues      = $rowValues
				})
			}
		}
		elseif (-not $line.StartsWith('[')) { $attrBuffer.Clear() }
	}
}

$catalog | Group-Object CaseKey | Where-Object Count -gt 1 | ForEach-Object {
	$errors.Add("CaseKey duplicado '$($_.Name)': $(($_.Group.FullName) -join ', ')")
}

if ($errors.Count) {
	$errors | ForEach-Object { Write-Host "ERROR: $_" -ForegroundColor Red }
	throw "Catalogo invalido: $($errors.Count) error(es). No se realizo ninguna llamada a Azure."
}

Write-Verbose "Casos descubiertos: $($catalog.Count)"
if ($OutputPath) {
	$catalog | ConvertTo-Json -Depth 6 | Set-Content $OutputPath -Encoding UTF8
	Write-Host "Catalogo generado: $OutputPath ($($catalog.Count) casos)"
}
else { $catalog }
