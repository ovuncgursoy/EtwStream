#region Using Statements

using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

#endregion

// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace EtwStream
{
    public static class SinkExtensions
    {
        public static IDisposable LogTo<T>(this IObservable<T> source, Func<IObservable<T>, IDisposable[]> subscribe)
        {
            var publishedSource = source.Publish().RefCount();

            var subscriptions = subscribe(publishedSource);

            return new CompositeDisposable(subscriptions);
        }
    }
}
