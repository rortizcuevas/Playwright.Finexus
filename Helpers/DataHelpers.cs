using System.Data;
using System.Text.Json;
using Playwright.Finexus.Interfaces;

namespace Playwright.Finexus.Helpers;

public class DataHelpers : IDataHelper
{
    public DataTable ReadJsonData(string filePath, string testName)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Data file not found: {filePath}");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(filePath));
        if (!document.RootElement.TryGetProperty(testName, out var section) || section.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Section '{testName}' not found or is not an array in {filePath}.");
        }

        var table = new DataTable(testName);
        foreach (var row in section.EnumerateArray())
        {
            foreach (var property in row.EnumerateObject())
            {
                if (!table.Columns.Contains(property.Name))
                {
                    table.Columns.Add(property.Name, typeof(string));
                }
            }

            var dataRow = table.NewRow();
            foreach (var property in row.EnumerateObject())
            {
                dataRow[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Null => string.Empty,
                    _ => property.Value.GetRawText()
                };
            }

            table.Rows.Add(dataRow);
        }

        return table;
    }
}
