using NUnit.Framework;

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
            "Tests.Resources.InOutParameters.cs/Test.Program/TestIn", // 10
            "Tests.Resources.InOutParameters.cs/Test.Program/TestOut", // 11

            "Tests.Resources.IfConditionals.cs/Test.Program/Test", // 12
            "Tests.Resources.IfConditionals.cs/Test.Program/Test", // 13
            "Tests.Resources.IfConditionals.cs/Test.Program/Test", // 14

            "Tests.Resources.Loop.cs/Test.Program/Test", // 15
            "Tests.Resources.Loop.cs/Test.Program/Test", // 16

            "Tests.Resources.Arrays.cs/Test.Program/Test1", // 17
            "Tests.Resources.Arrays.cs/Test.Program/Test2", // 18
            "Tests.Resources.Arrays.cs/Test.Program/Test3", // 19
            "Tests.Resources.Arrays.cs/Test.Program/Test4", // 20

            "Tests.Resources.Generics.cs/Test.Program/Test", // 21
            "Tests.Resources.Generics.cs/Test.Program/Test1", // 22
        };

        private static readonly object[] TestReturnValueParameters =
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
            null, // 9
            null, // 10
            null, // 11
            49, // 12
            199, // 13
            -2, // 14
            0, // 15
            8, // 16
            null, // 17
            null, // 18
            null, // 19
            null, // 20
            null, // 21
            null // 22
        };

        private void TestReturnValue(string testSeed, object parameters, bool cciProvider, bool tac)
        {
            char[] s = { '/' };
            var resourceToTest = testSeed.Split(s);

            string sourceCodeResource = resourceToTest[0];
            string type = resourceToTest[1];
            string method = resourceToTest[2];

            var source = GetTestSourceCode(sourceCodeResource);
            TestHandler testHandler = new TestHandler();
            object[] param = parameters == null ? null : new object[1] { parameters };

            var r = testHandler.Test(source, type, method, param, tac, cciProvider);
            var expectedResult = testHandler.RunOriginalCode(source, type, method, param);

            Assert.AreEqual(expectedResult, r);
        }

        [Test, Sequential]
        public void TestReturnValueCCINoTac(
            [ValueSource("TestReturnValueSeeds")] string testSeed,
            [ValueSource("TestReturnValueParameters")] object parameters)
        {
            TestReturnValue(testSeed, parameters, true, false);
        }

        [Test, Sequential]
        public void TestReturnValueMetadataProviderNoTac(
        [ValueSource("TestReturnValueSeeds")] string testSeed,
        [ValueSource("TestReturnValueParameters")] object parameters)
        {
            TestReturnValue(testSeed, parameters, false, false);
        }
    }
}
