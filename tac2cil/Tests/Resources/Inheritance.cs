using System;
using System.Collections.Generic;

namespace Test
{
    public class Program
    {
        public static int Test1(int i)
        {
            var a = new AddOne();
            return Program.Process(a, i);
        }

        public static int Test2(int i)
        {
            var a = new SubOne();
            return Program.Process(a, i);
        }

        public static int Test3()
        {
            var l = new ListWrapper();
            l.Add(1);
            l.Add(1);
            return l.Count;
        }

        public static int Process(Operation processable, int i)
        {
            return processable.Op(i);
        }
    }

    public abstract class Operation
    {
        public abstract int Op(int i);
    }

    class AddOne : Operation
    {
        public override int Op(int i)
        {
            return i + 1;
        }
    }

    class SubOne : Operation
    {
        public override int Op(int i)
        {
            return i - 1;
        }
    }

    class ListWrapper : List<int>
    {

    }
}
