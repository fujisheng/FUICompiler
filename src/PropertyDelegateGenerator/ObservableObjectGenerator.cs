using Microsoft.CodeAnalysis;

namespace FUICompiler
{
    internal class ObservableObjectGenerator
    {
        readonly ObservableObjectDelegateGenerator delegateGenerator;
        readonly ObservableObjectPropertyModifier propertyModifier;
        internal ObservableObjectGenerator()
        {
            delegateGenerator = new ObservableObjectDelegateGenerator();
            propertyModifier = new ObservableObjectPropertyModifier();
        }

        internal async Task<(Project project, List<Source> addition)> Generate(Project oldProject, Compilation compilation)
        {
            var addition = new List<Source>();
            List<(DocumentId remove, Document add)> modifiedDocument = new List<(DocumentId remove, Document add)>();
            foreach (var document in oldProject.Documents)
            {
                var root = await document.GetSyntaxRootAsync();
                if (root == null)
                {
                    continue;
                }

                var semanticModel = compilation.GetSemanticModel(root.SyntaxTree);
                var append = delegateGenerator.Generate(semanticModel, root);
                addition.AddRange(append);

                var newRoot = propertyModifier.Modify(semanticModel, root);
                if (newRoot == root)
                {
                    continue;
                }

                var newDocument = document.WithSyntaxRoot(newRoot);
                modifiedDocument.Add((document.Id, newDocument));
            }

            foreach (var item in modifiedDocument)
            {
                oldProject = oldProject
                    .RemoveDocument(item.remove)
                    .AddDocument(item.add.Name, item.add.GetTextAsync().Result, filePath: item.add.FilePath).Project;
            }

            return (oldProject, addition);
        }
    }
}
