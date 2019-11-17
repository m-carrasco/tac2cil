using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tests
{
    public class Compiler
    {
        public string CompileSource(string source)
        {
            SyntaxTree parsedSyntaxTree = Parse(source, "", CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));

            string outputFile = Path.GetTempFileName();
            string outputFileName = Path.GetFileName(outputFile);
            CSharpCompilation compilation
                = CSharpCompilation.Create(outputFileName, new SyntaxTree[] { parsedSyntaxTree }, DefaultReferences, DefaultCompilationOptions);

            Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(outputFile);

            if (!result.Success)
            {
                throw new Exception();
            }

            return outputFile;
        }

        private static readonly IEnumerable<string> DefaultNamespaces = new[]
        {
            "System",
            "System.IO",
            "System.Net",
            "System.Linq",
            "System.Text",
            "System.Text.RegularExpressions",
            "System.Collections.Generic"
        };

        private static readonly string runtimePath = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location);

        private readonly IEnumerable<MetadataReference> DefaultReferences =
            new[]
            {
                MetadataReference.CreateFromFile(Path.Combine(runtimePath, "mscorlib.dll")),
                MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.dll")),
                MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Core.dll"))
            };

        private readonly CSharpCompilationOptions DefaultCompilationOptions =
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOverflowChecks(true).WithOptimizationLevel(OptimizationLevel.Release)
                    .WithUsings(DefaultNamespaces);

        public SyntaxTree Parse(string text, string filename = "", CSharpParseOptions options = null)
        {
            SourceText stringText = SourceText.From(text, Encoding.UTF8);
            return SyntaxFactory.ParseSyntaxTree(stringText, options, filename);
        }
    }
}
