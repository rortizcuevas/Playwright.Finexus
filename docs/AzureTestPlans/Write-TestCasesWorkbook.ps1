function Write-TestCasesWorkbook {
	param(
		[Parameter(Mandatory = $true)][string]$Path,
		[Parameter(Mandatory = $true)][object[]]$Rows
	)
	Add-Type -AssemblyName System.IO.Compression
	$columns = @('ID', 'Work Item Type', 'Title', 'Test Step', 'Step Action', 'Step Expected', 'Area Path', 'Assigned To', 'State')
	$utf8 = [System.Text.UTF8Encoding]::new($false)
	$stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create)
	$archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create)
	try {
		$parts = @{
			'[Content_Types].xml' = '<?xml version="1.0" encoding="utf-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>'
			'_rels/.rels' = '<?xml version="1.0" encoding="utf-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>'
			'xl/workbook.xml' = '<?xml version="1.0" encoding="utf-8"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Test Cases" sheetId="1" r:id="rId1"/></sheets></workbook>'
			'xl/_rels/workbook.xml.rels' = '<?xml version="1.0" encoding="utf-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>'
		}
		foreach ($part in $parts.GetEnumerator()) {
			$entry = $archive.CreateEntry($part.Key)
			$writer = [System.IO.StreamWriter]::new($entry.Open(), $utf8)
			try { $writer.Write($part.Value) } finally { $writer.Dispose() }
		}
		$entry = $archive.CreateEntry('xl/worksheets/sheet1.xml')
		$settings = [System.Xml.XmlWriterSettings]::new()
		$settings.Encoding = $utf8
		$settings.CloseOutput = $true
		$writer = [System.Xml.XmlWriter]::Create($entry.Open(), $settings)
		try {
			$ns = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
			$writer.WriteStartDocument()
			$writer.WriteStartElement('worksheet', $ns)
			$writer.WriteStartElement('sheetData', $ns)
			for ($index = 0; $index -le $Rows.Count; $index++) {
				$writer.WriteStartElement('row', $ns)
				$writer.WriteAttributeString('r', ($index + 1).ToString())
				for ($column = 0; $column -lt $columns.Count; $column++) {
					$value = if ($index -eq 0) { $columns[$column] } else { [string]$Rows[$index - 1].($columns[$column]) }
					if ($value.Length -gt 32767) { throw "La celda supera el máximo de caracteres de Excel: fila $($index + 1)." }
					$writer.WriteStartElement('c', $ns)
					$writer.WriteAttributeString('r', ([string][char](65 + $column) + ($index + 1)))
					$writer.WriteAttributeString('t', 'inlineStr')
					$writer.WriteStartElement('is', $ns)
					$writer.WriteStartElement('t', $ns)
					$writer.WriteString($value)
					$writer.WriteEndElement()
					$writer.WriteEndElement()
					$writer.WriteEndElement()
				}
				$writer.WriteEndElement()
			}
			$writer.WriteEndElement()
			$writer.WriteEndElement()
			$writer.WriteEndDocument()
		} finally { $writer.Dispose() }
	} finally {
		$archive.Dispose()
		$stream.Dispose()
	}
	$stream = [System.IO.File]::OpenRead($Path)
	$archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Read)
	try {
		foreach ($entry in $archive.Entries) {
			$reader = [System.IO.StreamReader]::new($entry.Open(), $utf8)
			try { $xml = [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
			if ($entry.FullName -eq 'xl/worksheets/sheet1.xml') {
				$sheetRows = @($xml.worksheet.sheetData.row)
				if ($sheetRows.Count -ne ($Rows.Count + 1)) { throw "Número de filas XLSX incorrecto: $Path" }
				for ($index = 0; $index -lt $sheetRows.Count; $index++) {
					$cells = @($sheetRows[$index].c)
					if ($cells.Count -ne $columns.Count) { throw "Número de columnas XLSX incorrecto: $Path" }
					for ($column = 0; $column -lt $columns.Count; $column++) {
						$expected = if ($index -eq 0) { $columns[$column] } else { [string]$Rows[$index - 1].($columns[$column]) }
						if ([string]$cells[$column].is.t -cne $expected) { throw "Contenido XLSX incorrecto: fila $index, columna $column." }
					}
				}
			}
		}
	} finally {
		$archive.Dispose()
		$stream.Dispose()
	}
}
