using System;

namespace Test
{
    public class Program
    {
        public static void Test()
        {
            A a = new A(5);
            A(a);
        }

        public static void A(A a)
        {
            a.print();
        }
    }

    public class A
    {
        int i = 0;

        public A(int _i)
        {
            i = _i;
        }

        public void print()
        {
            Console.WriteLine(i);
        }
    }
}