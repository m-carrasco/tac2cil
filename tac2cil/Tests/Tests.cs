using Backend.Analyses;
using Backend.Transformations;
using Model;
using Model.Types;
using NUnit.Framework;
using System.IO;
using System.Linq;
using tac2cil.Assembler;

namespace Tests
{
    public class Tests
    {
        private void TransformToTac(MethodDefinition method)
        {
            var methodBodyBytecode = method.Body;
            var disassembler = new Disassembler(method);
            var methodBody = disassembler.Execute();
            method.Body = methodBody;

            var cfAnalysis = new ControlFlowAnalysis(method.Body);
            //var cfg = cfAnalysis.GenerateNormalControlFlow();
            var cfg = cfAnalysis.GenerateExceptionalControlFlow();

            var splitter = new WebAnalysis(cfg);
            splitter.Analyze();
            splitter.Transform();

            methodBody.UpdateVariables();

            var typeAnalysis = new TypeInferenceAnalysis(cfg, method.ReturnType);
            typeAnalysis.Analyze();

            // Copy Propagation
            var forwardCopyAnalysis = new ForwardCopyPropagationAnalysis(cfg);
            forwardCopyAnalysis.Analyze();
            forwardCopyAnalysis.Transform(methodBody);

            var backwardCopyAnalysis = new BackwardCopyPropagationAnalysis(cfg);
            backwardCopyAnalysis.Analyze();
            backwardCopyAnalysis.Transform(methodBody);

            methodBody.UpdateVariables();
        }

        [Test]
        public void BinaryOperationTest()
        {
            string source = @"
                            using System;
                            using System.Collections.Generic;
                            using System.Linq;
                            using System.Text.RegularExpressions;

                            namespace Test
                            {
                                public class Program
                                {
                                    public static void Main(string[] args)
                                    {
                                       int a=10;
                                       int b=10;
                                       int c=a + b;
                                    }
                                }
                            }";


            Compiler compiler = new Compiler();
            var output = compiler.CompileSource(source);

            Host host = new Host();
            ILoader provider = new CCIProvider.Loader(host);
            provider.LoadAssembly(output);

            var allDefinedMethods = from a in host.Assemblies
                                    from t in a.RootNamespace.GetAllTypes()
                                    from m in t.Members.OfType<MethodDefinition>()
                                    where m.HasBody && m.Name.Contains("Main")
                                    select m;

            MethodDefinition mainMethod = allDefinedMethods.First();
            MethodBody originalBytecodeBody = mainMethod.Body;
            TransformToTac(mainMethod);

            Assembler assembler = new Assembler(mainMethod.Body);
            var bytecodeBody = assembler.Execute();
        }

        [Test]
        public void ExportTest()
        {
            string source = @"
                            using System;
                            using System.Collections.Generic;
                            using System.Linq;
                            using System.Text.RegularExpressions;

                            namespace Test
                            {
                                public class Program
                                {
                                    public static void Main(string[] args)
                                    {
                                       int a=10;
                                       int b=10;
                                       int c=a + b;
                                    }
                                }
                            }";


            Compiler compiler = new Compiler();
            var output = compiler.CompileSource(source);

            Host host = new Host();
            ILoader provider = new CCIProvider.Loader(host);
            provider.LoadAssembly(output);

            var allDefinedMethods = from a in host.Assemblies
                                    from t in a.RootNamespace.GetAllTypes()
                                    from m in t.Members.OfType<MethodDefinition>()
                                    where m.HasBody
                                    select m;

            foreach (var definedMethod in allDefinedMethods)
            {
                MethodDefinition mainMethod = definedMethod;
                MethodBody originalBytecodeBody = mainMethod.Body;
                TransformToTac(mainMethod);
                originalBytecodeBody.Kind = MethodBodyKind.ThreeAddressCode;

                Assembler assembler = new Assembler(mainMethod.Body);
                var bytecodeBody = assembler.Execute();
                mainMethod.Body = bytecodeBody;
                mainMethod.Body.Kind = MethodBodyKind.Bytecode;
            }

            CodeGenerator.CecilCodeGenerator.CecilCodeGenerator exporter = new CodeGenerator.CecilCodeGenerator.CecilCodeGenerator(host);
            string outputDir = GetTemporaryDirectory();

            exporter.GenerateAssemblies(outputDir);
        }
        [Test]
        public void ExternMethod()
        {
            string source = @"
                using System;

                namespace Test
                {
                    public class Program
                    {
                        public static void Main(string[] args)
                        {
                            int a=10;
                            int b=10;
                            int c=a + b;
                            Console.WriteLine(c);
                        }
                    }
                }";


            Compiler compiler = new Compiler();
            var output = compiler.CompileSource(source);

            Host host = new Host();
            ILoader provider = new CCIProvider.Loader(host);
            provider.LoadAssembly(output);

            /*var allDefinedMethods = from a in host.Assemblies
                                    from t in a.RootNamespace.GetAllTypes()
                                    from m in t.Members.OfType<MethodDefinition>()
                                    where m.HasBody
                                    select m;

            foreach (var definedMethod in allDefinedMethods)
            {
                MethodDefinition mainMethod = definedMethod;
                MethodBody originalBytecodeBody = mainMethod.Body;
                TransformToTac(mainMethod);
                originalBytecodeBody.Kind = MethodBodyKind.ThreeAddressCode;

                Assembler assembler = new Assembler(mainMethod.Body);
                var bytecodeBody = assembler.Execute();
                mainMethod.Body = bytecodeBody;
                mainMethod.Body.Kind = MethodBodyKind.Bytecode;
            }*/

            CodeGenerator.CecilCodeGenerator.CecilCodeGenerator exporter = new CodeGenerator.CecilCodeGenerator.CecilCodeGenerator(host);
            string outputDir = GetTemporaryDirectory();

            exporter.GenerateAssemblies(outputDir);
        }

        [Test]
        public void VirtualCall()
        {
            string source = @"
                using System;
                namespace Test
                {
                    public class Program
                    {
                        public static void Main(string[] args)
                        {
                            A a = new A(5);
                            A(a);
                        }
                        
                        public static void A(A a){
                            a.print();
                        }
                    }
                    
                    public class A
                    {
                        int i = 0;
                        
                        public A(int _i){
                            i = _i;
                        }
                        
                        public void print(){
                            Console.WriteLine(i);
                        }
                    }
                }";


            Compiler compiler = new Compiler();
            var output = compiler.CompileSource(source);

            Host host = new Host();
            ILoader provider = new CCIProvider.Loader(host);
            provider.LoadAssembly(output);

            /*var allDefinedMethods = from a in host.Assemblies
                                    from t in a.RootNamespace.GetAllTypes()
                                    from m in t.Members.OfType<MethodDefinition>()
                                    where m.HasBody
                                    select m;

            foreach (var definedMethod in allDefinedMethods)
            {
                MethodDefinition mainMethod = definedMethod;
                MethodBody originalBytecodeBody = mainMethod.Body;
                TransformToTac(mainMethod);
                originalBytecodeBody.Kind = MethodBodyKind.ThreeAddressCode;

                Assembler assembler = new Assembler(mainMethod.Body);
                var bytecodeBody = assembler.Execute();
                mainMethod.Body = bytecodeBody;
                mainMethod.Body.Kind = MethodBodyKind.Bytecode;
            }*/

            CodeGenerator.CecilCodeGenerator.CecilCodeGenerator exporter = new CodeGenerator.CecilCodeGenerator.CecilCodeGenerator(host);
            string outputDir = GetTemporaryDirectory();

            exporter.GenerateAssemblies(outputDir);
        }

        private string GetTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }
    }
}
