<#
.SYNOPSIS
	Funciones auxiliares para Azure DevOps REST 7.1 (Test Plans y Work Items).
.DESCRIPTION
	Cargar con dot-sourcing: . (Join-Path $PSScriptRoot 'AzureDevOpsClient.ps1')
	El PAT se lee exclusivamente de $env:AZURE_DEVOPS_PAT y nunca se imprime.
#>

$script:AdoApiVersion = '7.1'

function Initialize-AdoClient {
	param([Parameter(Mandatory)][string]$Organization, [Parameter(Mandatory)][string]$Project)
	if (-not $env:AZURE_DEVOPS_PAT) { throw 'Defina la variable de entorno AZURE_DEVOPS_PAT.' }
	$script:AdoHeaders = @{ Authorization = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$($env:AZURE_DEVOPS_PAT)")) }
	$script:AdoBase = "https://dev.azure.com/$([uri]::EscapeDataString($Organization))/$([uri]::EscapeDataString($Project))/_apis"
}

function Invoke-AdoRequest {
	param(
		[Parameter(Mandatory)][string]$Method,
		[Parameter(Mandatory)][string]$Path,
		$Body,
		[string]$ContentType = 'application/json',
		[string]$ApiVersion = $script:AdoApiVersion,
		[ref]$ResponseHeaders
	)
	if (-not $script:AdoHeaders) { throw 'Llame primero a Initialize-AdoClient.' }
	$sep = if ($Path.Contains('?')) { '&' } else { '?' }
	$params = @{ Method = $Method; Uri = "$script:AdoBase/$Path$($sep)api-version=$ApiVersion"; Headers = $script:AdoHeaders; ContentType = "$ContentType; charset=utf-8" }
	if ($null -ne $Body) {
		$json = if ($Body -is [string]) { $Body } else { ConvertTo-Json -InputObject $Body -Depth 20 }
		$params.Body = [Text.Encoding]::UTF8.GetBytes($json)
	}
	try {
		if ($ResponseHeaders) {
			$resp = Invoke-WebRequest @params -UseBasicParsing
			$ResponseHeaders.Value = $resp.Headers
			$content = if ($resp.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($resp.Content) } else { $resp.Content }
			if ($content) { $content | ConvertFrom-Json }
		}
		else { Invoke-RestMethod @params }
	}
	catch {
		$detail = $_.ErrorDetails.Message
		throw "Error Azure DevOps [$Method $Path]: $($_.Exception.Message) $detail"
	}
}

function Get-AdoSuites {
	param([Parameter(Mandatory)][int]$PlanId)
	$all = [Collections.Generic.List[object]]::new(); $seen = @{}; $tokens = @{}; $token = $null
	do {
		$path = "testplan/Plans/$PlanId/suites"
		if ($token) { $path += "?continuationToken=$([uri]::EscapeDataString($token))" }
		$headers = $null
		$r = Invoke-AdoRequest -Method Get -Path $path -ResponseHeaders ([ref]$headers)
		foreach ($s in @($r.value)) { if ($s -and -not $seen.ContainsKey($s.id)) { $seen[$s.id] = $true; $all.Add($s) } }
		$token = $null
		if ($headers) {
			$key = @($headers.Keys | Where-Object { $_ -ieq 'x-ms-continuationtoken' })[0]
			if ($key) { $token = @($headers[$key])[0] }
		}
		if ($token -and $tokens.ContainsKey($token)) { throw "Token de continuacion repetido al leer suites del plan $PlanId." }
		if ($token) { $tokens[$token] = $true }
	} while ($token)
	$all.ToArray()
}

function New-AdoSuite {
	param([Parameter(Mandatory)][int]$PlanId, [Parameter(Mandatory)][int]$ParentId, [Parameter(Mandatory)][string]$Name)
	Invoke-AdoRequest -Method Post -Path "testplan/Plans/$PlanId/suites" -Body @{
		suiteType = 'staticTestSuite'; name = $Name; parentSuite = @{ id = $ParentId }
	}
}

function Find-AdoTestCaseByTag {
	param([Parameter(Mandatory)][string]$Tag)
	$escaped = $Tag.Replace("'", "''")
	$wiql = @{ query = "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project AND [System.WorkItemType] = 'Test Case' AND [System.Tags] CONTAINS '$escaped'" }
	$r = Invoke-AdoRequest -Method Post -Path 'wit/wiql' -Body $wiql
	@($r.workItems | ForEach-Object { $_.id })
}

function ConvertTo-AdoStepsXml {
	param([object[]]$Steps)
	$sb = [Text.StringBuilder]::new()
	[void]$sb.Append("<steps id=`"0`" last=`"$($Steps.Count + 1)`">")
	$n = 2
	foreach ($s in $Steps) {
		$a = [Security.SecurityElement]::Escape([Net.WebUtility]::HtmlEncode($s.Action))
		$e = [Security.SecurityElement]::Escape([Net.WebUtility]::HtmlEncode($s.Expected))
		[void]$sb.Append("<step id=`"$n`" type=`"ValidateStep`"><parameterizedString isformatted=`"true`">&lt;P&gt;$a&lt;/P&gt;</parameterizedString><parameterizedString isformatted=`"true`">&lt;P&gt;$e&lt;/P&gt;</parameterizedString><description/></step>")
		$n++
	}
	[void]$sb.Append('</steps>')
	$sb.ToString()
}

function Get-AdoTestCasePatch {
	param([Parameter(Mandatory)]$Case, [Parameter(Mandatory)]$TestCaseSettings, [string[]]$ExtraTags)
	$tags = @($TestCaseSettings.tags) + @("AutoKey:$($Case.CaseKey)") + @($ExtraTags) | Where-Object { $_ }
	$ops = [Collections.Generic.List[object]]::new()
	$add = { param($f, $v) if ($null -ne $v -and "$v" -ne '') { $ops.Add(@{ op = 'add'; path = "/fields/$f"; value = $v }) } }
	& $add 'System.Title' $Case.Title
	& $add 'System.AreaPath' $TestCaseSettings.areaPath
	& $add 'System.IterationPath' $TestCaseSettings.iterationPath
	& $add 'System.AssignedTo' $TestCaseSettings.assignedTo
	& $add 'System.State' $TestCaseSettings.state
	& $add 'System.Tags' ($tags -join '; ')
	& $add 'Microsoft.VSTS.TCM.Steps' (ConvertTo-AdoStepsXml @($Case.Steps))
	& $add 'Microsoft.VSTS.TCM.AutomatedTestName' $Case.FullName
	& $add 'System.Description' "Automatizado: $($Case.FullName)<br/>Archivo: $($Case.RelativePath)<br/>ContentHash: $($Case.ContentHash)"
	, $ops.ToArray()
}

function New-AdoTestCase {
	param([Parameter(Mandatory)]$Case, [Parameter(Mandatory)]$TestCaseSettings)
	$patch = Get-AdoTestCasePatch -Case $Case -TestCaseSettings $TestCaseSettings
	Invoke-AdoRequest -Method Post -Path 'wit/workitems/$Test%20Case' -Body (ConvertTo-Json -InputObject $patch -Depth 10) -ContentType 'application/json-patch+json'
}

function Update-AdoTestCase {
	param([Parameter(Mandatory)][int]$Id, [Parameter(Mandatory)]$Case, [Parameter(Mandatory)]$TestCaseSettings)
	$patch = Get-AdoTestCasePatch -Case $Case -TestCaseSettings $TestCaseSettings
	Invoke-AdoRequest -Method Patch -Path "wit/workitems/$Id" -Body (ConvertTo-Json -InputObject $patch -Depth 10) -ContentType 'application/json-patch+json'
}

function Get-AdoSuiteTestCaseIds {
	param([Parameter(Mandatory)][int]$PlanId, [Parameter(Mandatory)][int]$SuiteId)
	$r = Invoke-AdoRequest -Method Get -Path "testplan/Plans/$PlanId/Suites/$SuiteId/TestCase"
	@($r.value | ForEach-Object { [int]$_.workItem.id })
}

function Add-AdoTestCaseToSuite {
	param([Parameter(Mandatory)][int]$PlanId, [Parameter(Mandatory)][int]$SuiteId, [Parameter(Mandatory)][int]$TestCaseId, [string]$ConfigurationId)
	$item = @{ workItem = @{ id = $TestCaseId } }
	if ($ConfigurationId) { $item.pointAssignments = @(@{ configurationId = [int]$ConfigurationId }) }
	Invoke-AdoRequest -Method Post -Path "testplan/Plans/$PlanId/Suites/$SuiteId/TestCase" -Body (ConvertTo-Json -InputObject @($item) -Depth 10)
}
