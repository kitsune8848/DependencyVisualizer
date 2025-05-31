using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DependencyAnalyzer.DataStructure;
using System.Text;
using System.Xml.Linq;

namespace DependencyAnalyzer.SolutionAna
{
    /// <summary>
    /// C#プロジェクト内のクラス間依存関係を高速解析するコアエンジン
    /// パフォーマンスを重視し、並列処理とメモリ効率化を実装した依存関係分析器
    /// </summary>
    public class ProjectAnalyzer
    {
        #region Public Methods

        /// <summary>
        /// 指定されたプロジェクトを包括的に解析し、全クラスの依存関係マップを構築する
        /// </summary>
        /// <param name="project">解析対象のRoslynプロジェクト</param>
        /// <param name="classMap">クラス名をキーとした依存関係オブジェクトのマップ（出力用）</param>
        /// <param name="dependencyNamesMap">クラス名をキーとした依存先クラス名セットのマップ（出力用）</param>
        /// <returns>非同期タスク</returns>
        public async Task AnalyzeAsync(Project project,
            Dictionary<string, ClassDependency> classMap,
            Dictionary<string, HashSet<string>> dependencyNamesMap)
        {
            Console.WriteLine($"Analyzing project: {project.Name}");

            // プロジェクトをコンパイルして、意味解析の基盤となるCompilationオブジェクトを取得
            var compilation = await project.GetCompilationAsync().ConfigureAwait(false);
            if (compilation == null)
            {
                Console.WriteLine($"  Warning: Could not compile {project.Name}");
                return;
            }

            // コンパイルエラーの検出と警告表示（解析継続のためエラーがあっても処理を続行）
            var errors = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToArray();

            if (errors.Any())
            {
                Console.WriteLine($"  Warning: {errors.Length} compilation errors in {project.Name}");
                // 主要なエラーのみ表示してログの肥大化を防ぐ
                foreach (var error in errors.Take(3))
                {
                    Console.WriteLine($"    {error.GetMessage()}");
                }
                if (errors.Length > 3)
                {
                    Console.WriteLine($"    ... and {errors.Length - 3} more errors");
                }
            }

            // 各構文ツリーを並列処理で解析し、マルチコア環境でのパフォーマンスを最大化
            int classCount = 0;
            var tasks = compilation.SyntaxTrees.Select(async tree =>
                await AnalyzeSyntaxTreeOptimized(tree, compilation, classMap, dependencyNamesMap));

            var results = await Task.WhenAll(tasks);
            classCount = results.Sum();

            Console.WriteLine($"  Found {classCount} classes in {project.Name}");
        }

        #endregion

        #region Private Methods - Optimized Analysis

        /// <summary>
        /// 単一の構文ツリーを効率的に解析し、含まれる全ての型宣言を処理する
        /// メモリ使用量を抑制しながら高速処理を実現
        /// </summary>
        /// <param name="tree">解析対象の構文ツリー</param>
        /// <param name="compilation">コンパイル情報</param>
        /// <param name="classMap">クラス依存関係マップ（出力用）</param>
        /// <param name="dependencyNamesMap">依存関係名前マップ（出力用）</param>
        /// <returns>処理されたクラス数</returns>
        private async Task<int> AnalyzeSyntaxTreeOptimized(
            SyntaxTree tree,
            Compilation compilation,
            Dictionary<string, ClassDependency> classMap,
            Dictionary<string, HashSet<string>> dependencyNamesMap)
        {
            // 意味モデルを取得してシンボル情報にアクセス可能にする
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = await tree.GetRootAsync().ConfigureAwait(false);

            // クラスとインターフェースの宣言を一括取得し、重複処理を避ける
            var typeDeclarations = root.DescendantNodes()
                .Where(node => node is ClassDeclarationSyntax || node is InterfaceDeclarationSyntax)
                .ToList(); // 一度だけ列挙してパフォーマンス向上

            int count = 0;
            foreach (var decl in typeDeclarations)
            {
                try
                {
                    // 各型宣言を個別に処理し、エラー発生時も他の型の処理を継続
                    if (ProcessTypeDeclarationOptimized(decl, semanticModel, classMap, dependencyNamesMap))
                    {
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    // 個別型のエラーは記録するが、全体の解析は継続
                    Console.WriteLine($"型宣言処理中の例外: {ex.Message}");
                }
            }

            return count;
        }

        /// <summary>
        /// 単一の型宣言（クラスまたはインターフェース）を包括的に解析する
        /// 継承関係、メンバー、メソッド内の依存関係を統合的に抽出
        /// </summary>
        /// <param name="decl">型宣言の構文ノード</param>
        /// <param name="semanticModel">意味解析モデル</param>
        /// <param name="classMap">クラス依存関係マップ</param>
        /// <param name="dependencyNamesMap">依存関係名前マップ</param>
        /// <returns>処理が成功した場合true</returns>
        private bool ProcessTypeDeclarationOptimized(
            SyntaxNode decl,
            SemanticModel semanticModel,
            Dictionary<string, ClassDependency> classMap,
            Dictionary<string, HashSet<string>> dependencyNamesMap)
        {
            // 構文ノードから型シンボルを取得
            var symbol = semanticModel.GetDeclaredSymbol(decl);
            if (symbol is not INamedTypeSymbol typeSymbol)
                return false;

            string typeName = typeSymbol.ToString();

            // ClassDependencyオブジェクトの初期化または既存オブジェクトの取得
            if (!classMap.ContainsKey(typeName))
            {
                classMap[typeName] = new ClassDependency
                {
                    ClassName = typeName,
                    SymbolType = GetSymbolType(typeSymbol)
                };
            }

            var classDep = classMap[typeName];

            // 依存関係の包括的抽出（継承、メンバー、メソッド内使用を統合）
            var dependencies = ExtractAllDependencies(typeSymbol, semanticModel, decl);
            dependencyNamesMap[typeName] = dependencies;

            // パブリックメンバー情報の抽出
            ExtractMembers(typeSymbol, classDep);

            // XMLドキュメントコメントからサマリーを抽出
            if (decl is BaseTypeDeclarationSyntax baseDecl)
            {
                classDep.summary = ExtractSummaryComment(baseDecl);
            }

            return true;
        }

        /// <summary>
        /// 指定された型の全ての依存関係を統合的に抽出する高速化アルゴリズム
        /// 継承、メンバー、メソッド内使用の3つの観点から依存関係を分析
        /// </summary>
        /// <param name="classSymbol">解析対象の型シンボル</param>
        /// <param name="semanticModel">意味解析モデル</param>
        /// <param name="decl">型宣言の構文ノード</param>
        /// <returns>依存先型名のセット</returns>
        private HashSet<string> ExtractAllDependencies(INamedTypeSymbol classSymbol, SemanticModel semanticModel, SyntaxNode decl)
        {
            var dependencies = new HashSet<string>();

            // 1. 継承とインターフェース実装による直接的な依存関係
            ExtractInheritanceDependencies(classSymbol, dependencies);

            // 2. フィールド、プロパティ、メソッドシグネチャによる依存関係
            ExtractMemberDependencies(classSymbol, dependencies);

            // 3. メソッド実装内での型使用による間接的な依存関係
            if (decl is ClassDeclarationSyntax classDecl)
            {
                AnalyzeMethodBodiesOptimized(classDecl, semanticModel, dependencies);
            }

            return dependencies;
        }

        /// <summary>
        /// メソッド本体内の型使用を効率的に解析する最適化エンジン
        /// var宣言、オブジェクト生成、メソッド呼び出しの3つのパターンを並列処理
        /// </summary>
        /// <param name="classDecl">クラス宣言構文</param>
        /// <param name="semanticModel">意味解析モデル</param>
        /// <param name="dependencies">依存関係セット（出力用）</param>
        private void AnalyzeMethodBodiesOptimized(
            ClassDeclarationSyntax classDecl,
            SemanticModel semanticModel,
            HashSet<string> dependencies)
        {
            try
            {
                // 全ての子ノードを一度に取得してメモリ効率とパフォーマンスを両立
                var syntaxNodes = classDecl.DescendantNodes().ToList();

                // 型推論を含むvar宣言の解析
                ProcessVarDeclarations(syntaxNodes, semanticModel, dependencies);

                // new演算子によるオブジェクト生成の解析
                ProcessObjectCreations(syntaxNodes, semanticModel, dependencies);

                // メソッド呼び出しによる型依存関係の解析
                ProcessMethodInvocations(syntaxNodes, semanticModel, dependencies);
            }
            catch (Exception ex)
            {
                // メソッド本体解析エラーは記録するが、他の解析は継続
                Console.WriteLine($"メソッド本体解析中の例外: {ex.Message}");
            }
        }

        /// <summary>
        /// var宣言における型推論を解析し、実際の型依存関係を抽出する
        /// 初期化式から実際の型を特定して依存関係に追加
        /// </summary>
        /// <param name="syntaxNodes">解析対象の構文ノード一覧</param>
        /// <param name="semanticModel">意味解析モデル</param>
        /// <param name="dependencies">依存関係セット</param>
        private void ProcessVarDeclarations(List<SyntaxNode> syntaxNodes, SemanticModel semanticModel, HashSet<string> dependencies)
        {
            // var宣言のみを効率的にフィルタリング
            var varDeclarations = syntaxNodes
                .OfType<VariableDeclarationSyntax>()
                .Where(vd => vd.Type.IsVar);

            foreach (var varDecl in varDeclarations)
            {
                foreach (var variable in varDecl.Variables)
                {
                    // 初期化式が存在する場合のみ処理
                    if (variable.Initializer?.Value == null) continue;

                    try
                    {
                        // 初期化式の型情報を取得して実際の型を特定
                        var initializerTypeInfo = semanticModel.GetTypeInfo(variable.Initializer.Value);
                        if (initializerTypeInfo.Type != null)
                        {
                            AddTypeIfRelevant(dependencies, initializerTypeInfo.Type);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"var宣言解析エラー: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// new演算子によるオブジェクト生成を解析し、型依存関係を抽出する
        /// 生成される型とコンテキスト情報の両方を考慮した包括的分析
        /// </summary>
        /// <param name="syntaxNodes">解析対象の構文ノード一覧</param>
        /// <param name="semanticModel">意味解析モデル</param>
        /// <param name="dependencies">依存関係セット</param>
        private void ProcessObjectCreations(List<SyntaxNode> syntaxNodes, SemanticModel semanticModel, HashSet<string> dependencies)
        {
            var objectCreations = syntaxNodes.OfType<ObjectCreationExpressionSyntax>();

            foreach (var creation in objectCreations)
            {
                try
                {
                    // 生成される型の情報を取得
                    var createdTypeInfo = semanticModel.GetTypeInfo(creation.Type);
                    if (createdTypeInfo.Type != null)
                    {
                        AddTypeIfRelevant(dependencies, createdTypeInfo.Type);
                    }

                    // オブジェクト生成のコンテキストを分析（代入先の型など）
                    AnalyzeCreationContext(creation, semanticModel, dependencies);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"オブジェクト生成解析エラー: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// メソッド呼び出しを解析し、静的メソッド呼び出しとインスタンスメソッド呼び出しを区別して処理
        /// 呼び出し先の型情報を抽出して依存関係に追加
        /// </summary>
        /// <param name="syntaxNodes">解析対象の構文ノード一覧</param>
        /// <param name="semanticModel">意味解析モデル</param>
        /// <param name="dependencies">依存関係セット</param>
        private void ProcessMethodInvocations(List<SyntaxNode> syntaxNodes, SemanticModel semanticModel, HashSet<string> dependencies)
        {
            var invocations = syntaxNodes.OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                try
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                    if (symbolInfo.Symbol is not IMethodSymbol methodSymbol) continue;

                    // 静的メソッド呼び出しの場合（Systemタイプは除外）
                    if (methodSymbol.IsStatic &&
                        !methodSymbol.ContainingType.ContainingNamespace.ToString().StartsWith("System"))
                    {
                        dependencies.Add(methodSymbol.ContainingType.ToString());
                    }
                    // インスタンスメソッド呼び出しの場合
                    else if (!methodSymbol.IsStatic &&
                             invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                    {
                        var objectTypeInfo = semanticModel.GetTypeInfo(memberAccess.Expression);
                        if (objectTypeInfo.Type != null)
                        {
                            AddTypeIfRelevant(dependencies, objectTypeInfo.Type);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"メソッド呼び出し解析エラー: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// オブジェクト生成のコンテキスト（代入先、宣言型など）を分析する軽量アルゴリズム
        /// 変数宣言時の型情報や代入式の左辺型を抽出
        /// </summary>
        /// <param name="creation">オブジェクト生成式</param>
        /// <param name="semanticModel">意味解析モデル</param>
        /// <param name="dependencies">依存関係セット</param>
        private void AnalyzeCreationContext(ObjectCreationExpressionSyntax creation, SemanticModel semanticModel, HashSet<string> dependencies)
        {
            var parent = creation.Parent;

            switch (parent)
            {
                // 変数宣言での初期化の場合
                case EqualsValueClauseSyntax equalsValue
                    when equalsValue.Parent is VariableDeclaratorSyntax declarator &&
                         declarator.Parent is VariableDeclarationSyntax declaration:

                    // varでない明示的型宣言の場合、宣言された型を依存関係に追加
                    if (!declaration.Type.IsVar)
                    {
                        var declaredTypeInfo = semanticModel.GetTypeInfo(declaration.Type);
                        if (declaredTypeInfo.Type != null)
                        {
                            AddTypeIfRelevant(dependencies, declaredTypeInfo.Type);
                        }
                    }
                    break;

                // 代入式の右辺の場合
                case AssignmentExpressionSyntax assignment:
                    var leftTypeInfo = semanticModel.GetTypeInfo(assignment.Left);
                    if (leftTypeInfo.Type != null)
                    {
                        AddTypeIfRelevant(dependencies, leftTypeInfo.Type);
                    }
                    break;
            }
        }

        #endregion

        #region Private Methods - Core Functionality

        /// <summary>
        /// 型シンボルから適切なSymbolTypeを判定する分類アルゴリズム
        /// クラス、インターフェース、構造体、列挙型、デリゲートを区別
        /// </summary>
        /// <param name="symbol">分類対象の型シンボル</param>
        /// <returns>対応するSymbolType</returns>
        private static SymbolType GetSymbolType(INamedTypeSymbol symbol)
        {
            if (symbol.TypeKind == TypeKind.Class)
            {
                // 抽象クラス、インターフェース実装クラス、通常クラスの判定
                if (symbol.IsAbstract && !symbol.IsSealed)
                    return SymbolType.AbstractClass;
                else if (symbol.AllInterfaces.Length > 0)
                    return SymbolType.InterfaceImplementingClass;
                else
                    return SymbolType.Class;
            }

            // その他の型種別の判定
            return symbol.TypeKind switch
            {
                TypeKind.Interface => SymbolType.Interface,
                TypeKind.Struct => SymbolType.Struct,
                TypeKind.Enum => SymbolType.Enum,
                TypeKind.Delegate => SymbolType.Delegate,
                _ => SymbolType.Unknown,
            };
        }

        /// <summary>
        /// 型のパブリックメンバー（フィールド、プロパティ、メソッド）を抽出する
        /// APIの公開インターフェースを特定して依存関係解析に活用
        /// </summary>
        /// <param name="typeSymbol">解析対象の型シンボル</param>
        /// <param name="classDep">メンバー情報を格納するClassDependencyオブジェクト</param>
        private static void ExtractMembers(INamedTypeSymbol typeSymbol, ClassDependency classDep)
        {
            foreach (var member in typeSymbol.GetMembers())
            {
                // パブリックメンバーのみを対象とする
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

        /// <summary>
        /// 継承関係とインターフェース実装による直接的な依存関係を抽出する
        /// System名前空間の型は除外してユーザー定義型のみを対象とする
        /// </summary>
        /// <param name="classSymbol">解析対象のクラスシンボル</param>
        /// <param name="dependencies">依存関係セット</param>
        private static void ExtractInheritanceDependencies(INamedTypeSymbol classSymbol, HashSet<string> dependencies)
        {
            // 基底クラスの処理（Object型とSystem型は除外）
            if (classSymbol.BaseType != null &&
                classSymbol.BaseType.Name != "Object" &&
                !classSymbol.BaseType.ToString().StartsWith("System."))
            {
                dependencies.Add(classSymbol.BaseType.ToString());
            }

            // 実装インターフェースの処理（System型は除外）
            foreach (var interfaceType in classSymbol.Interfaces)
            {
                if (!interfaceType.ToString().StartsWith("System."))
                {
                    dependencies.Add(interfaceType.ToString());
                }
            }
        }

        /// <summary>
        /// メンバー（フィールド、プロパティ、メソッド）の型シグネチャから依存関係を抽出する
        /// 戻り値型、パラメータ型、フィールド型を包括的に分析
        /// </summary>
        /// <param name="classSymbol">解析対象のクラスシンボル</param>
        /// <param name="dependencies">依存関係セット</param>
        private void ExtractMemberDependencies(INamedTypeSymbol classSymbol, HashSet<string> dependencies)
        {
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
                        // メソッドの戻り値型を追加
                        AddTypeIfRelevant(dependencies, method.ReturnType);
                        // パラメータ型を追加
                        foreach (var param in method.Parameters)
                        {
                            AddTypeIfRelevant(dependencies, param.Type);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 型が依存関係として記録すべきかを判定し、適切な場合に追加する核心ロジック
        /// ビルトイン型、System型、ジェネリック型を適切に処理
        /// </summary>
        /// <param name="dependencies">依存関係セット</param>
        /// <param name="type">評価対象の型</param>
        private void AddTypeIfRelevant(HashSet<string> dependencies, ITypeSymbol type)
        {
            if (type == null) return;

            var typeName = type.ToString();

            // ビルトイン型とSpecialType（primitiveタイプ）は除外
            if (IsBuiltInType(typeName) || type.SpecialType != SpecialType.None)
                return;

            // 一般的なSystem型は除外（一部の汎用コレクションは含める）
            if (IsSystemTypeToExclude(typeName))
                return;

            // 型の種類に応じた処理
            ProcessTypeByKind(dependencies, type, typeName);
        }

        /// <summary>
        /// System名前空間の型のうち、依存関係から除外すべき型を判定する
        /// Dictionary、Listなどの汎用コレクションは依存関係として重要なため含める
        /// </summary>
        /// <param name="typeName">判定対象の型名</param>
        /// <returns>除外すべき場合true</returns>
        private static bool IsSystemTypeToExclude(string typeName)
        {
            return typeName.StartsWith("System.") &&
                   !typeName.StartsWith("System.Collections.Generic.Dictionary") &&
                   !typeName.StartsWith("System.Collections.Generic.List");
        }

        /// <summary>
        /// 型の種類（配列、ジェネリック、通常の型）に応じて適切な依存関係抽出を実行
        /// 配列の要素型、ジェネリック型引数を再帰的に処理
        /// </summary>
        /// <param name="dependencies">依存関係セット</param>
        /// <param name="type">処理対象の型</param>
        /// <param name="typeName">型名</param>
        private void ProcessTypeByKind(HashSet<string> dependencies, ITypeSymbol type, string typeName)
        {
            switch (type)
            {
                case IArrayTypeSymbol arrayType:
                    // 配列の要素型を再帰的に処理
                    AddTypeIfRelevant(dependencies, arrayType.ElementType);
                    break;

                case INamedTypeSymbol namedType when namedType.IsGenericType:
                    // ジェネリック型引数を再帰的に処理
                    foreach (var typeArg in namedType.TypeArguments)
                    {
                        AddTypeIfRelevant(dependencies, typeArg);
                    }

                    // ジェネリック型自体も依存関係として追加
                    if ((namedType.TypeKind == TypeKind.Class || namedType.TypeKind == TypeKind.Interface) &&
                        !IsSystemTypeToExclude(typeName))
                    {
                        dependencies.Add(typeName);
                    }
                    break;

                default:
                    // 通常のクラス・インターフェース型
                    if (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Interface)
                    {
                        dependencies.Add(typeName);
                    }
                    break;
            }
        }

        /// <summary>
        /// C#のビルトイン型（プリミティブ型）を判定する
        /// これらの型は依存関係として記録する必要がない
        /// </summary>
        /// <param name="typeName">判定対象の型名</param>
        /// <returns>ビルトイン型の場合true</returns>
        private static bool IsBuiltInType(string typeName)
        {
            var builtInTypes = new HashSet<string>
            {
                "string", "int", "bool", "double", "float", "decimal",
                "long", "short", "byte", "char", "object", "void"
            };
            return builtInTypes.Contains(typeName);
        }

        /// <summary>
        /// 型宣言のXMLドキュメントコメントからsummaryタグの内容を抽出する
        /// APIドキュメント生成や依存関係理由の理解に活用
        /// </summary>
        /// <param name="decl">型宣言構文</param>
        /// <returns>抽出されたサマリー文字列</returns>
        private static string ExtractSummaryComment(BaseTypeDeclarationSyntax decl)
        {
            // 型宣言の先頭トリビア（コメント）を取得
            var trivia = decl.GetLeadingTrivia();
            var docCommentTrivia = trivia
                .Select(t => t.GetStructure())
                .OfType<DocumentationCommentTriviaSyntax>()
                .FirstOrDefault();

            if (docCommentTrivia == null)
                return string.Empty;

            // XMLドキュメントコメントからsummaryタグを抽出
            var xmlText = new StringBuilder();
            foreach (var node in docCommentTrivia.Content)
            {
                if (node is XmlElementSyntax element &&
                    element.StartTag.Name.LocalName.Text == "summary")
                {
                    xmlText.Append(string.Concat(element.Content.Select(c => c.ToString())));
                }
            }

            // コメント記号を除去してクリーンなテキストを返す
            var summary = xmlText.ToString().Trim();
            return summary.Replace("///", "").Trim();
        }

        #endregion
    }
}