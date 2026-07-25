using System.Reflection;
using System.Reflection.Emit;
using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

public class ArchitectureDependencyTests
{
    private const string RootNamespace = "HDD_Index";
    private const string ModelsNamespace = RootNamespace + ".Models";
    private const string ApplicationNamespace = RootNamespace + ".Application";
    private const string ServicesNamespace = RootNamespace + ".Services";
    private const string MessagesNamespace = RootNamespace + ".Messages";
    private const string ViewModelsNamespace = RootNamespace + ".ViewModels";
    private const string ViewsNamespace = RootNamespace + ".Views";

    private static readonly Assembly ApplicationAssembly =
        typeof(Services.DeclarationSyncService).Assembly;

    private static readonly string[] LayerNamespaces =
    [
        ModelsNamespace,
        ApplicationNamespace,
        ServicesNamespace,
        MessagesNamespace,
        ViewModelsNamespace,
        ViewsNamespace,
    ];

    private static readonly string[] UiFrameworkNamespaces =
    [
        "Avalonia",
        "CommunityToolkit.Mvvm",
        "DynamicData",
        "ReactiveUI",
        "Xaml.Behaviors",
    ];

    private static readonly IReadOnlyDictionary<short, OpCode> OpCodesByValue =
        typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(opCode => opCode.Value);

    [Fact]
    public void Models_DoNotDependOnHigherLayers()
    {
        AssertNoDependencies(
            [ModelsNamespace],
            [
                ApplicationNamespace,
                ServicesNamespace,
                MessagesNamespace,
                ViewModelsNamespace,
                ViewsNamespace,
            ]);
    }

    [Fact]
    public void ApplicationLayer_DoesNotDependOnHigherLayers()
    {
        AssertNoDependencies(
            [ApplicationNamespace],
            [
                ServicesNamespace,
                MessagesNamespace,
                ViewModelsNamespace,
                ViewsNamespace,
            ]);
    }

    [Fact]
    public void Services_DoNotDependOnPresentationLayers()
    {
        AssertNoDependencies(
            [ServicesNamespace],
            [
                MessagesNamespace,
                ViewModelsNamespace,
                ViewsNamespace,
            ]);
    }

    [Fact]
    public void Views_DoNotDependOnApplicationOrServices()
    {
        AssertNoDependencies(
            [ViewsNamespace],
            [
                ApplicationNamespace,
                ServicesNamespace,
            ]);
    }

    [Fact]
    public void MainWindowViewModel_IsTheOnlyViewModelThatDependsOnViews()
    {
        // MainWindowViewModel currently owns dialog creation. Keep this exact
        // exception visible until dialog coordination moves out of ViewModels.
        var actualOffenders = FindDependencyViolations(
                [ViewModelsNamespace],
                [ViewsNamespace])
            .Select(violation => violation.SourceType)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(typeName => typeName, StringComparer.Ordinal)
            .ToList();
        var expectedOffenders = new[]
        {
            typeof(MainWindowViewModel).FullName!,
        };

        Assert.Equal(expectedOffenders, actualOffenders);
    }

    [Fact]
    public void Messages_DoNotDependOnOtherApplicationLayers()
    {
        AssertNoDependencies(
            [MessagesNamespace],
            [
                ModelsNamespace,
                ApplicationNamespace,
                ServicesNamespace,
                ViewModelsNamespace,
                ViewsNamespace,
            ]);
    }

    [Fact]
    public void CoreLayersAndMessages_DoNotDependOnUiFrameworks()
    {
        AssertNoDependencies(
            [
                ModelsNamespace,
                ApplicationNamespace,
                ServicesNamespace,
                MessagesNamespace,
            ],
            UiFrameworkNamespaces);
    }

    [Fact]
    public void ApplicationLayers_DoNotDependOnTheCompositionRoot()
    {
        var violations = FindDependencyViolations(
            LayerNamespaces,
            referencedType => referencedType.Namespace == RootNamespace);

        AssertNoViolations(violations);
    }

    [Fact]
    public void CompositionRoot_ContainsOnlyStartupTypes()
    {
        var actualRootTypes = ApplicationAssembly
            .GetTypes()
            .Where(type => type.Namespace == RootNamespace)
            .Select(GetArchitectureOwnerName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(typeName => typeName, StringComparer.Ordinal)
            .ToList();
        var expectedRootTypes = new[]
        {
            RootNamespace + ".App",
            RootNamespace + ".Program",
            RootNamespace + ".ViewLocator",
        };

        Assert.Equal(expectedRootTypes, actualRootTypes);
    }

    [Fact]
    public void ProductNamespaces_AreAssignedToAKnownLayer()
    {
        var unknownNamespaces = ApplicationAssembly
            .GetTypes()
            .Select(type => type.Namespace)
            .Where(typeNamespace =>
                typeNamespace?.StartsWith(RootNamespace + ".", StringComparison.Ordinal) == true)
            .Where(typeNamespace =>
                !LayerNamespaces.Any(layerNamespace =>
                    IsNamespaceOrChild(typeNamespace!, layerNamespace)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(typeNamespace => typeNamespace, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unknownNamespaces.Count == 0,
            "Namespaces without an architecture-layer rule:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, unknownNamespaces));
    }

    [Fact]
    public void ViewModelNamedClasses_InheritViewModelBase()
    {
        var violations = ApplicationAssembly
            .GetTypes()
            .Where(type => IsInNamespace(type, ViewModelsNamespace))
            .Where(type => type.IsClass)
            .Where(type =>
                type.Name.EndsWith("ViewModel", StringComparison.Ordinal)
                || type.Name.EndsWith("VM", StringComparison.Ordinal))
            .Where(type => !typeof(ViewModelBase).IsAssignableFrom(type))
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(typeName => typeName, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            violations.Count == 0,
            "ViewModel-named classes that do not inherit ViewModelBase:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void DependencyScanner_InspectsNestedGenericsAndMethodBodies()
    {
        var referencedTypes = GetReferencedTypes(typeof(DependencyScannerProbe));

        Assert.Contains(typeof(GenericDependencyProbe), referencedTypes);
        Assert.Contains(typeof(MethodBodyDependencyProbe), referencedTypes);
    }

    private static void AssertNoDependencies(
        IReadOnlyCollection<string> sourceNamespaces,
        IReadOnlyCollection<string> forbiddenNamespacePrefixes)
    {
        var violations = FindDependencyViolations(
            sourceNamespaces,
            forbiddenNamespacePrefixes);

        AssertNoViolations(violations);
    }

    private static void AssertNoViolations(
        IReadOnlyCollection<DependencyViolation> violations)
    {
        Assert.True(
            violations.Count == 0,
            "Forbidden architecture dependencies:"
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                violations.Select(violation =>
                    $"{violation.SourceType} -> {violation.ReferencedType}")));
    }

    private static IReadOnlyList<DependencyViolation> FindDependencyViolations(
        IReadOnlyCollection<string> sourceNamespaces,
        IReadOnlyCollection<string> forbiddenNamespacePrefixes)
    {
        return FindDependencyViolations(
            sourceNamespaces,
            referencedType => forbiddenNamespacePrefixes.Any(
                prefix => IsInNamespace(referencedType, prefix)));
    }

    private static IReadOnlyList<DependencyViolation> FindDependencyViolations(
        IReadOnlyCollection<string> sourceNamespaces,
        Func<Type, bool> isForbiddenDependency)
    {
        return ApplicationAssembly
            .GetTypes()
            .Where(type => sourceNamespaces.Any(source => IsInNamespace(type, source)))
            .SelectMany(sourceType => GetReferencedTypes(sourceType)
                .Where(isForbiddenDependency)
                .Select(referencedType => new DependencyViolation(
                    GetArchitectureOwnerName(sourceType),
                    referencedType.FullName ?? referencedType.Name)))
            .Distinct()
            .OrderBy(violation => violation.SourceType, StringComparer.Ordinal)
            .ThenBy(violation => violation.ReferencedType, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyCollection<Type> GetReferencedTypes(Type sourceType)
    {
        // Inspect both reflection metadata and IL. Signature-only inspection
        // misses dependencies created or called exclusively inside method bodies.
        // This is a binary dependency check: unused usings, inlined constants,
        // nameof/string reflection, and source-only XAML references have no IL
        // type token and therefore remain the compiler's responsibility.
        var referencedTypes = new HashSet<Type>();

        AddType(sourceType.BaseType, referencedTypes);
        foreach (var interfaceType in sourceType.GetInterfaces())
            AddType(interfaceType, referencedTypes);
        AddGenericParameterConstraints(sourceType.GetGenericArguments(), referencedTypes);
        AddCustomAttributes(sourceType.CustomAttributes, referencedTypes);

        const BindingFlags flags = BindingFlags.Instance
                                   | BindingFlags.Static
                                   | BindingFlags.Public
                                   | BindingFlags.NonPublic
                                   | BindingFlags.DeclaredOnly;

        foreach (var field in sourceType.GetFields(flags))
        {
            AddType(field.FieldType, referencedTypes);
            AddCustomAttributes(field.CustomAttributes, referencedTypes);
        }

        foreach (var property in sourceType.GetProperties(flags))
        {
            AddType(property.PropertyType, referencedTypes);
            foreach (var parameter in property.GetIndexParameters())
                AddParameter(parameter, referencedTypes);
            AddCustomAttributes(property.CustomAttributes, referencedTypes);
        }

        foreach (var eventInfo in sourceType.GetEvents(flags))
        {
            AddType(eventInfo.EventHandlerType, referencedTypes);
            AddCustomAttributes(eventInfo.CustomAttributes, referencedTypes);
        }

        foreach (var constructor in sourceType.GetConstructors(flags))
            AddMethod(constructor, referencedTypes);

        if (sourceType.TypeInitializer is not null)
            AddMethod(sourceType.TypeInitializer, referencedTypes);

        foreach (var method in sourceType.GetMethods(flags))
            AddMethod(method, referencedTypes);

        referencedTypes.Remove(sourceType);
        return referencedTypes;
    }

    private static void AddMethod(
        MethodBase method,
        ISet<Type> referencedTypes)
    {
        AddType(method.DeclaringType, referencedTypes);
        if (method is MethodInfo methodInfo)
        {
            AddType(methodInfo.ReturnType, referencedTypes);
            AddCustomAttributes(methodInfo.ReturnParameter.CustomAttributes, referencedTypes);
            AddGenericParameterConstraints(
                methodInfo.GetGenericArguments(),
                referencedTypes);
        }

        foreach (var parameter in method.GetParameters())
            AddParameter(parameter, referencedTypes);

        AddCustomAttributes(method.CustomAttributes, referencedTypes);

        var methodBody = method.GetMethodBody();
        if (methodBody is null)
            return;

        foreach (var localVariable in methodBody.LocalVariables)
            AddType(localVariable.LocalType, referencedTypes);
        foreach (var exceptionClause in methodBody.ExceptionHandlingClauses)
        {
            if (exceptionClause.Flags == ExceptionHandlingClauseOptions.Clause)
                AddType(exceptionClause.CatchType, referencedTypes);
        }

        AddIlReferences(method, methodBody, referencedTypes);
    }

    private static void AddIlReferences(
        MethodBase method,
        MethodBody methodBody,
        ISet<Type> referencedTypes)
    {
        var il = methodBody.GetILAsByteArray();
        if (il is null)
            return;

        var declaringTypeArguments = method.DeclaringType?.IsGenericType == true
            ? method.DeclaringType.GetGenericArguments()
            : null;
        var methodTypeArguments = method.IsGenericMethod
            ? method.GetGenericArguments()
            : null;

        for (var offset = 0; offset < il.Length;)
        {
            var instructionOffset = offset;
            var firstByte = il[offset++];
            var opCodeValue = firstByte == 0xfe
                ? unchecked((short)(0xfe00 | il[offset++]))
                : (short)firstByte;

            if (!OpCodesByValue.TryGetValue(opCodeValue, out var opCode))
                throw new InvalidOperationException(
                    $"Unknown IL opcode 0x{opCodeValue:x4} in {method.DeclaringType?.FullName}.{method.Name}.");

            if (opCode.OperandType == OperandType.InlineSig)
            {
                throw new InvalidOperationException(
                    $"Inline signatures are not supported by the architecture scanner: "
                    + $"{method.DeclaringType?.FullName}.{method.Name} at IL_{instructionOffset:x4}. "
                    + "Add signature decoding before allowing this instruction.");
            }

            if (opCode.OperandType is OperandType.InlineField
                or OperandType.InlineMethod
                or OperandType.InlineTok
                or OperandType.InlineType)
            {
                var metadataToken = BitConverter.ToInt32(il, offset);
                AddResolvedMember(
                    method,
                    instructionOffset,
                    metadataToken,
                    declaringTypeArguments,
                    methodTypeArguments,
                    referencedTypes);
            }

            offset += GetOperandSize(opCode.OperandType, il, offset);
        }
    }

    private static void AddResolvedMember(
        MethodBase sourceMethod,
        int instructionOffset,
        int metadataToken,
        Type[]? declaringTypeArguments,
        Type[]? methodTypeArguments,
        ISet<Type> referencedTypes)
    {
        try
        {
            var member = sourceMethod.Module.ResolveMember(
                metadataToken,
                declaringTypeArguments,
                methodTypeArguments);
            switch (member)
            {
                case Type type:
                    AddType(type, referencedTypes);
                    break;
                case FieldInfo field:
                    AddType(field.DeclaringType, referencedTypes);
                    AddType(field.FieldType, referencedTypes);
                    break;
                case MethodBase method:
                    AddType(method.DeclaringType, referencedTypes);
                    if (method is MethodInfo methodInfo)
                        AddType(methodInfo.ReturnType, referencedTypes);
                    foreach (var parameter in method.GetParameters())
                        AddType(parameter.ParameterType, referencedTypes);
                    if (method.IsGenericMethod)
                    {
                        foreach (var genericArgument in method.GetGenericArguments())
                            AddType(genericArgument, referencedTypes);
                    }
                    break;
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or BadImageFormatException
                or FileNotFoundException
                or MissingMethodException
                or NotSupportedException)
        {
            throw new InvalidOperationException(
                $"Unable to resolve metadata token 0x{metadataToken:x8} in "
                + $"{sourceMethod.DeclaringType?.FullName}.{sourceMethod.Name} "
                + $"at IL_{instructionOffset:x4}.",
                exception);
        }
    }

    private static int GetOperandSize(
        OperandType operandType,
        byte[] il,
        int operandOffset)
    {
        return operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget
                or OperandType.ShortInlineI
                or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget
                or OperandType.InlineField
                or OperandType.InlineI
                or OperandType.InlineMethod
                or OperandType.InlineSig
                or OperandType.InlineString
                or OperandType.InlineTok
                or OperandType.InlineType
                or OperandType.ShortInlineR => 4,
            OperandType.InlineI8
                or OperandType.InlineR => 8,
            OperandType.InlineSwitch =>
                sizeof(int)
                + (BitConverter.ToInt32(il, operandOffset) * sizeof(int)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(operandType),
                operandType,
                "Unsupported IL operand type."),
        };
    }

    private static void AddParameter(
        ParameterInfo parameter,
        ISet<Type> referencedTypes)
    {
        AddType(parameter.ParameterType, referencedTypes);
        AddCustomAttributes(parameter.CustomAttributes, referencedTypes);
    }

    private static void AddGenericParameterConstraints(
        IEnumerable<Type> genericArguments,
        ISet<Type> referencedTypes)
    {
        foreach (var genericArgument in genericArguments
                     .Where(type => type.IsGenericParameter))
        {
            foreach (var constraint in genericArgument.GetGenericParameterConstraints())
                AddType(constraint, referencedTypes);
        }
    }

    private static void AddCustomAttributes(
        IEnumerable<CustomAttributeData> attributes,
        ISet<Type> referencedTypes)
    {
        foreach (var attribute in attributes)
        {
            AddType(attribute.AttributeType, referencedTypes);
            AddType(attribute.Constructor.DeclaringType, referencedTypes);
            foreach (var argument in attribute.ConstructorArguments)
                AddAttributeArgument(argument, referencedTypes);
            foreach (var argument in attribute.NamedArguments)
                AddAttributeArgument(argument.TypedValue, referencedTypes);
        }
    }

    private static void AddAttributeArgument(
        CustomAttributeTypedArgument argument,
        ISet<Type> referencedTypes)
    {
        AddType(argument.ArgumentType, referencedTypes);
        if (argument.Value is Type type)
        {
            AddType(type, referencedTypes);
            return;
        }

        if (argument.Value is IEnumerable<CustomAttributeTypedArgument> values)
        {
            foreach (var value in values)
                AddAttributeArgument(value, referencedTypes);
        }
    }

    private static void AddType(Type? type, ISet<Type> referencedTypes)
    {
        if (type is null || !referencedTypes.Add(type))
            return;

        if (type.HasElementType)
            AddType(type.GetElementType(), referencedTypes);

        if (type.IsGenericType)
        {
            AddType(type.GetGenericTypeDefinition(), referencedTypes);
            foreach (var genericArgument in type.GetGenericArguments())
                AddType(genericArgument, referencedTypes);
        }

        if (type.IsGenericParameter)
        {
            foreach (var constraint in type.GetGenericParameterConstraints())
                AddType(constraint, referencedTypes);
        }

        AddType(type.DeclaringType, referencedTypes);
    }

    private static bool IsInNamespace(Type type, string namespacePrefix)
    {
        return type.Namespace is not null
               && IsNamespaceOrChild(type.Namespace, namespacePrefix);
    }

    private static string GetArchitectureOwnerName(Type type)
    {
        while (type.DeclaringType is not null)
            type = type.DeclaringType;

        return type.FullName ?? type.Name;
    }

    private static bool IsNamespaceOrChild(
        string typeNamespace,
        string namespacePrefix)
    {
        return typeNamespace.Equals(namespacePrefix, StringComparison.Ordinal)
               || typeNamespace.StartsWith(
                   namespacePrefix + ".",
                   StringComparison.Ordinal);
    }

    private sealed record DependencyViolation(
        string SourceType,
        string ReferencedType);

    private sealed class DependencyScannerProbe
    {
        public Dictionary<string, GenericDependencyProbe> GenericDependencies { get; } = [];

        public object CreateMethodBodyDependency()
        {
            return new MethodBodyDependencyProbe();
        }
    }

    private sealed class GenericDependencyProbe;

    private sealed class MethodBodyDependencyProbe;
}
