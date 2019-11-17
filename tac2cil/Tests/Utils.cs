using Backend.Analyses;
using Backend.Transformations;
using Model.Types;
using System.IO;

namespace Tests
{
    internal class Utils
    {
        public static string GetTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        public static void TransformToTac(MethodDefinition method)
        {
            Disassembler disassembler = new Disassembler(method);
            MethodBody methodBody = disassembler.Execute();
            method.Body = methodBody;

            ControlFlowAnalysis cfAnalysis = new ControlFlowAnalysis(method.Body);
            //var cfg = cfAnalysis.GenerateNormalControlFlow();
            Backend.Model.ControlFlowGraph cfg = cfAnalysis.GenerateExceptionalControlFlow();

            WebAnalysis splitter = new WebAnalysis(cfg);
            splitter.Analyze();
            splitter.Transform();

            methodBody.UpdateVariables();

            TypeInferenceAnalysis typeAnalysis = new TypeInferenceAnalysis(cfg, method.ReturnType);
            typeAnalysis.Analyze();

            // Copy Propagation
            ForwardCopyPropagationAnalysis forwardCopyAnalysis = new ForwardCopyPropagationAnalysis(cfg);
            forwardCopyAnalysis.Analyze();
            forwardCopyAnalysis.Transform(methodBody);

            BackwardCopyPropagationAnalysis backwardCopyAnalysis = new BackwardCopyPropagationAnalysis(cfg);
            backwardCopyAnalysis.Analyze();
            backwardCopyAnalysis.Transform(methodBody);

            methodBody.UpdateVariables();
        }
    }
}
