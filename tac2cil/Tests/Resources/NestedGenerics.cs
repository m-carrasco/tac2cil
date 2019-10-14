using System;
using System.Collections;
using System.Collections.Generic;

namespace Test
{
    public class Program
    {
        public static int Test0()
        {
            A<int> a = new A<int>(5);
            int r = 0;
            foreach (var i in a)
            {
                r = i;
            }

            return r;
        }
    }

    class A<T> : IEnumerable<T>
    {
        private T t;

        public A(T t)
        {
            this.t = t;
        }
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            yield return t;
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            yield return t;
        }
    }
}