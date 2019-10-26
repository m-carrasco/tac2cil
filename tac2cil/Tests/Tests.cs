using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace Tests
{
    public class Tests
    {
        public string GetResourceAsString(string res)
        {
            var sourceStream = System.Reflection.Assembly.GetAssembly(typeof(Tests)).GetManifestResourceStream(res);
            System.IO.StreamReader streamReader = new System.IO.StreamReader(sourceStream);
            var source = streamReader.ReadToEnd();
            return source;
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

            "Tests.Resources.ExternGeneric.cs/Test.Program/Test", // 23
            "Tests.Resources.ExternGeneric.cs/Test.Program/Test1", // 24

            "Tests.Resources.GenericMethods.cs/Test.Program/Test", // 25

            "Tests.Resources.GenericsWhere.cs/Test.Program/Test", // 26

            "Tests.Resources.Interfaces.cs/Test.Program/Test1", // 27
            "Tests.Resources.Interfaces.cs/Test.Program/Test2", // 28
            "Tests.Resources.Interfaces.cs/Test.Program/Test3", // 29

            "Tests.Resources.Inheritance.cs/Test.Program/Test1", //30
            "Tests.Resources.Inheritance.cs/Test.Program/Test2", // 31
            "Tests.Resources.Inheritance.cs/Test.Program/Test3", // 32

            "Tests.Resources.GenericMethod2.cs/Test.Program/Test0", // 33
            "Tests.Resources.GenericMethod2.cs/Test.Program/Test1", // 34

            "Tests.Resources.ArrayList.cs/Test.Program/Test0", // 35
            "Tests.Resources.NestedGenerics.cs/Test.Program/Test0", // 36
            "Tests.Resources.ArrayList.cs/Test.Program/Test1", // 37
            "Tests.Resources.ArrayList.cs/Test.Program/Test2", // 38
            "Tests.Resources.ArrayList.cs/Test.Program/Test3", // 39
            "Tests.Resources.ArrayList.cs/Test.Program/Test4", // 40
            "Tests.Resources.ArrayList.cs/Test.Program/Test5", // 41
            "Tests.Resources.ArrayList.cs/Test.Program/Test6", // 42
            "Tests.Resources.ArrayList.cs/Test.Program/Test7", // 43
            "Tests.Resources.ArrayList.cs/Test.Program/Test8", // 44
            "Tests.Resources.ArrayList.cs/Test.Program/Test9", // 45
            "Tests.Resources.ArrayList.cs/Test.Program/Test10", // 46
            "Tests.Resources.ArrayList.cs/Test.Program/Test11", // 47

            "Tests.Resources.Delegates.cs/Test.Program/Test0", // 48
            "Tests.Resources.Delegates.cs/Test.Program/Test1", // 49
            "Tests.Resources.Delegates.cs/Test.Program/Test2", // 50

            "Tests.Resources.Switch.cs/Test.Program/Test", // 51
            "Tests.Resources.Switch.cs/Test.Program/Test", // 52
            "Tests.Resources.Switch.cs/Test.Program/Test", // 53

            "Tests.Resources.Struct.cs/Test.Program/Test", // 54

            "Tests.Resources.InOutParameters2.cs/Test.Program/Test1", // 55
            "Tests.Resources.InOutParameters2.cs/Test.Program/Test2", // 56
            "Tests.Resources.InOutParameters2.cs/Test.Program/Test3", // 57

            "Tests.Resources.Arrays2.cs/Test.Program/Test", // 58
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
            null, // 22
            null, //23
            null, // 24
            null, // 25
            null, // 26
            10, // 27
            10, // 28
            null, // 29
            10, // 30
            10, // 31
            null, // 32
            null, // 33
            null, // 34
            null, // 35
            null, // 36
            null, // 37
            null, // 38
            null, // 39
            null, // 40
            null, // 41
            null, // 42
            null, // 43
            null, // 44
            null, // 45
            null, // 46
            null, // 47
            null, // 48
            null, // 49
            null, // 50
            1,    // 51
            2,    // 52
            3,    // 53
            null,  // 54
            null,  // 55
            null,  // 56
            null,  // 57
            null,  // 58
        };

        private void TestReturnValue(string testSeed, object parameters, ProviderType providerType, bool tac)
        {
            char[] s = { '/' };
            var resourceToTest = testSeed.Split(s);

            string sourceCodeResource = resourceToTest[0];
            string type = resourceToTest[1];
            string method = resourceToTest[2];

            var source = GetResourceAsString(sourceCodeResource);
            TestHandler testHandler = new TestHandler();
            object[] param = parameters == null ? null : new object[1] { parameters };

            var r = testHandler.Test(source, type, method, param, tac, providerType);
            var expectedResult = testHandler.RunOriginalCode(source, type, method, param);

            Assert.AreEqual(expectedResult, r);
        }

        [Test, Sequential]
        public void TestReturnValueCCINoTac(
            [ValueSource("TestReturnValueSeeds")] string testSeed,
            [ValueSource("TestReturnValueParameters")] object parameters)
        {
            TestReturnValue(testSeed, parameters, ProviderType.CCI, false);
        }

        [Test, Sequential, Ignore("metadata provider is failing for too many test cases")]
        public void TestReturnValueMetadataProviderNoTac(
        [ValueSource("TestReturnValueSeeds")] string testSeed,
        [ValueSource("TestReturnValueParameters")] object parameters)
        {
            if (IgnoreInMetadataProvider(testSeed))
                return;

            TestReturnValue(testSeed, parameters, ProviderType.METADATA, false);
        }

        [Test, Sequential]
        public void TestCecilProvider(
        [ValueSource("TestReturnValueSeeds")] string testSeed,
        [ValueSource("TestReturnValueParameters")] object parameters)
        {
            TestReturnValue(testSeed, parameters, ProviderType.CECIL, false);
        }

        [Test]
        public void TestCompileDSAWithCecilProvider()
        {
            Model.Host host = new Model.Host();
            Model.ILoader provider = new CecilProvider.Loader(host);
            var buildDir =
                Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(CecilProvider.Loader))
                    .Location);

            // create temporary directory where we place generated dlls
            var tempDir = Utils.GetTemporaryDirectory();

            // sanity check for needed libraries
            // resourceDSALibrary is the library that is going to be processed
            // resourceDSANUnit has test cases for the DSA library
            var resourceDSALibrary = Path.Combine(buildDir, "Resources/DSA/Library/DSA.dll");
            var resourceDSANUnit = Path.Combine(buildDir, "Resources/DSA/NUnit/DSANUnitTests.dll");
            if (!File.Exists(resourceDSALibrary) || !File.Exists((resourceDSANUnit)))
                throw new FileNotFoundException();

            // read the DSA library and re compile it using our framework
            provider.LoadAssembly(resourceDSALibrary);
            CodeGenerator.CecilCodeGenerator.CecilCodeGenerator exporter = new CodeGenerator.CecilCodeGenerator.CecilCodeGenerator(host);
            exporter.WriteAssemblies(tempDir);

            // copy nunit test library to temp dir
            var dsaNUnit = Path.Combine(tempDir, "DSANUnitTests.dll");
            File.Copy(resourceDSANUnit, dsaNUnit);

            // execute nunit test suite
            var autoRun = new NUnitLite.AutoRun(System.Reflection.Assembly.LoadFrom(dsaNUnit));
            var outputTxt = Path.Combine(tempDir, "output.txt");
            var outputCmd = "--out=" + outputTxt ;
            autoRun.Execute(new string[1] { outputCmd });

            // check results
            var output = File.ReadAllText(outputTxt);
            Assert.IsTrue(output.Contains("Test Count: 618, Passed: 618"));
        }

        private bool IgnoreInMetadataProvider(string testSeed)
        {
            HashSet<string> ignore = new HashSet<string>()
            {
                TestReturnValueSeeds[21],
                TestReturnValueSeeds[22],
                TestReturnValueSeeds[23],
                TestReturnValueSeeds[24],
                TestReturnValueSeeds[25],
                TestReturnValueSeeds[26],
                TestReturnValueSeeds[33],
                TestReturnValueSeeds[34],
                TestReturnValueSeeds[35],
                TestReturnValueSeeds[36],
            };

            if (ignore.Contains(testSeed))
                return true;

            return false;
        }
    }
}
