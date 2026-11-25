<#
.SYNOPSIS
	Crea/actualiza casos de prueba en Azure DevOps Test Plans a partir del catalogo de Tests.
.DESCRIPTION
	- Cada carpeta bajo Tests es una suite; cada archivo .cs es una suite; cada metodo/DataRow es un caso.
	- Identidad: CaseKey (mapeo AzureTestPlanMapping.json, o etiqueta AutoKey:<CaseKey>).
	- -DryRun: solo muestra las acciones (sin llamadas HTTP ni PAT).
	- -UpdateExisting: actualiza casos existentes cuyo ContentHash haya cambiado.
	- -Filter: limita por FullName o ruta relativa.
#>
[CmdletBinding()]
param(
	[string]$SettingsPath = (Join-Path $PSScriptRoot 'AzureTestPlanSettings.json'),
	[string]$MappingPath = (Join-Path $PSScriptRoot 'AzureTestPlanMapping.json'),
	[string]$TestsPath,
	[string]$AssemblyPath,
	[string]$Filter,
	[switch]$DryRun,
	[switch]$UpdateExisting
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AzureDevOpsClient.ps1')

$settings = Get-Content $SettingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$planId = [int]$settings.testPlanId
if ($planId -le 0 -and -not $DryRun) { throw 'testPlanId no configurado en AzureTestPlanSettings.json. Use -DryRun o configure el ID del plan.' }

$catArgs = @{ SettingsPath = $SettingsPath }
if ($TestsPath) { $catArgs.TestsPath = $TestsPath }
if ($AssemblyPath) { $catArgs.AssemblyPath = $AssemblyPath }
if ($Filter) { $catArgs.Filter = $Filter }
$catalog = @(& (Join-Path $PSScriptRoot 'Get-TestCatalog.ps1') @catArgs)
Write-Host "Casos en catalogo: $($catalog.Count)"

# Mapeo
$mappingDoc = if (Test-Path $MappingPath) { Get-Content $MappingPath -Raw -Encoding UTF8 | ConvertFrom-Json } else { [pscustomobject]@{ azureTestPlanMapping = @() } }
$mapping = @{}
foreach ($m in @($mappingDoc.azureTestPlanMapping)) {
	$k = if ($m.caseKey) { $m.caseKey } else { $m.caseReference }
	if ($k) { $mapping[$k] = $m }
}

# Suites
$suiteMap = @{}
if ($settings.suites) { foreach ($p in $settings.suites.PSObject.Properties) { $suiteMap[$p.Name] = [int]$p.Value } }
$remoteSuites = @(); $rootSuiteId = 0
if (-not $DryRun) {
	Initialize-AdoClient -Organization $settings.organization -Project $settings.project
	$remoteSuites = @(Get-AdoSuites -PlanId $planId)
	$rootSuiteId = [int](@($remoteSuites | Where-Object { -not $_.parentSuite })[0].id)
}
$dryId = -1

function Resolve-Suite([string[]]$path) {
	$parentId = $rootSuiteId; $key = ''
	foreach ($name in $path) {
		$key = if ($key) { "$key/$name" } else { $name }
		if ($suiteMap.ContainsKey($key)) { $parentId = $suiteMap[$key]; continue }
		$existing = @($remoteSuites | Where-Object { $_.name -eq $name -and $_.parentSuite -and [int]$_.parentSuite.id -eq $parentId })
		if ($existing.Count) { $id = [int]$existing[0].id }
		elseif ($DryRun) { Write-Host "[DryRun] Crear suite '$key'"; $id = $script:dryId; $script:dryId-- ; $script:stats.SuitesCreated++ }
		else {
			$s = New-AdoSuite -PlanId $planId -ParentId $parentId -Name $name
			$id = [int]$s.id; $script:remoteSuites += $s; $script:stats.SuitesCreated++
			Write-Host "Suite creada '$key' ($id)"
		}
		$suiteMap[$key] = $id; $parentId = $id
	}
	$parentId
}

$stats = [ordered]@{ SuitesCreated = 0; Created = 0; Updated = 0; Unchanged = 0; Skipped = 0; AddedToSuite = 0; Errors = 0 }
$suiteCases = @{}

foreach ($case in $catalog) {
	try {
		$suiteId = Resolve-Suite @($case.SuitePath)
		$entry = $mapping[$case.CaseKey]
		if (-not $entry -and $case.CaseReference -and -not $case.DataRowDisplayName) { $entry = $mapping[$case.CaseReference] }
		$id = if ($entry -and $entry.azureTestCaseId) { [int]$entry.azureTestCaseId } else { 0 }
		if (-not $id -and -not $DryRun) {
			$found = @(Find-AdoTestCaseByTag -Tag "AutoKey:$($case.CaseKey)")
			if ($found.Count) { $id = [int]$found[0] }
		}

		if (-not $id) {
			if ($DryRun) { Write-Host "[DryRun] Crear caso '$($case.Title)' [$($case.CaseKey)] en '$($case.SuitePath -join '/')'"; $id = $dryId; $dryId-- }
			else { $id = [int](New-AdoTestCase -Case $case -TestCaseSettings $settings.testCase).id; Write-Host "Caso creado $id '$($case.Title)'" }
			$stats.Created++
		}
		elseif ($entry -and $entry.contentHash -eq $case.ContentHash) { $stats.Unchanged++ }
		elseif ($UpdateExisting) {
			if ($DryRun) { Write-Host "[DryRun] Actualizar caso $id '$($case.Title)'" }
			else { [void](Update-AdoTestCase -Id $id -Case $case -TestCaseSettings $settings.testCase); Write-Host "Caso actualizado $id '$($case.Title)'" }
			$stats.Updated++
		}
		else { Write-Verbose "Omitido $id (use -UpdateExisting)"; $stats.Skipped++ }

		if (-not $DryRun -and $suiteId -gt 0) {
			if (-not $suiteCases.ContainsKey($suiteId)) { $suiteCases[$suiteId] = @(Get-AdoSuiteTestCaseIds -PlanId $planId -SuiteId $suiteId) }
			if ($id -notin $suiteCases[$suiteId]) {
				[void](Add-AdoTestCaseToSuite -PlanId $planId -SuiteId $suiteId -TestCaseId $id -ConfigurationId $settings.configurationId)
				$suiteCases[$suiteId] += $id; $stats.AddedToSuite++
			}
		}

		if ($id -gt 0) {
			$mapping[$case.CaseKey] = [pscustomobject]@{
				caseKey = $case.CaseKey; caseReference = $case.CaseReference; title = $case.Title
				azureTestCaseId = $id; suiteId = $suiteId
				configurationId = if ($entry) { $entry.configurationId } else { '' }
				contentHash = if ($UpdateExisting -or -not $entry) { $case.ContentHash } else { $entry.contentHash }
			}
			if ($case.CaseReference -and $case.CaseReference -ne $case.CaseKey) { $mapping.Remove($case.CaseReference) }
		}
	}
	catch { $stats.Errors++; Write-Warning "[$($case.CaseKey)] $($_.Exception.Message)" }
}

if (-not $DryRun) {
	$suitesObj = [ordered]@{}; foreach ($k in ($suiteMap.Keys | Sort-Object)) { $suitesObj[$k] = $suiteMap[$k] }
	$settings | Add-Member -NotePropertyName suites -NotePropertyValue ([pscustomobject]$suitesObj) -Force
	$settings | ConvertTo-Json -Depth 20 | Set-Content $SettingsPath -Encoding UTF8
	$mappingDoc | Add-Member -NotePropertyName azureTestPlanMapping -NotePropertyValue @($mapping.Values | Sort-Object caseKey) -Force
	$mappingDoc | ConvertTo-Json -Depth 20 | Set-Content $MappingPath -Encoding UTF8
}

Write-Host ''
Write-Host ("Resumen{0}:" -f $(if ($DryRun) { ' (DryRun)' } else { '' }))
$stats.GetEnumerator() | ForEach-Object { Write-Host ("  {0,-14} {1}" -f $_.Key, $_.Value) }
if ($stats.Errors) { exit 1 }
