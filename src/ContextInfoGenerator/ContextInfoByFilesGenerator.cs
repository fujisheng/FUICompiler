using Microsoft.CodeAnalysis;

namespace FUICompiler
{
    /// <summary>
    /// 通过绑定文件生成上下文信息  需要扩展其他生成方式可以使用这个方式  只需要输出ContextBindingInfo的json文件即可
    /// 例如：
    /// 你可以通过UI编辑器生成一个json文件  然后通过这个生成器读取json文件生成上下文信息
    /// 或者自行实现在Inspector中生成上下文信息
    /// 或者可以通过名字约定生成上下文信息
    /// </summary>
    internal class ContextInfoByFilesGenerator : IContextInfoGenerator
    {
        /// <summary>
        /// 文件存放路径
        /// </summary>
        readonly string directory;

        /// <summary>
        /// 文件扩展名
        /// </summary>
        readonly string extension;

        private ContextInfoByFilesGenerator() { }

        /// <summary>
        /// 构建一个通过文件生成上下文信息的生成器
        /// </summary>
        /// <param name="directory">文件存放路径</param>
        /// <param name="extension">扩展名 .xxx</param>
        internal ContextInfoByFilesGenerator(string directory, string extension)
        {
            this.directory = directory;
        }

        public List<ContextBindingInfo> Generate(SemanticModel semanticModel, SyntaxNode root)
        {
            if (!Directory.Exists(directory))
            {
                return null;
            }

            var files = Directory.GetFiles(directory, $"*{extension}", SearchOption.AllDirectories);
            var result = new List<ContextBindingInfo>();
            foreach(var file in files)
            {
                var info = Newtonsoft.Json.JsonConvert.DeserializeObject<ContextBindingInfo>(file);
                result.Add(info);
            }

            return result;
        }
    }
}
