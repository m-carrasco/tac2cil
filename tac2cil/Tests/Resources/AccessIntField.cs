using System;
namespace Test
{
    public class Program
    {
        public static int Test()
        {
            A a = new A(5);
            return a.i;
        }
    }

    public class A
    {
        public int i = 0;

        public A(int _i)
        {
            i = _i;
        }
    }
}