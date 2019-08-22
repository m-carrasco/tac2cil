using Model;
using Model.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tac2cil.Assembler;

namespace Tests
{
    class TestHandler
    {
        public Object Test(string source, string typeName, string method, object[] parameters, bool convertToTac)
        {
            Compiler compiler = new Compiler();
            var output = compiler.CompileSource(source);

            Host host = new Host();
            ILoader provider = new CCIProvider.Loader(host);
            provider.LoadAssembly(output);

            if (convertToTac)
            {
                var allDefinedMethods = from a in host.Assemblies
                                        from t in a.RootNamespace.GetAllTypes()
                                        from m in t.Members.OfType<MethodDefinition>()
                                        where m.HasBody
                                        select m;

                foreach (var definedMethod in allDefinedMethods)
                {
                    MethodDefinition mainMethod = definedMethod;
                    MethodBody originalBytecodeBody = mainMethod.Body;
                    Utils.TransformToTac(mainMethod);
                    originalBytecodeBody.Kind = MethodBodyKind.ThreeAddressCode;

                    Assembler assembler = new Assembler(mainMethod.Body);
                    var bytecodeBody = assembler.Execute();
                    mainMethod.Body = bytecodeBody;
                    mainMethod.Body.Kind = MethodBodyKind.Bytecode;
                }
            }

            CodeGenerator.CecilCodeGenerator.CecilCodeGenerator exporter = new CodeGenerator.CecilCodeGenerator.CecilCodeGenerator(host);
            string outputDir = Utils.GetTemporaryDirectory();
            exporter.GenerateAssemblies(outputDir);

            var DLL = System.Reflection.Assembly.LoadFile(Path.Combine(outputDir, Path.GetFileName(output)));

            Type type = DLL.GetType(typeName);
            var methodInfoStatic = type.GetMethod(method);
            if (methodInfoStatic == null)
            {
                throw new Exception("No such static method exists.");
            }

            // Invoke static method
            Object result = methodInfoStatic.Invoke(null, parameters);

            return result;
        }
    }
}
