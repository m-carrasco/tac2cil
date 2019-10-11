using CodeGenerator.CecilCodeGenerator;
using System.Collections.Generic;
using AnalysisNet = Model;
using Cecil = Mono.Cecil;

namespace CodeGenerator.CecilCodeGenerator
{
    class Context
    {
        public Context(Cecil.ModuleDefinition current, DefinitionMapping definitionMapping)
        {
            this.CurrentModule = current;
            this.DefinitionMapping = definitionMapping;
        }

        public Cecil.ModuleDefinition CurrentModule { get; }
        public DefinitionMapping DefinitionMapping { get; }

    }
}
