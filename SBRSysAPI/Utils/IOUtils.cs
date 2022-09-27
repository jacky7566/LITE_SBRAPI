using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using SystemLibrary.Utility;

namespace SBRSysAPI.Utils
{
    public static class IOUtils
    {
        public static void AddRange<T>(this ICollection<T> target, IEnumerable<T> source)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            foreach (var element in source)
                target.Add(element);
        }

        public static HttpResponseMessage LogAndResponse(HttpContent hc, HttpStatusCode hsc = HttpStatusCode.OK, string message = "")
        {
            var resp = new HttpResponseMessage(hsc);
            if (string.IsNullOrEmpty(message) == false)
            {
                LogHelper.WriteLine(message);
                resp.Content = new StringContent(message);
            }
            else
            {
                resp.Content = hc;
            }
            return resp;
        }
    }
}