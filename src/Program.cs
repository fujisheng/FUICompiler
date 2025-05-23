﻿using FUICompiler;
using FUICompiler.Message;

const string slnMark = "--sln";
const string projectMark = "--project";
const string outputMark = "--output";
const string bindingConfigMark = "--binding";
const string generatedPathMark = "--generated";
const string contextGenerateTypeMark = "--ctx_type";
const string bindingOutputMark = "--binding_output";
const string modifiedOutputMark = "--modified_output";

try
{
    //string workspace = "..\\..\\..\\..\\..\\..\\workspace\\AlienExodus\\Client\\";
    //args = $"--sln={workspace}.\\Client.sln --project=Game.UI --output={workspace}.\\Library\\ScriptAssemblies --binding={workspace}.\\Binding\\ --generated={workspace}.\\FUI\\Generated\\  --binding_output={workspace}.\\FUI\\BindingInfo\\".Split(' ');
    var param = ParseArgs(args);
    var compiler = new Compiler(param);
    await compiler.Build();
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
    var modifiedOutputIndex = Array.FindIndex(args, (k) => k.StartsWith(modifiedOutputMark));

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
        contextGenerateType: contextGenerateTypeIndex == -1 ? BindingContextGenerateType.All : (BindingContextGenerateType)int.Parse(args[contextGenerateTypeIndex].Substring(contextGenerateTypeMark.Length + 1)),
        bindingOutput: bindingOutputIndex == -1 ? string.Empty : args[bindingOutputIndex].Substring(bindingOutputMark.Length + 1),
        modifiedOutput: modifiedOutputIndex == -1 ? string.Empty : args[modifiedOutputIndex].Substring(modifiedOutputMark.Length + 1)
    );
}