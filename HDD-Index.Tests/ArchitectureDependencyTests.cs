using System.Reflection;
using System.Reflection.Emit;
using HDD_Index.ViewModels;

namespace HDD_Index.Tests;

/// <summary>
/// 守护 HDD-Index 各层之间的依赖方向。
/// 测试同时检查类型元数据和方法体 IL，避免只检查公开 API 而漏掉真实实现依赖。
/// </summary>
public class ArchitectureDependencyTests
{
    // 以下常量是项目认可的架构层。子命名空间自动归属于对应父层。
    private const string RootNamespace = "HDD_Index";
    private const string ModelsNamespace = RootNamespace + ".Models";
    private const string ApplicationNamespace = RootNamespace + ".Application";
    private const string DeclarationsNamespace = ApplicationNamespace + ".Declarations";
    private const string RepositoriesNamespace = ApplicationNamespace + ".Repositories";
    private const string ExternalInteractionsNamespace =
        ApplicationNamespace + ".ExternalInteractions";
    private const string ServicesNamespace = RootNamespace + ".Services";
    private const string AdaptersNamespace = RootNamespace + ".Adapters";
    private const string MessagesNamespace = RootNamespace + ".Messages";
    private const string ViewModelsNamespace = RootNamespace + ".ViewModels";
    private const string ViewsNamespace = RootNamespace + ".Views";

    // 通过应用中的稳定类型取得被测程序集，避免依赖输出文件路径。
    private static readonly Assembly ApplicationAssembly =
        typeof(Services.DeclarationSyncService).Assembly;

    // 所有业务与展示层命名空间，用于检查新类型是否被明确归类。
    private static readonly string[] LayerNamespaces =
    [
        ModelsNamespace,
        ApplicationNamespace,
        ServicesNamespace,
        AdaptersNamespace,
        MessagesNamespace,
        ViewModelsNamespace,
        ViewsNamespace,
    ];

    // 核心层和消息契约不应引用这些 UI/MVVM 框架。
    private static readonly string[] UiFrameworkNamespaces =
    [
        "Avalonia",
        "CommunityToolkit.Mvvm",
        "DynamicData",
        "ReactiveUI",
        "Xaml.Behaviors",
    ];

    // 建立 IL 操作码查找表，用来逐条解析方法体中的类型、字段和方法引用。
    private static readonly IReadOnlyDictionary<short, OpCode> OpCodesByValue =
        typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(opCode => opCode.Value);

    // Models 是最底层领域模型，不能反向依赖任何上层。
    [Fact]
    public void Models_DoNotDependOnHigherLayers()
    {
        AssertNoDependencies(
            [ModelsNamespace],
            [
                ApplicationNamespace,
                ServicesNamespace,
                AdaptersNamespace,
                MessagesNamespace,
                ViewModelsNamespace,
                ViewsNamespace,
            ]);
    }

    // Model 可以使用 Path 处理领域中的扩展名，但不能直接访问本地文件系统。
    [Fact]
    public void Models_DoNotAccessTheLocalFileSystem()
    {
        var forbiddenTypes = new HashSet<Type>
        {
            typeof(Directory),
            typeof(DirectoryInfo),
            typeof(DriveInfo),
            typeof(File),
            typeof(FileInfo),
            typeof(FileStream),
            typeof(FileSystemInfo),
            typeof(FileSystemWatcher),
        };
        var violations = FindDependencyViolations(
            [ModelsNamespace],
            forbiddenTypes.Contains);

        AssertNoViolations(violations);
    }

    // JSON 序列化属于持久化实现；Model 只保留兼容现有格式所需的声明性特性。
    [Fact]
    public void Models_DoNotUseJsonSerializer()
    {
        var forbiddenTypes = new HashSet<Type>
        {
            typeof(System.Text.Json.JsonSerializer),
            typeof(System.Text.Json.JsonSerializerOptions),
        };
        var violations = FindDependencyViolations(
            [ModelsNamespace],
            forbiddenTypes.Contains);

        AssertNoViolations(violations);
    }

    // Application 负责编排领域操作，可以依赖 Models，但不能依赖更高层。
    [Fact]
    public void ApplicationLayer_DoesNotDependOnHigherLayers()
    {
        AssertNoDependencies(
            [ApplicationNamespace],
            [
                ServicesNamespace,
                AdaptersNamespace,
                MessagesNamespace,
                ViewModelsNamespace,
                ViewsNamespace,
            ]);
    }

    // 声明用例返回计划和验证结果，不能直接调用消息、确认或领域对话端口。
    [Fact]
    public void DeclarationUseCases_DoNotDependOnExternalInteractions()
    {
        AssertNoDependencies(
            [DeclarationsNamespace],
            [ExternalInteractionsNamespace]);
    }

    // Repository 用例返回计划和业务结果，不能直接调用重命名、删除确认或消息端口。
    [Fact]
    public void RepositoryUseCases_DoNotDependOnExternalInteractions()
    {
        AssertNoDependencies(
            [RepositoriesNamespace],
            [ExternalInteractionsNamespace]);
    }

    // Services 可以使用 Models 和 Application，但不能知道展示层的存在。
    [Fact]
    public void Services_DoNotDependOnPresentationLayers()
    {
        AssertNoDependencies(
            [ServicesNamespace],
            [
                MessagesNamespace,
                AdaptersNamespace,
                ViewModelsNamespace,
                ViewsNamespace,
            ]);
    }

    // Views 可以绑定 ViewModels、Models 和 Messages，但不能越过 ViewModels 调用业务层。
    [Fact]
    public void Views_DoNotDependOnApplicationOrServices()
    {
        AssertNoDependencies(
            [ViewsNamespace],
            [
                ApplicationNamespace,
                ServicesNamespace,
                AdaptersNamespace,
            ]);
    }

    // ViewModels 只依赖 UI 无关端口，不能创建具体 Views 或依赖外部适配器。
    [Fact]
    public void ViewModels_DoNotDependOnViewsOrAdapters()
    {
        AssertNoDependencies(
            [ViewModelsNamespace],
            [ViewsNamespace, AdaptersNamespace]);
    }

    // ViewModels 不得重新通过全局应用对象查找窗口，也不能直接启动平台进程。
    [Fact]
    public void ViewModels_DoNotUseGlobalApplicationOrProcesses()
    {
        var forbiddenTypes = new HashSet<Type>
        {
            typeof(Avalonia.Application),
            typeof(System.Diagnostics.Process),
            typeof(System.Diagnostics.ProcessStartInfo),
        };
        var violations = FindDependencyViolations(
            [ViewModelsNamespace],
            forbiddenTypes.Contains);

        AssertNoViolations(violations);
    }

    // Messages 是独立的展示层契约，应保持轻量，不能依赖其他项目层。
    [Fact]
    public void Messages_DoNotDependOnOtherApplicationLayers()
    {
        AssertNoDependencies(
            [MessagesNamespace],
            [
                ModelsNamespace,
                ApplicationNamespace,
                ServicesNamespace,
                AdaptersNamespace,
                ViewModelsNamespace,
                ViewsNamespace,
            ]);
    }

    // 核心层和消息契约应保持 UI 无关，便于独立测试和后续复用。
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

    // HDD_Index 根命名空间是组合根，只允许它组装各层，其他层不能反向引用它。
    [Fact]
    public void ApplicationLayers_DoNotDependOnTheCompositionRoot()
    {
        var violations = FindDependencyViolations(
            LayerNamespaces,
            referencedType => referencedType.Namespace == RootNamespace);

        AssertNoViolations(violations);
    }

    // 根命名空间只保留启动和界面装配类型，避免业务类型放在根目录绕过分层检查。
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

    // 新增项目命名空间时必须先决定其架构归属，不能默认落在规则之外。
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

    // 名称以 ViewModel 或 VM 结尾的类必须使用统一基类，保持通知机制一致。
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

    // 对扫描器本身做防回归验证：既要展开嵌套泛型，也要读取方法体中的 newobj。
    [Fact]
    public void DependencyScanner_InspectsNestedGenericsAndMethodBodies()
    {
        var referencedTypes = GetReferencedTypes(typeof(DependencyScannerProbe));

        Assert.Contains(typeof(GenericDependencyProbe), referencedTypes);
        Assert.Contains(typeof(MethodBodyDependencyProbe), referencedTypes);
    }

    // 通用的“源命名空间不得依赖目标命名空间”断言入口。
    private static void AssertNoDependencies(
        IReadOnlyCollection<string> sourceNamespaces,
        IReadOnlyCollection<string> forbiddenNamespacePrefixes)
    {
        var violations = FindDependencyViolations(
            sourceNamespaces,
            forbiddenNamespacePrefixes);

        AssertNoViolations(violations);
    }

    // 集中输出完整违规列表，避免 Assert.DoesNotContain 只能显示首个问题。
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

    // 将禁止的命名空间前缀转换为统一的类型过滤条件。
    private static IReadOnlyList<DependencyViolation> FindDependencyViolations(
        IReadOnlyCollection<string> sourceNamespaces,
        IReadOnlyCollection<string> forbiddenNamespacePrefixes)
    {
        return FindDependencyViolations(
            sourceNamespaces,
            referencedType => forbiddenNamespacePrefixes.Any(
                prefix => IsInNamespace(referencedType, prefix)));
    }

    // 扫描所有源类型，返回“架构归属类型 -> 被引用类型”的去重结果。
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
        // 同时检查反射元数据和 IL。仅检查方法签名会漏掉只在方法体中创建或调用的类型。
        // 这是二进制依赖检查：未使用的 using、被内联的常量、nameof/字符串反射，
        // 以及只存在于源 XAML 中的引用不会产生 IL 类型令牌，仍由编译器负责检查。
        var referencedTypes = new HashSet<Type>();

        // 类型级依赖：基类、接口、泛型约束和特性。
        AddType(sourceType.BaseType, referencedTypes);
        foreach (var interfaceType in sourceType.GetInterfaces())
            AddType(interfaceType, referencedTypes);
        AddGenericParameterConstraints(sourceType.GetGenericArguments(), referencedTypes);
        AddCustomAttributes(sourceType.CustomAttributes, referencedTypes);

        // 同时检查公开与私有成员；DeclaredOnly 避免把继承成员重复归到当前类型。
        const BindingFlags flags = BindingFlags.Instance
                                   | BindingFlags.Static
                                   | BindingFlags.Public
                                   | BindingFlags.NonPublic
                                   | BindingFlags.DeclaredOnly;

        // 成员元数据依赖：字段、属性（含索引器）、事件及其特性。
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

        // 方法级依赖：构造函数、静态构造函数和普通方法。
        foreach (var constructor in sourceType.GetConstructors(flags))
            AddMethod(constructor, referencedTypes);

        if (sourceType.TypeInitializer is not null)
            AddMethod(sourceType.TypeInitializer, referencedTypes);

        foreach (var method in sourceType.GetMethods(flags))
            AddMethod(method, referencedTypes);

        referencedTypes.Remove(sourceType);
        return referencedTypes;
    }

    // 收集方法签名、泛型约束、特性、局部变量、异常类型和方法体 IL 中的依赖。
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

    // 顺序解析方法体 IL，并解析携带元数据令牌的指令操作数。
    private static void AddIlReferences(
        MethodBase method,
        MethodBody methodBody,
        ISet<Type> referencedTypes)
    {
        var il = methodBody.GetILAsByteArray();
        if (il is null)
            return;

        // 解析泛型方法或泛型声明类型中的令牌时，需要把当前泛型上下文传给反射 API。
        var declaringTypeArguments = method.DeclaringType?.IsGenericType == true
            ? method.DeclaringType.GetGenericArguments()
            : null;
        var methodTypeArguments = method.IsGenericMethod
            ? method.GetGenericArguments()
            : null;

        for (var offset = 0; offset < il.Length;)
        {
            var instructionOffset = offset;

            // IL 操作码可能占一个字节，也可能以 0xfe 开头占两个字节。
            var firstByte = il[offset++];
            var opCodeValue = firstByte == 0xfe
                ? unchecked((short)(0xfe00 | il[offset++]))
                : (short)firstByte;

            if (!OpCodesByValue.TryGetValue(opCodeValue, out var opCode))
                throw new InvalidOperationException(
                    $"Unknown IL opcode 0x{opCodeValue:x4} in {method.DeclaringType?.FullName}.{method.Name}.");

            // InlineSig（例如 calli/函数指针）需要单独解析签名。
            // 当前选择明确失败，不能静默跳过而让架构检查产生假阴性。
            if (opCode.OperandType == OperandType.InlineSig)
            {
                throw new InvalidOperationException(
                    $"Inline signatures are not supported by the architecture scanner: "
                    + $"{method.DeclaringType?.FullName}.{method.Name} at IL_{instructionOffset:x4}. "
                    + "Add signature decoding before allowing this instruction.");
            }

            // 只有以下四类操作数直接携带可解析的类型、字段或方法元数据令牌。
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

            // 跳过当前指令的操作数，继续读取下一条操作码。
            offset += GetOperandSize(opCode.OperandType, il, offset);
        }
    }

    // 将 IL 元数据令牌还原为成员，并继续展开成员签名中的所有类型。
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

    // 根据操作数类型计算字节长度；switch 的长度由分支数量动态决定。
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

    // 参数依赖不仅包括参数类型，也包括施加在参数上的特性。
    private static void AddParameter(
        ParameterInfo parameter,
        ISet<Type> referencedTypes)
    {
        AddType(parameter.ParameterType, referencedTypes);
        AddCustomAttributes(parameter.CustomAttributes, referencedTypes);
    }

    // 泛型参数本身没有业务命名空间，真正需要检查的是它的类型约束。
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

    // 特性类型及特性参数也可能携带跨层类型引用，不能只扫描成员签名。
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

    // 特性参数可能是单个 Type，也可能是 Type 数组，因此需要递归展开。
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

    // 递归展开数组/指针/ByRef、泛型定义、全部泛型实参和嵌套声明类型。
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

    // 命名空间判断包含其子命名空间，但不会误把 ViewModelsLegacy 当作 ViewModels。
    private static bool IsInNamespace(Type type, string namespacePrefix)
    {
        return type.Namespace is not null
               && IsNamespaceOrChild(type.Namespace, namespacePrefix);
    }

    // 编译器会为 async、迭代器和 lambda 生成嵌套类型；违规应归到最外层业务类型。
    private static string GetArchitectureOwnerName(Type type)
    {
        while (type.DeclaringType is not null)
            type = type.DeclaringType;

        return type.FullName ?? type.Name;
    }

    // 使用“完全相等或以点号分隔的子命名空间”作为架构层匹配规则。
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

    // 以下探针仅用于验证依赖扫描器自身，不属于应用架构层。
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
