using System;

namespace Test
{
    public class Program
    {
        public static int Test()
        {
            int[] n = new int[10]; /* n is an array of 10 integers */
            int i, j, sum = 0;

            /* initialize elements of array n */
            for (i = 0; i < 10; i++)
            {
                n[i] = i + 100;
            }

            /* output each array element's value */
            for (j = 0; j < 10; j++)
            {
                sum += n[j];
            }

            return sum;
        }
    }
}
