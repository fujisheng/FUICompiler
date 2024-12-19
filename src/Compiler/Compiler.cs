using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;

namespace FUICompiler
{
    internal class Compiler
    {
        /// <summary>
        /// 构建参数
        /// </summary>
        readonly BuildParam param;

        /// <summary>
        /// 所有的绑定上下文信息生成器
        /// </summary>
        readonly ContextInfoGenerators contextInfoGenerators;

        /// <summary>
        /// 可观察对象生成器
        /// </summary>
        readonly ObservableObjectGenerator observableObjectGenerator;

        /// <summary>
        /// 绑定上下文生成器
        /// </summary>
        readonly BindingContextGenerator bindingContextGenerator;

        internal Compiler(BuildParam param)
        {
            this.param = param;
            contextInfoGenerators = new ContextInfoGenerators(param);
            observableObjectGenerator = new ObservableObjectGenerator();
            bindingContextGenerator = new BindingContextGenerator();
        }

        /// <summary>
        /// 编译一个工程
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        internal async Task Build()
        {
            //加载工程
            var (project, compilation) = await ProjectLoader.Load(param);

            //生成上下文信息
            var contexts = await contextInfoGenerators.Generate(project, compilation, param);

            //所有生成的代码
            List<Source> addition = new List<Source>();

            //生成ObservableObject相关代码
            var (newProject, append) = await observableObjectGenerator.Generate(project, compilation);
            addition.AddRange(append);
            project = newProject;

            //生成绑定上下文相关代码
            addition.AddRange(bindingContextGenerator.Generate(contexts));
            
            //保存生成的代码文件
            SaveGeneratedSources(addition, param.generatedPath);

            //将生成的代码添加到工程中
            foreach (var add in addition)
            {
                var filePath = string.IsNullOrEmpty(param.generatedPath) ? null : $"{param.generatedPath}\\{add.name}.cs";
                project = project.AddDocument(add.name, add.Text, filePath: Path.GetFullPath(filePath)).Project;
            }

            //编译工程
            var asm = await InternalBuild(project);

            //编译失败
            if(asm == default)
            {
                Message.Message.WriteMessage(Message.MessageType.Log, new Message.LogMessage
                {
                    Level = Message.LogLevel.Error,
                    Message = $"compiler error"
                });
                return;
            }

            //编译成功 输出最终的dll
            var dllPath = $"{param.output}\\{param.projectName}.dll";
            var pdbPath = $"{param.output}\\{param.projectName}.pdb";
            File.WriteAllBytes(dllPath, asm.dllStream.ToArray());
            File.WriteAllBytes(pdbPath, asm.pdbStream.ToArray());

            Message.Message.WriteMessage(Message.MessageType.Log, new Message.LogMessage
            {
                Level = Message.LogLevel.Info,
                Message = $"compiler complete at:{param.output}"
            });
        }

        /// <summary>
        /// 编译一个工程
        /// </summary>
        /// <param name="project">工程</param>
        /// <returns></returns>
        async Task<(MemoryStream dllStream, MemoryStream pdbStream)> InternalBuild(Project project)
        {
            var compilation = await project.GetCompilationAsync();
            var dllStream = new MemoryStream();
            var pdbStream = new MemoryStream();

            var emitOptions = new EmitOptions(false, DebugInformationFormat.PortablePdb);
            var result = compilation.Emit(dllStream, pdbStream, options: emitOptions);
            if (!result.Success)
            {
                IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                diagnostic.IsWarningAsError ||
                diagnostic.Severity == DiagnosticSeverity.Error);

                foreach (Diagnostic diagnostic in failures)
                {
                    Message.Message.WriteMessage(Message.MessageType.Compiler, new Message.CompilerMessage
                    {
                        Error = diagnostic.ToString()
                    });
                }
                return default;
            }

            dllStream.Seek(0, SeekOrigin.Begin);
            pdbStream.Seek(0, SeekOrigin.Begin);
            return (dllStream, pdbStream);
        }

        /// <summary>
        /// 保存生成的文件
        /// </summary>
        /// <param name="addition"></param>
        /// <param name="path"></param>
        void SaveGeneratedSources(List<Source> addition, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            var directoryInfo = Utility.GetOrCreateDirectory(path);
            foreach (var source in addition)
            {
                var fileName = $"{source.name}.cs";
                File.WriteAllText($"{directoryInfo.FullName}\\{fileName}", source.Text.ToString(), System.Text.Encoding.UTF8);
            }
        }
    }
}
