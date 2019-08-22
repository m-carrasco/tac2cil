using Backend.Analyses;
using Backend.Transformations;
using Model.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    class Utils
    {
        public static string GetTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        public static void TransformToTac(MethodDefinition method)
        {
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
    }
}
