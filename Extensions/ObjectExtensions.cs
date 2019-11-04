using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HttpApiClient.Extensions {

    public static class ObjectExtensions {

        public static FormUrlEncodedContent ToFormUrlEncodedContent(this object obj) {
            var formData = obj.ToKeyValue();
            return new FormUrlEncodedContent(formData);
        }

        public static IDictionary<string, string> ToKeyValue(this object obj) {
            if (obj == null) {
                return null;
            }
            var serializer = new JsonSerializer { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
            var token = obj as JToken;
            if (token == null) {
                // If not a JToken, convert and then run through this method again
                // use serializer defined above
                return ToKeyValue(JObject.FromObject(obj, serializer));
            }

            if (token.HasValues) {
                var contentData = new Dictionary<string, string>();
                foreach (var child in token.Children().ToList()) {
                    var childContent = child.ToKeyValue();
                    if (childContent != null) {
                        contentData = contentData.Concat(childContent)
                            .ToDictionary(k => k.Key, v => v.Value);
                    }
                }
                return contentData;
            }

            var jValue = token as JValue;
            if (jValue?.Value == null) {
                return null;
            }

            var value = jValue?.Type == JTokenType.Date ?
                jValue?.ToString("o", CultureInfo.InvariantCulture) :
                jValue?.ToString(CultureInfo.InvariantCulture);

            return new Dictionary<string, string> { { token.Path, value } };
        }
    }
}