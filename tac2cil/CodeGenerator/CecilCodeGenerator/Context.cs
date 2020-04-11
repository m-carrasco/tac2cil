using Cecil = Mono.Cecil;

namespace CodeGenerator.CecilCodeGenerator
{
    internal class Context
    {
        public Context(Cecil.ModuleDefinition current, ModelMapping modelMapping, Model.Host host)
        {
            CurrentModule = current;
            ModelMapping = modelMapping;
            Host = host;
        }

        public Cecil.ModuleDefinition CurrentModule { get; }
        public ModelMapping ModelMapping { get; }
        public Model.Host Host { get; }
    }
}
