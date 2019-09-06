using System;
namespace Test
{
    public class Program
    {
        public static int Test()
        {
            A<int> a = new A<int>(0);
            a.Set(1);
            return a.Get();
        }

        public static int Test1()
        {
            A<int> a = new A<int>(0);
            a.i = 5;
            return a.Get();
        }
    }

    public class A<T>
    {
        public T i;

        public A(T _i)
        {
            i = _i;
        }

        public void Set(T _i)
        {
            i = _i;
        }

        public T Get()
        {
            return i;
        }
    }

    public class C<W, X, Y>
    {
        public void M()
        {
        }
    }

    public class D<T>
    {
        public void M()
        {
            var c = new C<int, T, bool>();
        }
    }

    public class E<T0, T1, T2>
    {
        public void M()
        {
            var c = new C<T0, T1, T2>();
        }
    }
}