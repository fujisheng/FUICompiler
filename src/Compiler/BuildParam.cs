namespace FUICompiler
{
    /// <summary>
    /// 绑定上下文生成类型
    /// </summary>
    public enum BindingContextGenerateType
    {
        /// <summary>
        /// 通过特性生成
        /// </summary>
        Attribute = 1 << 0,

        /// <summary>
        /// 通过配置文件生成
        /// </summary>
        Config = 1 << 1,

        /// <summary>
        /// 通过描述文件生成
        /// </summary>
        Descriptor = 1 << 2,

        /// <summary>
        /// 混合生成
        /// </summary>
        Mix = Attribute | Config | Descriptor,
    }

    /// <summary>
    /// 编译参数
    /// </summary>
    public struct BuildParam
    {
        /// <summary>
        /// 解决方案路径
        /// </summary>
        public readonly string solutionPath;

        /// <summary>
        /// 工程名
        /// </summary>
        public readonly string projectName;

        /// <summary>
        /// 输出路径
        /// </summary>
        public readonly string output;

        /// <summary>
        /// 存放绑定配置的路径
        /// </summary>
        public readonly string bindingPath;

        /// <summary>
        /// 存放生成代码的路径 为空则不保存生成的代码
        /// </summary>
        public readonly string generatedPath;

        /// <summary>
        /// 绑定上下文生成类型
        /// </summary>
        public readonly BindingContextGenerateType contextGenerateType;

        /// <summary>
        /// 绑定配置输出路径
        /// </summary>
        public readonly string bindingOutput;

        /// <summary>
        /// 构造编译参数
        /// </summary>
        /// <param name="solutionPath">解决方案路径</param>
        /// <param name="projectName">工程名</param>
        /// <param name="output">工程输出路径</param>
        /// <param name="bindingPath">存放绑定资源的路径</param>
        /// <param name="generatedPath">存放生成的代码的路径</param>
        public BuildParam(string solutionPath, string projectName, string output, string bindingPath, string generatedPath, BindingContextGenerateType contextGenerateType, string bindingOutput)
        {
            this.solutionPath = solutionPath;
            this.projectName = projectName;
            this.output = output;
            this.bindingPath = bindingPath;
            this.generatedPath = generatedPath;
            this.contextGenerateType = contextGenerateType;
            this.bindingOutput = bindingOutput;
        }
    }
}
