using System;
using System.Collections.Generic;
using System.Linq;

namespace IntelOrca.Biohazard.BioRand.Extensions
{
    public static class CollectionExtensions
    {
        public static IEnumerable<T> Choose<T>(this IEnumerable<T?> source)
        {
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
            return source.Where(x => x is not null);
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
        }

        public static IEnumerable<TResult> Choose<T, TResult>(this IEnumerable<T> source, Func<T, TResult?> selector)
        {
            return source.Select(selector).Choose();
        }

        public static int FindIndex<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            int index = 0;
            foreach (var item in source)
            {
                if (predicate(item))
                {
                    return index;
                }
                index++;
            }
            return -1;
        }

        public static T? GetItem<T>(this List<T> list, int index)
        {
            return list.Count <= index ? default : list[index];
        }

        public static void SetItem<T>(this List<T> list, int index, T value)
        {
            if (list.Count <= index)
                list.Resize(index + 1);
            list[index] = value;
        }

        public static void Resize<T>(this List<T> list, int count)
        {
            if (list.Count > count)
                list.RemoveRange(count, list.Count - count);
            while (list.Count < count)
                list.Add(default!);
        }

        public static void Pop<T>(this List<T> list)
        {
            list.RemoveAt(list.Count - 1);
        }

        public static Queue<T> ToQueue<T>(this IEnumerable<T> collection)
        {
            return new Queue<T>(collection);
        }

        public static T[] Shuffle<T>(this IEnumerable<T> items, Rng rng)
        {
            var array = items.ToArray();
            for (int i = 0; i < array.Length - 1; i++)
            {
                var ri = rng.Next(i, array.Length);
                var tmp = array[ri];
                array[ri] = array[i];
                array[i] = tmp;
            }
            return array;
        }

        public static T TakeRandom<T>(this IEnumerable<T> items, Rng rng)
        {
            return rng.Next(items);
        }

        public static T[] TakeRandom<T>(this IEnumerable<T> items, Rng rng, int count)
        {
            return items.Shuffle(rng).Take(count).ToArray();
        }
    }
}
