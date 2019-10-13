using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    public class Program
    {
        public static Object Test0()
        {
            return D.Foo<bool, int>(new B<bool, int>(), false, new C<int>());
        }

        public static Object Test1()
        {
            return D.Bar<bool>();
        }
    }

    public class A<T, W> { }
    public class B<T, W> { }
    public class C<T> { }

    public class D
    {
        public static A<T, W> Foo<T, W>(B<T, W> x, T y, C<W> z)
            where T : IComparable<T>
            where W : IComparable<W>
        {
            return null;
        }

        public static A<T, W> Fooo<T, W>()
        {
            return null;
        }

        public static Object Bar<W>()
        {
            return D.Fooo<W, int>();
        }
    }

}