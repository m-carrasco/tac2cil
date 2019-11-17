using Cecil = Mono.Cecil;

namespace CodeGenerator.CecilCodeGenerator
{
    internal class Context
    {
        public Context(Cecil.ModuleDefinition current, ModelMapping modelMapping)
        {
            CurrentModule = current;
            ModelMapping = modelMapping;
        }

        public Cecil.ModuleDefinition CurrentModule { get; }
        public ModelMapping ModelMapping { get; }

    }
}
