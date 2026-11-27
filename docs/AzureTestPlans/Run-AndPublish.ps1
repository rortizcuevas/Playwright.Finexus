
# Ejemplo: -Filter AuthLoginTests,SigningPdfValidE2ETests
# Sin -Filter se ejecutan todas las pruebas.
param(
	[string[]]$Filter = @(),
	[string[]]$CasePrefix = @(),
	[string]$Configuration = 'Debug',
	[switch]$Publish
)
$ErrorActionPreference = 'Stop'
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$projectDir = (Resolve-Path (Join-Path $scriptDir '../..')).Path
$project = Join-Path $projectDir 'Playwright.Finexus.csproj'
$assembly = Join-Path $projectDir "bin/$Configuration/net9.0/Playwright.Finexus.dll"
$trxDir = Join-Path $scriptDir 'output/trx'
$trxName = 'run-{0}.trx' -f (Get-Date -Format 'yyyyMMdd-HHmmss')

& dotnet build $project -c $Configuration -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'La compilacion ha fallado.' }

& (Join-Path $scriptDir 'Validate-AzureTestPlanMapping.ps1') -AssemblyPath $assembly -CasePrefix $CasePrefix
if ($LASTEXITCODE -ne 0) { throw 'La validacion del mapeo ha fallado.' }

$runSettings = Join-Path $trxDir 'evidence.runsettings'
New-Item -ItemType Directory -Force $trxDir | Out-Null
[xml]$settingsXml = Get-Content (Join-Path $projectDir 'playwright.runsettings') -Raw -Encoding UTF8
$param = $settingsXml.SelectSingleNode("//TestRunParameters/Parameter[@name='captureStepEvidence']")
if (-not $param) {
	$container = $settingsXml.SelectSingleNode('//TestRunParameters')
	if (-not $container) { throw 'playwright.runsettings no define TestRunParameters.' }
	$param = $settingsXml.CreateElement('Parameter')
	$param.SetAttribute('name', 'captureStepEvidence')
	[void]$container.AppendChild($param)
}
$param.SetAttribute('value', 'true')
$resultsDir = $settingsXml.SelectSingleNode('//RunConfiguration/ResultsDirectory')
if ($resultsDir) { $resultsDir.InnerText = Join-Path $projectDir $resultsDir.InnerText }
$settingsXml.Save($runSettings)
$filterArgs = @()
if ($Filter.Count -gt 0) {
	$expression = ($Filter | ForEach-Object { "FullyQualifiedName~$_" }) -join '|'
	$filterArgs = @('--filter', $expression)
}
& dotnet test $project -c $Configuration --no-build --settings $runSettings `
	@filterArgs --logger "trx;LogFileName=$trxName" --results-directory $trxDir
$testExit = $LASTEXITCODE
Remove-Item $runSettings -ErrorAction SilentlyContinue
$trx = Join-Path $trxDir $trxName
if (-not (Test-Path $trx)) { throw 'No se genero el archivo .trx.' }
Write-Host "Resultados: $trx (dotnet test exit=$testExit)"

if (-not $Publish) {
	Write-Host 'No se ha publicado nada. Use -Publish para enviar los resultados a Azure.'
	exit $testExit
}
& (Join-Path $scriptDir 'Publish-AzureTestPlan.ps1') -TrxPath $trx -AssemblyPath $assembly -CasePrefix $CasePrefix
$publishExit = $LASTEXITCODE
if ($publishExit -ne 0) { exit $publishExit }
exit $testExit
