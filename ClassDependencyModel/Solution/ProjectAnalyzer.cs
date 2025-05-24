using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DependencyAnalyzer.DataStructure;
using System.Text;
using System.Xml.Linq;

namespace DependencyAnalyzer.SolutionAna
{
    public class ProjectAnalyzer
    {
        public async Task AnalyzeAsync(Project project,
            Dictionary<string, ClassDependency> classMap,
            Dictionary<string, HashSet<string>> dependencyNamesMap)
        {
            Console.WriteLine($"Analyzing project: {project.Name}");

            var compilation = await project.GetCompilationAsync();
            if (compilation == null)
            {
                Console.WriteLine($"  Warning: Could not compile {project.Name}");
                return;
            }

            var errors = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToArray();

            if (errors.Any())
            {
                Console.WriteLine($"  Warning: {errors.Length} compilation errors in {project.Name}");
                foreach (var error in errors.Take(3))
                {
                    Console.WriteLine($"    {error.GetMessage()}");
                }
                if (errors.Length > 3)
                {
                    Console.WriteLine($"    ... and {errors.Length - 3} more errors");
                }
            }

            int classCount = 0;
            foreach (var tree in compilation.SyntaxTrees)
            {
                classCount += await AnalyzeSyntaxTree(tree, compilation, classMap, dependencyNamesMap);
            }

            Console.WriteLine($"  Found {classCount} classes in {project.Name}");
        }

        private async Task<int> AnalyzeSyntaxTree(
            SyntaxTree tree,
            Compilation compilation,
            Dictionary<string, ClassDependency> classMap,
            Dictionary<string, HashSet<string>> dependencyNamesMap)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = await tree.GetRootAsync();
            var typeDeclarations = root.DescendantNodes()
                .Where(node => node is ClassDeclarationSyntax || node is InterfaceDeclarationSyntax);

            int count = 0;
            foreach (var decl in typeDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(decl);
                if (symbol is not INamedTypeSymbol typeSymbol)
                    continue;

                string typeName = typeSymbol.ToString();

                if (!classMap.ContainsKey(typeName))
                {
                    classMap[typeName] = new ClassDependency
                    {
                        ClassName = typeName,
                        SymbolType = GetSymbolType(typeSymbol)
                    };
                }

                var classDep = classMap[typeName];
                var dependencies = ExtractDependencies(typeSymbol, semanticModel);
                dependencyNamesMap[typeName] = dependencies;

                ExtractMembers(typeSymbol, classDep);

                if (decl is BaseTypeDeclarationSyntax baseDecl)
                {
                    classDep.Sammary = ExtractSummaryComment(baseDecl);
                }

                count++;
            }

            return count;
        }

        private string ExtractSummaryComment(BaseTypeDeclarationSyntax decl)
        {
            var trivia = decl.GetLeadingTrivia();
            var docCommentTrivia = trivia
                .Select(t => t.GetStructure())
                .OfType<DocumentationCommentTriviaSyntax>()
                .FirstOrDefault();

            if (docCommentTrivia == null)
                return string.Empty;

            var xmlText = new StringBuilder();
            foreach (var node in docCommentTrivia.Content)
            {
                if (node is XmlElementSyntax element &&
                    element.StartTag.Name.LocalName.Text == "summary")
                {
                    xmlText.Append(string.Concat(element.Content.Select(c => c.ToString())));
                }
            }

            var summary = xmlText.ToString().Trim();
            return summary.Replace("///", "").Trim();
        }

        private void ExtractMembers(INamedTypeSymbol typeSymbol, ClassDependency classDep)
        {
            foreach (var member in typeSymbol.GetMembers())
            {
                if (member.DeclaredAccessibility != Accessibility.Public)
                    continue;

                switch (member)
                {
                    case IFieldSymbol field:
                        classDep.Fields.Add(field.Name);
                        break;
                    case IPropertySymbol prop:
                        classDep.Fields.Add(prop.Name);
                        break;
                    case IMethodSymbol method when !method.IsImplicitlyDeclared && method.MethodKind == MethodKind.Ordinary:
                        classDep.Methods.Add(method.Name);
                        break;
                }
            }
        }


        private SymbolType GetSymbolType(INamedTypeSymbol symbol)
        {
            if (symbol.TypeKind == TypeKind.Class)
            {
                if (symbol.IsAbstract && !symbol.IsSealed)
                {
                    return SymbolType.AbstractClass;
                }
                else if (symbol.AllInterfaces.Length > 0)
                {
                    return SymbolType.InterfaceImplementingClass;
                }
                else
                {
                    return SymbolType.Class;
                }
            }

            return symbol.TypeKind switch
            {
                TypeKind.Interface => SymbolType.Interface,
                TypeKind.Struct => SymbolType.Struct,
                TypeKind.Enum => SymbolType.Enum,
                TypeKind.Delegate => SymbolType.Delegate,
                _ => SymbolType.Unknown,
            };
        }


        private HashSet<string> ExtractDependencies(INamedTypeSymbol classSymbol, SemanticModel semanticModel)
        {
            var dependencies = new HashSet<string>();

            // 継承関係
            if (classSymbol.BaseType != null &&
                classSymbol.BaseType.Name != "Object" &&
                !classSymbol.BaseType.ToString().StartsWith("System."))
            {
                dependencies.Add(classSymbol.BaseType.ToString());
            }

            // インターフェース実装
            foreach (var interfaceType in classSymbol.Interfaces)
            {
                if (!interfaceType.ToString().StartsWith("System."))
                {
                    dependencies.Add(interfaceType.ToString());
                }
            }

            // メンバー（フィールド、プロパティ、メソッド）の詳細分析
            foreach (var member in classSymbol.GetMembers())
            {
                switch (member)
                {
                    case IFieldSymbol field:
                        AddTypeIfRelevant(dependencies, field.Type);
                        break;
                    case IPropertySymbol prop:
                        AddTypeIfRelevant(dependencies, prop.Type);
                        break;
                    case IMethodSymbol method when !method.IsImplicitlyDeclared:
                        // 戻り値の型を分析
                        AddTypeIfRelevant(dependencies, method.ReturnType);

                        // パラメータの型を分析（ここが重要！）
                        foreach (var param in method.Parameters)
                        {
                            AddTypeIfRelevant(dependencies, param.Type);
                        }
                        break;
                }
            }

            // コンストラクタやメソッド内の依存関係を詳細に分析
            AnalyzeMethodBodies(classSymbol, semanticModel, dependencies);

            return dependencies;
        }
        private void AnalyzeMethodBodies(
    INamedTypeSymbol classSymbol,
    SemanticModel semanticModel,
    HashSet<string> dependencies)
        {
            foreach (var syntaxRef in classSymbol.DeclaringSyntaxReferences)
            {
                try
                {
                    var node = syntaxRef.GetSyntax();
                    if (node is not ClassDeclarationSyntax classDecl)
                        continue;

                    // メソッド宣言の詳細分析
                    try
                    {
                        AnalyzeMethodDeclarations(classDecl, semanticModel, dependencies);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"メソッド宣言解析中の例外: {ex.Message}");
                    }

                    // 変数宣言の型情報解析
                    try
                    {
                        var variableDeclarations = classDecl.DescendantNodes()
                            .OfType<VariableDeclarationSyntax>();

                        foreach (var varDecl in variableDeclarations)
                        {
                            try
                            {
                                var typeInfo = semanticModel.GetTypeInfo(varDecl.Type);
                                if (typeInfo.Type != null)
                                {
                                    AddTypeIfRelevant(dependencies, typeInfo.Type);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"変数宣言の解析中に例外発生: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"変数宣言の全体解析中に例外発生: {ex.Message}");
                    }

                    // オブジェクト生成の解析
                    try
                    {
                        AnalyzeObjectCreation(classDecl, semanticModel, dependencies);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"オブジェクト生成の解析中に例外発生: {ex.Message}");
                    }

                    // メソッド呼び出しの解析
                    try
                    {
                        AnalyzeMethodInvocations(classDecl, semanticModel, dependencies);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"メソッド呼び出しの解析中に例外発生: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"クラス構文取得中に例外発生: {ex.Message}");
                }
            }
        }


        // 新しく追加：メソッド宣言の詳細分析
        private void AnalyzeMethodDeclarations(
            ClassDeclarationSyntax classDecl,
            SemanticModel semanticModel,
            HashSet<string> dependencies)
        {
            var methodDeclarations = classDecl.DescendantNodes()
                .OfType<MethodDeclarationSyntax>();

            foreach (var methodDecl in methodDeclarations)
            {
                try
                {
                    // 戻り値の型を分析
                    if (methodDecl.ReturnType != null)
                    {
                        try
                        {
                            var returnTypeInfo = semanticModel.GetTypeInfo(methodDecl.ReturnType);
                            if (returnTypeInfo.Type != null)
                            {
                                AddTypeIfRelevant(dependencies, returnTypeInfo.Type);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"戻り値型の解析中に例外発生: {ex.Message}");
                        }
                    }

                    // パラメータの型を詳細分析
                    foreach (var parameter in methodDecl.ParameterList.Parameters)
                    {
                        try
                        {
                            if (parameter.Type != null)
                            {
                                var paramTypeInfo = semanticModel.GetTypeInfo(parameter.Type);
                                if (paramTypeInfo.Type != null)
                                {
                                    AddTypeIfRelevant(dependencies, paramTypeInfo.Type);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"パラメータ型の解析中に例外発生: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"メソッド宣言の解析中に予期しない例外が発生しました: {ex.Message}");
                }
            }
        }


        private void AnalyzeObjectCreation(
            ClassDeclarationSyntax classDecl,
            SemanticModel semanticModel,
            HashSet<string> dependencies)
        {
            var objectCreations = classDecl.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>();

            foreach (var creation in objectCreations)
            {
                // 実装クラスではなく、変数宣言の型を優先
                var parent = creation.Parent;
                bool foundDeclarationType = false;

                // 変数宣言の場合: Dictionary<string, ClassDependency> classMap = new Dictionary<string, ClassDependency>();
                if (parent is EqualsValueClauseSyntax equalsValue &&
                    equalsValue.Parent is VariableDeclaratorSyntax declarator &&
                    declarator.Parent is VariableDeclarationSyntax declaration)
                {
                    var declaredTypeInfo = semanticModel.GetTypeInfo(declaration.Type);
                    if (declaredTypeInfo.Type != null)
                    {
                        AddTypeIfRelevant(dependencies, declaredTypeInfo.Type);
                        foundDeclarationType = true;
                    }
                }

                // 代入の場合: service = new ServiceImpl();
                if (!foundDeclarationType && parent is AssignmentExpressionSyntax assignment)
                {
                    var leftTypeInfo = semanticModel.GetTypeInfo(assignment.Left);
                    if (leftTypeInfo.Type != null)
                    {
                        AddTypeIfRelevant(dependencies, leftTypeInfo.Type);
                        foundDeclarationType = true;
                    }
                }

                // メソッド引数の場合: Method(new ServiceImpl());
                if (!foundDeclarationType && parent is ArgumentSyntax argument &&
                    argument.Parent is ArgumentListSyntax argumentList &&
                    argumentList.Parent is InvocationExpressionSyntax invocation)
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                    if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
                    {
                        var argIndex = argumentList.Arguments.IndexOf(argument);
                        if (argIndex >= 0 && argIndex < methodSymbol.Parameters.Length)
                        {
                            var paramType = methodSymbol.Parameters[argIndex].Type;
                            AddTypeIfRelevant(dependencies, paramType);
                            foundDeclarationType = true;
                        }
                    }
                }

                // 上記に該当しない場合のみ実装型を追加
                if (!foundDeclarationType)
                {
                    var typeInfo = semanticModel.GetTypeInfo(creation);
                    if (typeInfo.Type != null)
                    {
                        AddTypeIfRelevant(dependencies, typeInfo.Type);
                    }
                }
            }
        }

        private void AnalyzeMethodInvocations(
            ClassDeclarationSyntax classDecl,
            SemanticModel semanticModel,
            HashSet<string> dependencies)
        {
            var invocations = classDecl.DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                var methodSymbol = symbolInfo.Symbol as IMethodSymbol;

                if (methodSymbol != null)
                {
                    // 静的メソッド呼び出しの場合
                    if (methodSymbol.IsStatic &&
                        !methodSymbol.ContainingType.ContainingNamespace.ToString().StartsWith("System"))
                    {
                        dependencies.Add(methodSymbol.ContainingType.ToString());
                    }
                    // インスタンスメソッド呼び出しの場合は、呼び出し元オブジェクトの型を取得
                    else if (!methodSymbol.IsStatic)
                    {
                        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                        {
                            var objectTypeInfo = semanticModel.GetTypeInfo(memberAccess.Expression);
                            if (objectTypeInfo.Type != null)
                            {
                                AddTypeIfRelevant(dependencies, objectTypeInfo.Type);
                            }
                        }
                    }
                }
            }
        }

        private void AddTypeIfRelevant(HashSet<string> dependencies, ITypeSymbol type)
        {
            if (type == null) return;

            var typeName = type.ToString();

            // システム型、プリミティブ型を除外（ただし、一部の重要な型は含める）
            if (IsBuiltInType(typeName) ||
                type.SpecialType != SpecialType.None)
                return;

            // System名前空間の基本型を除外（但し、重要なコレクション型などは含める可能性がある）
            if (typeName.StartsWith("System.") &&
                !typeName.StartsWith("System.Collections.Generic.Dictionary") &&
                !typeName.StartsWith("System.Collections.Generic.List"))
            {
                return;
            }

            // 配列の場合、要素型を分析
            if (type is IArrayTypeSymbol arrayType)
            {
                AddTypeIfRelevant(dependencies, arrayType.ElementType);
                return;
            }

            // ジェネリック型の処理
            if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                // Dictionary<string, ClassDependency> -> ClassDependency を抽出
                foreach (var typeArg in namedType.TypeArguments)
                {
                    AddTypeIfRelevant(dependencies, typeArg);
                }

                // ジェネリック型自体も追加する場合（必要に応じて）
                if (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Interface)
                {
                    if (!typeName.StartsWith("System.Collections.Generic.Dictionary") &&
                        !typeName.StartsWith("System.Collections.Generic.List"))
                    {
                        dependencies.Add(typeName);
                    }
                }
            }
            else if (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Interface)
            {
                dependencies.Add(typeName);
            }
        }

        private bool IsBuiltInType(string typeName)
        {
            var builtInTypes = new HashSet<string>
            {
                "string", "int", "bool", "double", "float", "decimal",
                "long", "short", "byte", "char", "object", "void"
            };
            return builtInTypes.Contains(typeName);
        }

    }
}