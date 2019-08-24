using System;

namespace Test
{
    public class Program
    {
        public static int Test()
        {
            int i = 1;
            Foo(ref i);
            return i;
        }

        public static void Foo(ref int a)
        {
            a = 10;
        }

        public static bool TestBool()
        {
            bool i = false;
            FooBool(ref i);
            return i;
        }

        public static void FooBool(ref bool a)
        {
            a = true;
        }

        public static float TestFloat()
        {
            float i = 5.0f;
            FooFloat(ref i);
            return i;
        }

        public static void FooFloat(ref float a)
        {
            a = 10.0f;
        }

        public static Object TestObject()
        {
            Object p = new Program();
            FooObject(ref p);
            return p;
        }

        public static void FooObject(ref Object a)
        {
            a = null;
        }
    }
}