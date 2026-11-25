using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;

// Uso: TestMetadataExtractor <ruta-ensamblado-de-pruebas> [ruta-salida.json] [atributos-separados-por-coma]
// Emite un JSON con metodo, clase, [Description], ID TC-MODULO-## y una entrada por cada [DataRow].
if (args.Length < 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Uso: TestMetadataExtractor <ensamblado> [salida.json] [TestMethod,DataTestMethod,...]");
    return 2;
}

var testAttributeNames = (args.Length > 2 && !string.IsNullOrWhiteSpace(args[2])
        ? args[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        : ["TestMethod", "DataTestMethod", "Test", "TestCase", "Fact", "Theory"])
    .Select(n => n.EndsWith("Attribute", StringComparison.Ordinal) ? n : n + "Attribute")
    .ToHashSet(StringComparer.Ordinal);

var assemblyPath = Path.GetFullPath(args[0]);
var idPattern = new Regex(@"^(?<id>TC-[A-Z0-9]+(?:-[A-Z0-9]+)*-\d+)\b", RegexOptions.Compiled);
var context = new TestAssemblyLoadContext(assemblyPath);
var assembly = context.LoadFromAssemblyPath(assemblyPath);

Type[] types;
try
{
    types = assembly.GetTypes();
}
catch (ReflectionTypeLoadException ex)
{
    types = ex.Types.Where(t => t is not null).ToArray()!;
}

var results = new List<object>();
foreach (var type in types.OrderBy(t => t.FullName, StringComparer.Ordinal))
{
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                 .OrderBy(m => m.Name, StringComparer.Ordinal))
    {
        var attributes = CustomAttributeData.GetCustomAttributes(method);
        if (!attributes.Any(a => IsTestMethod(a.AttributeType, testAttributeNames)))
        {
            continue;
        }

        var description = attributes
            .Where(a => a.AttributeType.FullName == "Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute")
            .Select(a => a.ConstructorArguments.FirstOrDefault().Value as string)
            .FirstOrDefault();

        var displayName = attributes
            .Where(a => a.AttributeType.FullName == "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute"
                        || a.AttributeType.FullName == "Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute")
            .SelectMany(a => a.NamedArguments)
            .Where(n => n.MemberName == "DisplayName")
            .Select(n => n.TypedValue.Value as string)
            .FirstOrDefault();

        var match = description is null ? null : idPattern.Match(description);
        var caseReference = match is { Success: true } ? match.Groups["id"].Value : null;
        var dataRows = attributes
            .Where(a => a.AttributeType.FullName == "Microsoft.VisualStudio.TestTools.UnitTesting.DataRowAttribute")
            .ToList();

        if (dataRows.Count == 0)
        {
            results.Add(new
            {
                @namespace = type.Namespace,
                className = type.FullName,
                method = method.Name,
                fullName = $"{type.FullName}.{method.Name}",
                description,
                displayName,
                caseReference,
                dataRowIndex = (int?)null,
                dataRowDisplayName = (string?)null,
                dataRowValues = (object?[]?)null
            });
            continue;
        }

        for (var i = 0; i < dataRows.Count; i++)
        {
            var row = dataRows[i];
            results.Add(new
            {
                @namespace = type.Namespace,
                className = type.FullName,
                method = method.Name,
                fullName = $"{type.FullName}.{method.Name}",
                description,
                displayName,
                caseReference,
                dataRowIndex = (int?)i,
                dataRowDisplayName = row.NamedArguments
                    .Where(n => n.MemberName == "DisplayName")
                    .Select(n => n.TypedValue.Value as string)
                    .FirstOrDefault(),
                dataRowValues = (object?[]?)row.ConstructorArguments.SelectMany(FlattenArgument).ToArray()
            });
        }
    }
}

var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
if (args.Length > 1)
{
    File.WriteAllText(args[1], json, new System.Text.UTF8Encoding(false));
}
else
{
    Console.OutputEncoding = new System.Text.UTF8Encoding(false);
    Console.WriteLine(json);
}

return 0;

static bool IsTestMethod(Type? type, HashSet<string> names)
{
    for (var current = type; current is not null; current = current.BaseType)
    {
        if (names.Contains(current.Name))
        {
            return true;
        }
    }

    return false;
}

static IEnumerable<object?> FlattenArgument(CustomAttributeTypedArgument argument)
{
    if (argument.Value is IReadOnlyCollection<CustomAttributeTypedArgument> items)
    {
        return items.Select(item => ToSerializable(item.Value));
    }

    return [ToSerializable(argument.Value)];
}

static object? ToSerializable(object? value) => value switch
{
    null => null,
    Type t => t.FullName,
    string or bool or char => value,
    _ when value.GetType().IsPrimitive || value is decimal => value,
    _ => value.ToString()
};

sealed class TestAssemblyLoadContext(string assemblyPath) : AssemblyLoadContext(isCollectible: false)
{
    private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);
    private readonly string _directory = Path.GetDirectoryName(assemblyPath)!;

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is null)
        {
            var candidate = Path.Combine(_directory, assemblyName.Name + ".dll");
            path = File.Exists(candidate) ? candidate : null;
        }

        return path is null ? null : LoadFromAssemblyPath(path);
    }
}
