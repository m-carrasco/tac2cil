using System;
using System.Collections.Generic;

namespace Test
{
    public class Program
    {
        public static int Test()
        {
            C<int, int> c = new C<int, int>();
            var v = c.M(10);

            return v.Length;
        }

    }

    public class C<T, Q>
    {
        public KeyValuePair<T, Q>[] M(int Count)
        {
            KeyValuePair<T, Q>[] newArray = new KeyValuePair<T, Q>[Count];
            return newArray;
        }
    }
}
