using Backend.Analyses;
using Backend.Transformations;
using Model;
using Model.Types;
using NUnit.Framework;
using System.IO;
using System.Linq;
using tac2cil.Assembler;
using System.Reflection;
using MethodBody = Model.Types.MethodBody;
using System;

namespace Tests
{
    public class Tests
    {
        public string GetTestSourceCode(string res)
        {
            var sourceStream = System.Reflection.Assembly.GetAssembly(typeof(Tests)).GetManifestResourceStream(res);
            System.IO.StreamReader streamReader = new System.IO.StreamReader(sourceStream);
            var source = streamReader.ReadToEnd();
            return source;
        }

        [TestCase("Tests.Resources.ObjectInitObjectFields.cs", "Test.Program", "Test", null, false)]
        public void TestNoCrash(string sourceCodeResource, string type, string method, object[] parameters, bool useTac)
        {
            var source = GetTestSourceCode(sourceCodeResource);
            TestHandler testHandler = new TestHandler();
            testHandler.Test(source, type, method, parameters, useTac);
        }

        [TestCase("Tests.Resources.AccessIntField.cs", "Test.Program", "Test", null, false, ExpectedResult=5)]
        [TestCase("Tests.Resources.Sum.cs", "Test.Program", "Test", null, false, ExpectedResult = 20)]
        public object TestReturnValue(string sourceCodeResource, string type, string method, object[] parameters, bool useTac)
        {
            var source = GetTestSourceCode(sourceCodeResource);
            TestHandler testHandler = new TestHandler();
            return testHandler.Test(source, type, method, parameters, useTac);
        }
    }
}
