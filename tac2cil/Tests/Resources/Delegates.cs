using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    class Program
    {
        public static int Test0()
        {
            Test t = new Test();
            DelegateInt delegateInt = t.Identity;
            return delegateInt(10);
        }

        public static int Test1()
        {
            Func<int, int> f = num => num * 5;

            return f(10);
        }
    }

    //public delegate TWeight AddWeights<TWeight>(TWeight a, TWeight b)
    //where TWeight : IComparable<TWeight>;

    public delegate int DelegateInt(int i);

    class Test
    {
        public int Identity(int i)
        {
            return i;
        }
    }
}
