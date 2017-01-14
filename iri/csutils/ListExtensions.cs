namespace iri.utils
{
    using System.Collections.Generic;
    using System.Linq;

    public static class ListExtensions
    {
        // Solves the error “The type T must be a reference type in order to use it as..."
        public static long? Poll(this IList<long?> list) 
        {
            lock (list)
            {
                if (list.Count == 0)
                    return null;

                long? value = list[0];
                list.RemoveAt(0);
                return value;
            }
        }

        public static T poll<T>(this IList<T> list) where T : class
        {
            lock (list)
            {
                if (list.Count == 0)
                    return null;

                T value = list[0];
                list.RemoveAt(0);
                return value;
            }
        }

        public static IList<T> SingletonList<T>(this IList<T> iList, T item)
        {
            return Enumerable.Range(0, 1).Select(i => item).ToList().AsReadOnly();

            // or
            // var Result = new List<T>();
            // Result.Add(item);
            // return Result.AsReadOnly();
        }
    }
}
