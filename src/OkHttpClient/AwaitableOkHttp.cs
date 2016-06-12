using System.Threading.Tasks;
using System.Net;
using Square.OkHttp3;

namespace OkHttpClient
{

    public static class AwaitableOkHttp
    {
        public static Task<Response> EnqueueAsync(this ICall call)
        {
            var cb = new OkTaskCallback();
            call.Enqueue(cb);

            return cb.Task;
        }

        class OkTaskCallback : Java.Lang.Object, ICallback
        {
            readonly TaskCompletionSource<Response> _tcs = new TaskCompletionSource<Response>();
            public Task<Response> Task => _tcs.Task;

            public void OnFailure(ICall call, Java.IO.IOException ioException)
            {
                // Kind of a hack, but the simplest way to find out that server cert. validation failed

                var host = call?.Request()?.Url()?.Uri()?.Host;

                if (host != null && ioException.Message == $"Hostname '{host}' was not verified")
                {
                    _tcs.TrySetException(new WebException(ioException.LocalizedMessage, WebExceptionStatus.TrustFailure));
                }
                else
                {
                    _tcs.TrySetException(ioException);
                }
            }

            public void OnResponse(ICall call, Response response)
            {
                _tcs.TrySetResult(response);
            }
        }
    }
}
