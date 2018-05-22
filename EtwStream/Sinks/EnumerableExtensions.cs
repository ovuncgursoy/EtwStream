#region Using Statements

using System;
using System.Collections.Generic;

#endregion

// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace EtwStream
{
    internal static class EnumerableExtensions
    {
        public static void FastForEach<T>(this IList<T> source, Action<T> action)
        {
            if (source is List<T> l)
            {
                l.ForEach(action);

                return;
            }

            if (source is T[] a)
            {
                foreach (var t in a)
                {
                    action(t);
                }

                return;
            }

            foreach (var item in source)
            {
                action(item);
            }
        }
    }
}
