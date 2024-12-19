using Microsoft.CodeAnalysis;

namespace FUICompiler
{
    /// <summary>
    /// 绑定上下文信息生成器
    /// </summary>
    internal interface IContextInfoGenerator
    {
        List<ContextBindingInfo> Generate(SemanticModel semanticModel, SyntaxNode root);
    }
}
