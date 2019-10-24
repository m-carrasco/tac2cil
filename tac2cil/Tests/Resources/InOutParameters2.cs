using System;

namespace Test
{
    public class Program
    {
        public static int Test1()
        {
            int a = 10;
            return C.Id(in a);
        }

        public static int Test2()
        {
            int a = 10;
            C.SetOne(out a);
            return a;
        }

        public static int Test3()
        {
            int a = 10;
            C.SetTwo(ref a);
            return a;
        }
    }

    public class C
    {
        public static int Id(in int a)
        {
            return a;
        }

        public static void SetOne(out int a)
        {
            a = 1;
        }

        public static void SetTwo(ref int a)
        {
            a = 2;
        }
    }
}
