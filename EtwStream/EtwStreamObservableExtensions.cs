#region Using Statements

using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

#endregion

// ReSharper disable UnusedMember.Global
namespace EtwStream
{
    public static class EtwStreamObservableExtensions
    {
        public static IObservable<T> TakeUntil<T>(this IObservable<T> source, CancellationToken terminateToken)
        {
            var subject = new Subject<Unit>();

            terminateToken.Register(s =>
            {
                if (!(s is Subject<Unit> ss))
                {
                    return;
                }

                ss.OnNext(Unit.Default);

                ss.OnCompleted();
            }, subject);

            return source.TakeUntil(subject);
        }

        public static IObservable<IList<T>> Buffer<T>(this IObservable<T> source, TimeSpan timeSpan, int count, CancellationToken terminateToken)
            => source.TakeUntil(terminateToken).Buffer(timeSpan, count);
    }
}
