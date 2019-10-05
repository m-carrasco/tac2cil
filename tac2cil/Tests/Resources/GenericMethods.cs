using System;
using System.Collections.Generic;

namespace Test
{
    public class Program
    {
        public static int Test()
        {
            return A.Foo<bool>();
        }

        public static int Test2()
        {
            return A.Identity<int>(5);
        }
    }

    public class A
    {
        public static int Foo<T>()
        {
            List<T> l = new List<T>();
            return l.Count;
        }

        public static T Identity<T>(T t) { return t; }
    }
}