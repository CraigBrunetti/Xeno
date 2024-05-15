using System;
using System.Collections.Generic;
using System.Linq;

namespace Xeno
{
    public static class Extensions
    {
        public static void ForEach<T>(this IEnumerable<T> items, Action<T> action)
        {
            var exceptions = new List<Exception>();

            foreach (var item in items)
            {
                try
                {
                    action(item);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            if (exceptions.Any())
            {
                throw new AggregateException(exceptions);
            }
        }


        public static List<Tout> ToList<T, Tout>(this IEnumerable<T> items, Func<T, Tout> converter)
        {
            return items.Select(converter).ToList();
        }


        public static bool TryFirstOrDefault<T>(this IEnumerable<T> items, Func<T, bool> predicate, out T result)
        {
            try
            {
                var match = items.FirstOrDefault(predicate);

                if (null != match)
                {
                    result = match;
                    return true;
                }
            }
            catch { }

            result = default;
            return false;
        }


        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
        {
            items.ForEach(collection.Add);
        }


        public static IEnumerable<T> Append<T>(this IEnumerable<T> collection, IEnumerable<T> items)
        {
            foreach (var item in collection)
            {
                yield return item;
            }

            foreach (var item in items)
            {
                yield return item;
            }
        }
    }
}
