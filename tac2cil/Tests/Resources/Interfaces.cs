using System;

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
            var s = new SubOne();
            return Program.Process(s, i);
        }

        public static int Test3()
        {
            var wp5 = new WrapperInt(5);
            var wp4 = new WrapperInt(4);

            return wp5.CompareTo(wp4);
        }
        
        public static int Process(IProcessable processable, int i)
        {
            return processable.Process(i);
        }
    }

    public interface IProcessable
    {
        int Process(int i);
    }

    class AddOne : IProcessable
    {
        public int Process(int i)
        {
            return i + 1;
        }
    }

    class SubOne : IProcessable
    {
        public int Process(int i)
        {
            return i - 1;
        }
    }

    class WrapperInt : IComparable<WrapperInt>
    {
        public int val;

        public WrapperInt(int _v)
        {
            val = _v;
        }

        public int CompareTo(WrapperInt o)
        {
            if (val < o.val)
                return -1;

            if (val == o.val)
                return 0;

            return 1;
        }
    }
}
