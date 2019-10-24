using System;

namespace Test
{
    public struct A
    {
        public int F;
    }

    public class Program
    {
        public static int Test()
        {
            A a = new A();
            a.F = 10;
            Set(a);
            return a.F;
        }

        public static void Set(A a)
        {
            a.F = 1;
        }
    }
}