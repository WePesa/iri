namespace iri.utils
{
    using System;
    using System.Numerics;

    public class ArithmeticHelper
    {
        public static long longValueExact(BigInteger value)
        {
            if (value > long.MaxValue || value < long.MinValue)
                throw new ArithmeticException();

            return (long)value;
        }

        public static int intValueExact(BigInteger value)
        {
            if (value > int.MaxValue || value < int.MinValue)
                throw new ArithmeticException();

            return (int)value;
        }
    }
}