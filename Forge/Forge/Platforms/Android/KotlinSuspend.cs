using Android.Runtime;

using Kotlin.Coroutines;
using Kotlin.Coroutines.Intrinsics;

namespace Forge.Platforms.Android
{
    /// <summary>
    /// Awaits a Kotlin <c>suspend</c> function from C#. Bound suspend functions take a trailing
    /// <see cref="IContinuation"/> and either return their value straight away or return
    /// COROUTINE_SUSPENDED and later call <see cref="IContinuation.ResumeWith"/> with a Kotlin
    /// <c>Result</c> (the value itself, or a <c>Result$Failure</c> wrapping the exception).
    /// </summary>
    internal static class KotlinSuspend
    {
        public static Task<Java.Lang.Object?> CallAsync(Func<IContinuation, Java.Lang.Object?> call)
        {
            var tcs = new TaskCompletionSource<Java.Lang.Object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var continuation = new Continuation(tcs);
            try
            {
                var immediate = call(continuation);
                if (immediate is null || !immediate.Equals(IntrinsicsKt.COROUTINE_SUSPENDED))
                    Complete(tcs, immediate);
            }
            catch (Exception ex) { tcs.TrySetException(ex); }
            return tcs.Task;
        }

        private static void Complete(TaskCompletionSource<Java.Lang.Object?> tcs, Java.Lang.Object? result)
        {
            if (result?.Class.Name == "kotlin.Result$Failure")
            {
                var field = result.Class.GetDeclaredField("exception");
                field.Accessible = true;
                var cause = field.Get(result)?.JavaCast<Java.Lang.Throwable>();
                tcs.TrySetException(cause is null ? new InvalidOperationException("Kotlin call failed") : new Java.Lang.Exception(cause.Message, cause));
                return;
            }
            tcs.TrySetResult(result);
        }

        private sealed class Continuation : Java.Lang.Object, IContinuation
        {
            private readonly TaskCompletionSource<Java.Lang.Object?> _tcs;
            public Continuation(TaskCompletionSource<Java.Lang.Object?> tcs) => _tcs = tcs;

            public ICoroutineContext Context => EmptyCoroutineContext.Instance;

            public void ResumeWith(Java.Lang.Object result) => Complete(_tcs, result);
        }
    }
}
