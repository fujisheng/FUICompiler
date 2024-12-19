using Microsoft.CodeAnalysis;

namespace FUICompiler
{
    /// <summary>
    /// 所有上下文信息生成器
    /// </summary>
    internal class ContextInfoGenerators
    {
        readonly List<IContextInfoGenerator> generators;

        internal ContextInfoGenerators(BuildParam param)
        {
            this.generators = new List<IContextInfoGenerator>();
            if (param.contextGenerateType.HasFlag(BindingContextGenerateType.Attribute))
            {
                this.generators.Add(new ContextInfoByAttributeGenerator());
            }

            if(param.contextGenerateType.HasFlag(BindingContextGenerateType.Config))
            {
                this.generators.Add(new ContextInfoByFilesGenerator(string.Empty, string.Empty));
            }

            if(param.contextGenerateType.HasFlag(BindingContextGenerateType.Descriptor))
            {
                this.generators.Add(new ContextInfoByDescriptorGenerator());
            }
        }

        /// <summary>
        /// 生成一个工程的上下文信息
        /// </summary>
        /// <param name="project">目标项目</param>
        /// <param name="compilation">编译</param>
        /// <param name="buildParam">构建参数</param>
        /// <returns></returns>
        internal async Task<IReadOnlyList<ContextBindingInfo>> Generate(Project project, Compilation compilation, BuildParam buildParam)
        {
            var result = new List<ContextBindingInfo>();
            foreach (var document in project.Documents)
            {
                var root = await document.GetSyntaxRootAsync();
                if (root == null)
                {
                    continue;
                }

                var semanticModel = compilation.GetSemanticModel(root.SyntaxTree);
                foreach (var generator in generators)
                {
                    var infos = generator.Generate(semanticModel, root);
                    if (infos == null)
                    {
                        continue;
                    }

                    foreach (var info in infos)
                    {
                        if (info == null)
                        {
                            continue;
                        }
                        result.Add(info);
                        TrySaveToFile(info, buildParam.bindingOutput);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 尝试保存绑定信息到文件
        /// </summary>
        /// <param name="info">要保存的绑定信息</param>
        /// <param name="output">输出文件夹</param>
        void TrySaveToFile(ContextBindingInfo info, string output)
        {
            if (!string.IsNullOrEmpty(output))
            {
                return;
            }

            if (!Directory.Exists(output))
            {
                Directory.CreateDirectory(output);
            }

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(info, Newtonsoft.Json.Formatting.Indented);
            var fileName = $"{Utility.GetBindingContextTypeName(info)}.binding";
            var file = Path.Combine(output, fileName);
            File.WriteAllText(file, json);
        }
    }
}
