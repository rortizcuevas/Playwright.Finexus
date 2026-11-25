# Valida el mapeo TC-MODULO-## -> Azure DevOps contra las pruebas compiladas. No ejecuta pruebas ni contacta con Azure.
# Uso: .\Validate-AzureTestPlanMapping.ps1 -AssemblyPath <dll de pruebas> [-CasePrefix 'TC-DOCUMENTOS-CONSULTADOCUMENTOS-']
param(
	[Parameter(Mandatory)][string]$AssemblyPath,
	[string]$MappingPath,
	[string]$ExtractorDll,
	[string[]]$CasePrefix = @(),
	[switch]$Quiet
)
$ErrorActionPreference = 'Stop'
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
if (-not $MappingPath) { $MappingPath = Join-Path $scriptDir 'AzureTestPlanMapping.json' }
if (-not $ExtractorDll) { $ExtractorDll = Join-Path $scriptDir 'output/extractor/TestMetadataExtractor.dll' }
$errors = [System.Collections.Generic.List[string]]::new()

if (-not (Test-Path $AssemblyPath)) { throw "No existe el ensamblado: $AssemblyPath" }
if (-not (Test-Path $MappingPath)) { throw "No existe el mapeo: $MappingPath" }
if (-not (Test-Path $ExtractorDll)) {
	$project = Join-Path $scriptDir 'TestMetadataExtractor'
	& dotnet build $project -nologo -v q -o (Split-Path $ExtractorDll) | Out-Null
	if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar el extractor.' }
}

$tempFile = [System.IO.Path]::GetTempFileName()
try {
	& dotnet $ExtractorDll (Resolve-Path $AssemblyPath).Path $tempFile
	if ($LASTEXITCODE -ne 0) { throw 'El extractor ha fallado.' }
	$methods = @(ConvertFrom-Json (Get-Content $tempFile -Raw -Encoding UTF8)) | ForEach-Object { $_ }
}
finally { Remove-Item $tempFile -ErrorAction SilentlyContinue }

$mapping = @((Get-Content $MappingPath -Raw -Encoding UTF8 | ConvertFrom-Json).azureTestPlanMapping)

function Test-InScope([string]$id) {
	if ($CasePrefix.Count -eq 0) { return $true }
	foreach ($prefix in $CasePrefix) { if ($id.StartsWith($prefix, [System.StringComparison]::Ordinal)) { return $true } }
	return $false
}

foreach ($m in ($methods | Where-Object { -not $_.caseReference })) {
	if ($CasePrefix.Count -eq 0) { $errors.Add("Metodo sin ID TC-MODULO-## valido en [Description]: $($m.fullName)") }
}

$withId = @($methods | Where-Object { $_.caseReference })
foreach ($group in ($withId | Group-Object { "$($_.caseReference)|$($_.fullName)|$($_.dataRowIndex)" } | Where-Object Count -gt 1)) {
	$errors.Add("ID duplicado $($group.Group[0].caseReference) en: $((($group.Group | ForEach-Object fullName) -join ', '))")
}
foreach ($group in ($withId | Group-Object caseReference | Where-Object { @($_.Group | ForEach-Object fullName | Select-Object -Unique).Count -gt 1 })) {
	$errors.Add("ID duplicado $($group.Name) en: $((($group.Group | ForEach-Object fullName | Select-Object -Unique) -join ', '))")
}

$mappedIds = @($mapping | ForEach-Object caseReference)
$keyField = if ($mapping.Count -gt 0 -and $mapping[0].PSObject.Properties['caseKey']) { 'caseKey' } else { 'caseReference' }
foreach ($group in ($mapping | Group-Object $keyField | Where-Object Count -gt 1)) {
	$errors.Add("Entrada de mapeo duplicada: $($group.Name)")
}
foreach ($group in ($mapping | Group-Object azureTestCaseId | Where-Object Count -gt 1)) {
	$errors.Add("azureTestCaseId duplicado: $($group.Name)")
}
foreach ($group in ($withId | Group-Object caseReference)) {
	$expected = $group.Count
	$actual = @($mapping | Where-Object { $_.caseReference -eq $group.Name }).Count
	if ((Test-InScope $group.Name) -and $actual -gt 0 -and $actual -ne $expected) {
		$errors.Add("Numero de entradas de mapeo ($actual) distinto del de casos en codigo ($expected) para $($group.Name)")
	}
}
foreach ($m in $withId) {
	if ((Test-InScope $m.caseReference) -and ($mappedIds -notcontains $m.caseReference)) {
		$errors.Add("ID presente en el codigo y ausente del mapeo: $($m.caseReference) ($($m.fullName))")
	}
}
$codeIds = @($withId | ForEach-Object caseReference)
foreach ($entry in $mapping) {
	if ($codeIds -notcontains $entry.caseReference) { $errors.Add("Entrada del mapeo sin prueba correspondiente: $($entry.caseReference)") }
	foreach ($field in 'azureTestCaseId', 'suiteId') {
		$value = [string]$entry.$field
		if ([string]::IsNullOrWhiteSpace($value) -or $value -match '^0+$' -or $value -notmatch '^\d+$') {
			$errors.Add("Valor invalido en '$field' para $($entry.caseReference): '$value'")
		}
	}
}

if (-not $Quiet) {
	Write-Host "Metodos de prueba: $($methods.Count); con ID: $($withId.Count); entradas de mapeo: $($mapping.Count)."
}
if ($errors.Count -gt 0) {
	$errors | ForEach-Object { Write-Error $_ -ErrorAction Continue }
	Write-Host "Validacion fallida: $($errors.Count) problema(s)."
	exit 1
}
Write-Host 'Validacion correcta.'
exit 0
