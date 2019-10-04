using System;
using System.Collections.Generic;

namespace Test
{
    public class Program
    {
        public static int Test()
        {
            var l = new List<int>();
            return l.Count;
        }

        public static int Test1()
        {
            var l = new ListWrapper<int>();
            l.Add(1);
            return l.Count();
        }

    }

    public class ListWrapper<T>
    {
        List<T> l;

        public ListWrapper()
        {
            l = new List<T>();
        }

        public void Add(T t)
        {
            l.Add(t);
        }

        public int Count()
        {
            return l.Count;
        }
    }
}