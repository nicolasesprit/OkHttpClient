using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Square.OkHttp3;
using Java.IO;

namespace OkHttpClient
{
    public class OkHttpClientHandler : HttpClientHandler
    {
        readonly Square.OkHttp3.OkHttpClient _client = new Square.OkHttp3.OkHttpClient();
        readonly CacheControl _noCacheCacheControl;
        readonly bool _throwOnCaptiveNetwork;

        readonly Dictionary<HttpRequestMessage, WeakReference> _registeredProgressCallbacks =
            new Dictionary<HttpRequestMessage, WeakReference>();

        readonly Dictionary<string, string> _headerSeparators =
            new Dictionary<string, string>
            {
                {"User-Agent", " "}
            };

        public bool DisableCaching { get; set; }

        public OkHttpClientHandler() : this(false) { }

        public OkHttpClientHandler(bool throwOnCaptiveNetwork, NativeCookieHandler cookieHandler = null)
        {
            _throwOnCaptiveNetwork = throwOnCaptiveNetwork;
            _noCacheCacheControl = (new CacheControl.Builder()).NoCache().Build();
        }

        public void RegisterForProgress(HttpRequestMessage request, ProgressDelegate callback)
        {
            if (callback == null && _registeredProgressCallbacks.ContainsKey(request))
            {
                _registeredProgressCallbacks.Remove(request);
                return;
            }

            _registeredProgressCallbacks[request] = new WeakReference(callback);
        }

        ProgressDelegate GetAndRemoveCallbackFromRegister(HttpRequestMessage request)
        {
            ProgressDelegate emptyDelegate = delegate { };

            lock (_registeredProgressCallbacks)
            {
                if (!_registeredProgressCallbacks.ContainsKey(request))
                {
                    return emptyDelegate;
                }

                var weakRef = _registeredProgressCallbacks[request];
                if (weakRef == null)
                {
                    return emptyDelegate;
                }

                var callback = weakRef.Target as ProgressDelegate;
                if (callback == null)
                {
                    return emptyDelegate;
                }

                _registeredProgressCallbacks.Remove(request);
                return callback;
            }
        }

        string GetHeaderSeparator(string name)
        {
            if (_headerSeparators.ContainsKey(name))
            {
                return _headerSeparators[name];
            }

            return ",";
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var java_uri = request.RequestUri.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
            var url = new Java.Net.URL(java_uri);

            var body = default(RequestBody);
            if (request.Content != null)
            {
                var bytes = await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                var contentType = "text/plain";
                if (request.Content.Headers.ContentType != null)
                {
                    contentType = string.Join(" ", request.Content.Headers.GetValues("Content-Type"));
                }

                body = RequestBody.Create(MediaType.Parse(contentType), bytes);
            }

            var builder = new Request.Builder()
                .Method(request.Method.Method.ToUpperInvariant(), body)
                .Url(url);

            if (DisableCaching)
            {
                builder.CacheControl(_noCacheCacheControl);
            }

            var keyValuePairs = request.Headers
                .Union(request.Content != null ?
                    request.Content.Headers :
                    Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>());

            foreach (var kvp in keyValuePairs)
            {
                builder.AddHeader(kvp.Key, string.Join(GetHeaderSeparator(kvp.Key), kvp.Value));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var rq = builder.Build();
            var call = _client.NewCall(rq);

            // NB: Even closing a socket must be done off the UI thread. Cray!
            cancellationToken.Register(() => Task.Run(() => call.Cancel()));

            var resp = default(Response);
            try
            {
                resp = await call.EnqueueAsync().ConfigureAwait(false);
                var newReq = resp.Request();
                var newUri = newReq == null ? null : newReq.Url().Uri();
                request.RequestUri = new Uri(newUri.ToString());

                if (_throwOnCaptiveNetwork && newUri != null)
                {
                    if (url.Host != newUri.Host)
                    {
                        throw new CaptiveNetworkException(new Uri(java_uri), new Uri(newUri.ToString()));
                    }
                }
            }
            catch (IOException ex)
            {
                if (ex.Message != null && ex.Message.ToLowerInvariant().Contains("canceled"))
                {
                    throw new OperationCanceledException();
                }

                throw;
            }

            var respBody = resp.Body();

            cancellationToken.ThrowIfCancellationRequested();

            var ret = new HttpResponseMessage((HttpStatusCode)resp.Code());
            ret.RequestMessage = request;
            ret.ReasonPhrase = resp.Message();

            if (respBody != null)
            {
                var content = new ProgressStreamContent(respBody.ByteStream(), CancellationToken.None);
                content.Progress = GetAndRemoveCallbackFromRegister(request);
                ret.Content = content;
            }
            else
            {
                ret.Content = new ByteArrayContent(new byte[0]);
            }

            var respHeaders = resp.Headers();
            foreach (var k in respHeaders.Names())
            {
                ret.Headers.TryAddWithoutValidation(k, respHeaders.Get(k));
                ret.Content.Headers.TryAddWithoutValidation(k, respHeaders.Get(k));
            }

            return ret;
        }
    }
}