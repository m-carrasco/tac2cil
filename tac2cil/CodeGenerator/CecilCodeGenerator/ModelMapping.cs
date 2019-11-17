using System;
using System.Collections.Generic;
using System.Linq;
using AnalysisNet = Model;
using Cecil = Mono.Cecil;
namespace CodeGenerator.CecilCodeGenerator
{
    // the class holds a mapping between analysis-net and cecil code model objects
    internal class ModelMapping
    {
        public ModelMapping()
        {
            FieldsMap = new Dictionary<AnalysisNet.Types.IFieldReference, Cecil.FieldReference>();
            TypesMap = new Dictionary<AnalysisNet.Types.IType, Cecil.TypeReference>();
            MethodsMap = new Dictionary<AnalysisNet.Types.IMethodReference, Cecil.MethodReference>();
            AssembliesMap = new Dictionary<AnalysisNet.Assembly, Cecil.AssemblyDefinition>();
        }

        public IDictionary<AnalysisNet.Assembly, Cecil.AssemblyDefinition> AssembliesMap { get; }
        public IDictionary<AnalysisNet.Types.IMethodReference, Cecil.MethodReference> MethodsMap { get; }
        public IDictionary<AnalysisNet.Types.IType, Cecil.TypeReference> TypesMap { get; }
        public IDictionary<AnalysisNet.Types.IFieldReference, Cecil.FieldReference> FieldsMap { get; }

        public AnalysisNet.Assembly GetAnalysisNetAssembly(AnalysisNet.IAssemblyReference assemblyReference)
        {
            IEnumerable<AnalysisNet.Assembly> assemblyQuery = AssembliesMap.Keys.Where(a => a.MatchReference(assemblyReference));

            if (assemblyQuery.Count() > 1)
            {
                throw new Exception("at most one assembly should match");
            }

            AnalysisNet.Assembly assembly = assemblyQuery.FirstOrDefault();

            return assembly;
        }
    }
}
