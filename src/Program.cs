using FUICompiler;
using FUICompiler.Message;

const string slnMark = "--sln";
const string projectMark = "--project";
const string outputMark = "--output";
const string bindingConfigMark = "--binding";
const string generatedPathMark = "--generated";
const string contextGenerateTypeMark = "--ctx_type";
const string bindingOutputMark = "--binding_output";

try
{
    var compiler = new Compiler();
    string workspace = "..\\..\\..\\..\\..\\..\\FUI\\";
    args = $"--sln={workspace}.\\FUI.sln --project=FUI.Test --output={workspace}.\\Library\\ScriptAssemblies --binding={workspace}.\\Binding\\ --generated={workspace}.\\FUI\\Generated\\ --ctx_type=Attribute --binding_output={workspace}.\\FUI\\BindingInfo\\".Split(' ');
    var param = ParseArgs(args);

    if (param.contextGenerateType == BindingContextGenerateType.Mix || param.contextGenerateType == BindingContextGenerateType.Attribute)
    {
        compiler.typeSyntaxRootGenerators.Add(new AttributeBindingContextGenerator(param));
        compiler.typeSyntaxRootGenerators.Add(new DescriptorBindingContextGenerator(param));
    }

    if (param.contextGenerateType == BindingContextGenerateType.Mix || param.contextGenerateType == BindingContextGenerateType.Config)
    {
        compiler.beforeCompilerSourcesGenerators.Add(new BindingContextGenerator(param.bindingPath, ".binding"));
    }

    compiler.typeSyntaxRootGenerators.Add(new ObservableObjectAppendGenerator());
    //compiler.typeDefinationInjectors.Add(new PropertyChangedInjector());


    //Console.WriteLine(@$"
    //start build
    //sln:{Path.GetFullPath(param.solutionPath)}
    //project:{param.projectName}
    //output:{Path.GetFullPath(param.output)}
    //binding:{Path.GetFullPath(param.bindingPath)}
    //context_generate_type:{param.contextGenerateType}");
    await compiler.Build(param);
}
catch(Exception e)
{
    Message.WriteMessage(MessageType.Log, new LogMessage
    {
        Level = LogLevel.Error,
        Message = e.ToString(),
    });
}


BuildParam ParseArgs(string[] args)
{
    var solutionPathIndex = Array.FindIndex(args, (k) => k.StartsWith(slnMark));
    var projectNameIndex = Array.FindIndex(args, (k) => k.StartsWith(projectMark));
    var outputIndex = Array.FindIndex(args, (k) => k.StartsWith(outputMark));
    var bindingPathIndex = Array.FindIndex(args, (k) => k.StartsWith(bindingConfigMark));
    var generatedPathIndex = Array.FindIndex(args, (k) => k.StartsWith(generatedPathMark));
    var contextGenerateTypeIndex = Array.FindIndex(args, (k) => k.StartsWith(contextGenerateTypeMark));
    var bindingOutputIndex = Array.FindIndex(args, (k) => k.StartsWith(bindingOutputMark));

    if (solutionPathIndex == -1 || projectNameIndex == -1 || outputIndex == -1)
    {
        throw new ArgumentException($"Invalid args, \n required: --sln=your_sln_path --project=your_project_path --output=your_output_path \n optional: -- binding=your_binding_file_path  --generated=your_generated_cs_path --ctx_type=your_binding_context_generate_type");
    }

    return new BuildParam
    (
        solutionPath: args[solutionPathIndex].Substring(slnMark.Length + 1),
        projectName: args[projectNameIndex].Substring(projectMark.Length + 1),
        output: args[outputIndex].Substring(outputMark.Length + 1),
        bindingPath: bindingPathIndex == -1 ? string.Empty : args[bindingPathIndex].Substring(bindingConfigMark.Length + 1),
        generatedPath: generatedPathIndex == -1 ? string.Empty : args[generatedPathIndex].Substring(generatedPathMark.Length + 1),
        contextGenerateType: contextGenerateTypeIndex == -1 ? BindingContextGenerateType.Mix : Enum.Parse<BindingContextGenerateType>(args[contextGenerateTypeIndex].Substring(contextGenerateTypeMark.Length + 1)),
        bindingOutput: bindingOutputIndex == -1 ? string.Empty : args[bindingOutputIndex].Substring(bindingOutputMark.Length + 1)
    );
}