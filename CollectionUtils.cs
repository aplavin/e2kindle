using System;
using System.Collections.Generic;
using System.Linq;

namespace e2Kindle
{
    public static class CollectionUtils
    {

        /// <summary>
        /// Tests whether the enumerable is empty. Equal to !enumerable.Any().
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable"></param>
        /// <returns></returns>
        public static bool Empty<T>(this IEnumerable<T> enumerable)
        {
            return !enumerable.Any();
        }

        /// <summary>
        /// Runs the action for each element in the enumerable.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable"></param>
        /// <param name="action"></param>
        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach (T elem in enumerable)
            {
                action(elem);
            }
        }

        /// <summary>
        /// Adds the IEnumerable source elements to ICollection dest.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dest"></param>
        /// <param name="source"></param>
        public static void AddRange<T>(this ICollection<T> dest, IEnumerable<T> source)
        {
            foreach (T elem in source)
            {
                dest.Add(elem);
            }
        }

        /// <summary>
        /// Bypasses elements in a sequence until a specified condition is true and then returns the remaining elements. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IEnumerable<T> SkipUntil<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            return source.SkipWhile(t => !predicate(t));
        }

        /// <summary>
        /// Bypasses elements in a sequence until a specified condition is true and then returns the remaining elements. 
        /// The element's index is used in the logic of the predicate function.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IEnumerable<T> SkipUntil<T>(this IEnumerable<T> source, Func<T, int, bool> predicate)
        {
            return source.SkipWhile((t, i) => !predicate(t, i));
        }

        /// <summary>
        /// Returns elements from a sequence until a specified condition is true.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IEnumerable<T> TakeUntil<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            return source.TakeWhile(t => !predicate(t));
        }

        /// <summary>
        /// Returns elements from a sequence until a specified condition is true.
        /// The element's index is used in the logic of the predicate function.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IEnumerable<T> TakeUntil<T>(this IEnumerable<T> source, Func<T, int, bool> predicate)
        {
            return source.TakeWhile((t, i) => !predicate(t, i));
        }

        /// <summary>
        /// Filters a sequence of values based on a predicate.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IEnumerable<T> WhereNot<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            return source.Where(t => !predicate(t));
        }

        /// <summary>
        /// Filters a sequence of values based on a predicate.
        /// The element's index is used in the logic of the predicate function.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IEnumerable<T> WhereNot<T>(this IEnumerable<T> source, Func<T, int, bool> predicate)
        {
            return source.Where((t, i) => !predicate(t, i));
        }
    }
}
