namespace iri.utils
{
    using System;
    using System.Collections.Generic;

    internal class FuncComparer<T> : IComparer<T>
    {
        private readonly Comparison<T> comparison;
        public FuncComparer(Comparison<T> comparison)
        {
            this.comparison = comparison;
        }
        public int Compare(T x, T y)
        {
            return comparison(x, y);
        }
    }
}
