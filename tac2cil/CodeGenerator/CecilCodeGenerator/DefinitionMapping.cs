using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnalysisNet = Model;
using Cecil = Mono.Cecil;
namespace CodeGenerator.CecilCodeGenerator
{
    // the class holds a mapping between analysis-net and cecil definitions
    class DefinitionMapping
    {
        public DefinitionMapping()
        {
            FieldsMap = new Dictionary<AnalysisNet.Types.FieldDefinition, Cecil.FieldDefinition>();
            TypesMap = new Dictionary<AnalysisNet.Types.TypeDefinition, Cecil.TypeDefinition>();
            MethodsMap = new Dictionary<AnalysisNet.Types.MethodDefinition, Cecil.MethodDefinition>();
            AssembliesMap = new Dictionary<AnalysisNet.Assembly, Cecil.AssemblyDefinition>();
        }

        public IDictionary<AnalysisNet.Assembly, Cecil.AssemblyDefinition> AssembliesMap { get; }
        public IDictionary<AnalysisNet.Types.MethodDefinition, Cecil.MethodDefinition> MethodsMap { get; }
        public IDictionary<AnalysisNet.Types.TypeDefinition, Cecil.TypeDefinition> TypesMap { get; }
        public IDictionary<AnalysisNet.Types.FieldDefinition, Cecil.FieldDefinition> FieldsMap { get; }

        public AnalysisNet.Assembly GetAnalysisNetAssembly(AnalysisNet.IAssemblyReference assemblyReference)
        {
            var assemblyQuery = AssembliesMap.Keys.Where(a => a.MatchReference(assemblyReference));

            if (assemblyQuery.Count() > 1)
                throw new Exception("at most one assembly should match");

            AnalysisNet.Assembly assembly = assemblyQuery.FirstOrDefault();

            return assembly;
        }
    }
}
