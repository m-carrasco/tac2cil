using System;

namespace Test
{
    public class Program
    {
        public static int TestIn()
        {
            int i = 1;
            return FooIn(in i);
        }

        public static int FooIn(in int a)
        {
            return a;
        }

        public static int TestOut()
        {
            int i = 0;
            FooOut(out i);
            return i;
        }

        public static void FooOut(out int a)
        {
            a = 100;
        }
    }
}
