using System.Collections.Generic;
using System.Linq;
using System.Net;
using Java.Net;

namespace OkHttpClient
{
    public class NativeCookieHandler
    {
        readonly CookieManager _cookieManager = new CookieManager();

        public NativeCookieHandler()
        {
            CookieHandler.Default = _cookieManager; //set cookie manager if using NativeCookieHandler
        }

        public void SetCookies(IEnumerable<Cookie> cookies)
        {
            foreach (var nc in cookies.Select(ToNativeCookie))
            {
                _cookieManager.CookieStore.Add(new URI(nc.Domain), nc);
            }
        }

        public List<Cookie> Cookies => _cookieManager.CookieStore
                                                     .Cookies
                                                     .Select(ToNetCookie)
                                                     .ToList();


        static HttpCookie ToNativeCookie(Cookie cookie)
        {
            var nc = new HttpCookie(cookie.Name, cookie.Value);
            nc.Domain = cookie.Domain;
            nc.Path = cookie.Path;
            nc.Secure = cookie.Secure;

            return nc;
        }

        static Cookie ToNetCookie(HttpCookie cookie)
        {
            var nc = new Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain);
            nc.Secure = cookie.Secure;

            return nc;
        }
    }
}
