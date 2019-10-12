using CodeGenerator.CecilCodeGenerator;
using System.Collections.Generic;
using AnalysisNet = Model;
using Cecil = Mono.Cecil;

namespace CodeGenerator.CecilCodeGenerator
{
    class Context
    {
        public Context(Cecil.ModuleDefinition current, ModelMapping modelMapping)
        {
            this.CurrentModule = current;
            this.ModelMapping = modelMapping;
        }

        public Cecil.ModuleDefinition CurrentModule { get; }
        public ModelMapping ModelMapping { get; }

    }
}
