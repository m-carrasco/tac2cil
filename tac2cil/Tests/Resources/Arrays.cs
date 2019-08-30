using System;

namespace Test
{
    public class Program
    {
        // 1045
        public static int Test1()
        {
            int[] n = new int[10]; 
            int i, j, sum = 0;

            for (i = 0; i < 10; i++)
            {
                n[i] = i + 100;
            }

            for (j = 0; j < 10; j++)
            {
                sum += n[j];
            }

            return sum;
        }

        // 1045
        public static int Test2()
        {
            int[,] n = new int[2, 5];
            int i, j, sum = 0;

            for (i = 0; i < 10; i++)
            {
                int m = i / 5;
                n[m, i % 5] = i + 100;
            }

            for (j = 0; j < 10; j++)
            {
                int m = j / 5;
                sum += n[m, j % 5];
            }

            return sum;
        }


        // 20
        public static int Test3()
        {
            int[,] n = new int[2, 5];
            ref int e = ref n[1, 4];

            e = 10;
            n[1, 4] += e;

            return e;
        }

        // 20
        public static int Test4()
        {
            int[] n = new int[2];
            ref int e = ref n[1];

            e = 10;
            n[1] += e;

            return e;
        }

    }
}
