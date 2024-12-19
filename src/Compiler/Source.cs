using Microsoft.CodeAnalysis.Text;

using System.Text;

namespace FUICompiler
{
    /// <summary>
    /// 生成的源文件
    /// </summary>
    internal struct Source
    {
        public readonly string name;
        public SourceText Text { get; private set; }

        public Source(string name, string text)
        {
            this.name = name;
            this.Text = SourceText.From(text, Encoding.UTF8);
        }
    }
}
