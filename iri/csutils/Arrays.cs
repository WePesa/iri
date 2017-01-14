namespace iri.utils
{
    using System;
    using System.Linq;

    public class Arrays
    {
        public static int[] copyOf(int[] original, int newLength)
        {
            int[] copy = new int[newLength];

            Array.Copy(original, 0, copy, 0,
                             Math.Min(original.Length, newLength));
            return copy;
        }

        public static sbyte[] copyOfRange(sbyte[] original, int from, int to)
        {
            int newLength = to - from;

            if (newLength < 0)
                throw new ArgumentException(from + " > " + to);

            sbyte[] copy = new sbyte[newLength];
            Array.Copy(original, from, copy, 0, Math.Min(original.Length - from, newLength));
            return copy;
        }

        // http://grepcode.com/file/repository.grepcode.com/java/root/jdk/openjdk/8u40-b25/java/util/Arrays.java#Arrays.copyOfRange%28int%5B%5D%2Cint%2Cint%29
        public static int[] copyOfRange(int[] original, int from, int to)
        {
            int newLength = to - from;
            if (newLength < 0)
                throw new ArgumentException(from + " > " + to);
            int[] copy = new int[newLength];
            Array.Copy(original, from, copy, 0,
                             Math.Min(original.Length - from, newLength));
            return copy;
        }

        public static void fill(short[] a, int fromIndex, int toIndex, short val)
        {
            rangeCheck(a.Length, fromIndex, toIndex);
            for (int i = fromIndex; i < toIndex; i++)
                a[i] = val;
        }

        private static void rangeCheck(int arrayLength, int fromIndex, int toIndex)
        {
            if (fromIndex > toIndex)
            {
                throw new ArgumentException(
                        "fromIndex(" + fromIndex + ") > toIndex(" + toIndex + ")");
            }

            if (fromIndex < 0)
            {
                throw new ArgumentOutOfRangeException(fromIndex.ToString());
            }

            if (toIndex > arrayLength)
            {
                throw new ArgumentOutOfRangeException(toIndex.ToString());
            }
        }

        public static bool equals(sbyte[] a1, sbyte[] a2)
        {
            // Quick test which saves comparing elements of the same array, and also
            // catches the case that both are null.
            if (a1 == a2)
                return true;

            if (null == a1 || null == a2)
                return false;

            // If they're the same length, test each element
            if (a1.Length == a2.Length)
            {
                int i = a1.Length;
                while (--i >= 0)
                    if (a1[i] != a2[i])
                        return false;
                return true;
            }
            return false;
        }

        public static bool equals(int[] a, int[] b)
        {
            // both null considered by Arrays.equals equal in Java.
            // Enumerable.SequenceEqual throws System.ArgumentNullException incase any of them null
            // so we add the following checks

            if (a == null && b == null)
                return true;

            if (a == null || b == null)
                return false;

            return Enumerable.SequenceEqual(a, b);
        }

        public static int hashCode(int[] v)
        {
            if (v == null)
                return 0;

            int result = 1;

            for (int i = 0; i < v.Length; ++i)
                result = 31 * result + v[i];

            return result;
        }

        public static int hashCode(sbyte[] v)
        {
            if (v == null)
                return 0;

            int result = 1;

            for (int i = 0; i < v.Length; ++i)
                result = 31 * result + v[i];

            return result;
        }

        public static int compareTo(sbyte[] left, sbyte[] right)
        {
            for (int i = 0, j = 0; i < left.Length && j < right.Length; i++, j++)
            {
                int a = (left[i] & 0xff);
                int b = (right[j] & 0xff);
                if (a != b)
                {
                    return a - b;
                }
            }
            return left.Length - right.Length;
        }
    }
}