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

        [TestCase("Tests.Resources.ObjectInitObjectFields.cs", "Test.Program", "Test", null, false, false)]
        public void TestNoCrash(string sourceCodeResource, string type, string method, object[] parameters, bool useTac, bool cciProvider)
        {
            var source = GetTestSourceCode(sourceCodeResource);
            TestHandler testHandler = new TestHandler();
            testHandler.Test(source, type, method, parameters, useTac, cciProvider);
        }

        private static readonly string[] TestReturnValueSeeds =
        {
            "Tests.Resources.AccessIntField.cs/Test.Program/Test", // 0
            "Tests.Resources.Sum.cs/Test.Program/Test", // 1
            "Tests.Resources.RefParameter.cs/Test.Program/Test", // 2
            "Tests.Resources.RefParameter.cs/Test.Program/TestBool", //3
            "Tests.Resources.RefParameter.cs/Test.Program/TestFloat", // 4
            "Tests.Resources.RefParameter.cs/Test.Program/TestObject", // 5
            "Tests.Resources.RefParameter2.cs/Test.Program/TestInt", // 6
            "Tests.Resources.RefParameter2.cs/Test.Program/TestBool", // 7
            "Tests.Resources.RefParameter2.cs/Test.Program/TestFloat", // 8
            "Tests.Resources.RefParameter2.cs/Test.Program/TestObject", // 9
        };

        private static readonly string[] TestReturnValueParameters =
        {
            null, // 0
            null, // 1
            null, // 2
            null, // 3
            null, // 4
            null, // 5
            null, // 6
            null, // 7
            null, // 8
            null  // 9
        };

        private static readonly object[] TestReturnValueExpectedResult =
        {
            5, // 0
            20, // 1
            10, // 2
            true, // 3
            10.0f, // 4
            null, // 5
            1, // 6
            true, // 7
            5.0f, // 8
            null // 9
        };

        [Test, Sequential]
        public void TestReturnValueCCINoTac(
            [ValueSource("TestReturnValueSeeds")] string testSeed,
            [ValueSource("TestReturnValueParameters")] object parameters,
            [ValueSource("TestReturnValueExpectedResult")] object expectedResult)
        {
            char[] s = { '/' };
            var resourceToTest = testSeed.Split(s);

            string sourceCodeResource = resourceToTest[0];
            string type = resourceToTest[1];
            string method = resourceToTest[2];

            var source = GetTestSourceCode(sourceCodeResource);
            TestHandler testHandler = new TestHandler();
            object[] param = null;
            var r = testHandler.Test(source, type, method, param, false, true);
            Assert.AreEqual(r, expectedResult);
        }

        [Test, Sequential]
        public void TestReturnValueMetadataProviderNoTac(
        [ValueSource("TestReturnValueSeeds")] string testSeed,
        [ValueSource("TestReturnValueParameters")] object parameters,
        [ValueSource("TestReturnValueExpectedResult")] object expectedResult)
        {
            char[] s = { '/' };
            var resourceToTest = testSeed.Split(s);

            string sourceCodeResource = resourceToTest[0];
            string type = resourceToTest[1];
            string method = resourceToTest[2];

            var source = GetTestSourceCode(sourceCodeResource);
            TestHandler testHandler = new TestHandler();
            object[] param = null;
            var r = testHandler.Test(source, type, method, param, false, false);
            Assert.AreEqual(r, expectedResult);
        }
    }
}
