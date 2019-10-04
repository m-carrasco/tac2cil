using System;
using System.Collections.Generic;

namespace Test
{
    public class Program
    {
        public static int Test()
        {
            var l = new List<int>();
            var wrapper =
                new ListWrapper<List<int>>(l);

            return wrapper.Count();
        }
    }

    public class ListWrapper<T>
        where T : IList<int>
    {
        T list;

        public ListWrapper(T t)
        {
            list = t;
        }

        public void Add(int e)
        {
            list.Add(e);
        }

        public int Pop()
        {
            var temp = list[0];
            list.Remove(0);
            return temp;
        }

        public int Count()
        {
            return list.Count;
        }
    }
}