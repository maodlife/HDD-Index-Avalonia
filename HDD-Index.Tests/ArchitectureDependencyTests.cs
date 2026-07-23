using System.Reflection;

namespace HDD_Index.Tests;

public class ArchitectureDependencyTests
{
    [Fact]
    public void ServiceLayerPublicAndPrivateApis_DoNotReferenceViewModels()
    {
        var serviceTypes = typeof(Services.DeclarationSyncService).Assembly
            .GetTypes()
            .Where(x => x.Namespace == "HDD_Index.Services")
            .ToList();

        var referencedTypes = serviceTypes.SelectMany(GetReferencedTypes).ToList();

        Assert.DoesNotContain(
            referencedTypes,
            x => x.Namespace?.StartsWith("HDD_Index.ViewModels", StringComparison.Ordinal) == true);
    }

    private static IEnumerable<Type> GetReferencedTypes(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance
                                   | BindingFlags.Static
                                   | BindingFlags.Public
                                   | BindingFlags.NonPublic
                                   | BindingFlags.DeclaredOnly;

        foreach (var field in type.GetFields(flags))
            yield return Unwrap(field.FieldType);
        foreach (var property in type.GetProperties(flags))
            yield return Unwrap(property.PropertyType);
        foreach (var constructor in type.GetConstructors(flags))
        foreach (var parameter in constructor.GetParameters())
            yield return Unwrap(parameter.ParameterType);
        foreach (var method in type.GetMethods(flags))
        {
            yield return Unwrap(method.ReturnType);
            foreach (var parameter in method.GetParameters())
                yield return Unwrap(parameter.ParameterType);
        }
    }

    private static Type Unwrap(Type type)
    {
        while (type.HasElementType)
            type = type.GetElementType()!;
        return type.IsGenericType
            ? type.GetGenericArguments().FirstOrDefault() ?? type
            : type;
    }
}
