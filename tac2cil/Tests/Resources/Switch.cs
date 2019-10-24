using System;

namespace Test
{
    public class Program
    {
        public static int Test(int i)
        {
            int result;
            switch (i)
            {
                case 1:
                    result = 1;
                    break;
                case 2:
                    result = 2;
                    break;
                default:
                    result = 3;
                    break;
            }

            return result;
        }
    }
}