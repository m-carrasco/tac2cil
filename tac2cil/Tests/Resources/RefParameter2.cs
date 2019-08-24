using System;

namespace Test
{
    public class Program
    {
        public static int TestInt()
        {
            int i = 1;
            return FooInt(ref i);
        }

        public static int FooInt(ref int j)
        {
            return j;
        }

        public static bool TestBool()
        {
            bool i = true;
            return FooBool(ref i);
        }

        public static bool FooBool(ref bool j)
        {
            return j;
        }

        public static float TestFloat()
        {
            float i = 5.0f;
            return FooFloat(ref i);
        }

        public static float FooFloat(ref float a)
        {
            return a;
        }

        public static Object TestObject()
        {
            Object p = new Program();
            return FooObject(ref p);
        }

        public static Object FooObject(ref Object a)
        {
            a = null;
            return a;
        }
    }
}