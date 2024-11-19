using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using Mono.Cecil;

namespace FUICompiler
{
    internal struct Source
    {
        public readonly string name;
        public SourceText Text { get; private set; }

        public Source(string name, string text)
        {
            this.name = name;
            this.Text = SourceText.From(text, System.Text.Encoding.UTF8);
        }

        public void BuildText(string text)
        {
            this.Text = SourceText.From(text, System.Text.Encoding.UTF8);
        }
    }

    /// <summary>
    /// 根据类型的语法树 生成代码
    /// </summary>
    internal interface ITypeSyntaxNodeSourcesGenerator
    {
        Source?[] Generate(SemanticModel semanticModel, SyntaxNode root, out SyntaxNode newRoot);
    }

    /// <summary>
    /// 编译前源代码生成器
    /// </summary>
    internal interface IBeforeCompilerSourcesGenerator
    {
        Source?[] Generate();
    }

    /// <summary>
    /// 类型定义注入器
    /// </summary>
    internal interface ITypeDefinationInjector
    {
        void Inject(ModuleDefinition moduleDefinition, TypeDefinition typeDefinition);
    }
}
