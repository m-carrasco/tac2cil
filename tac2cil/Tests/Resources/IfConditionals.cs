using System;

namespace Test
{
    public class Program
    {
        public static int Test(int i)
        {
            if (i > 0)
            {
                if (i < 100)
                {
                    return 50;
                }
                else
                {
                    return 200;
                }
            }
            else
            {
                return -1;
            }
        }
    }
}
