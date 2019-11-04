using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HttpApiClient.Extensions {
    public static class JTokenExtensions {

        public static string SelectStringValue(this JToken obj, string key, string defaultSubstitute = null) {
            if (obj == null) return defaultSubstitute;
            try {
                JToken token = obj.SelectToken(key);
                if (token != null) {
                    if (token.Type ==  JTokenType.String) {
                        return token.ToObject<string>();
                    } else {
                        // JToken.ToString() Returns the indented JSON for this token
                        return token.ToString();
                    }
                }
                return defaultSubstitute;
            } catch(Exception ex) {
                string errorMessage = $"SelectStringValue Error:{ex.Message}";
                Console.WriteLine(errorMessage);
                return errorMessage;
            }
        }
    }
}