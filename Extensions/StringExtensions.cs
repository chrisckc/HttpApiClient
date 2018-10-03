namespace HttpApiClient.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Truncates the string to a specified length and suffixes the truncated string with "..."
        /// </summary>
        /// <param name="text">string that will be truncated</param>
        /// <param name="maxLength">length of characters to maintain before truncation</param>
        /// <returns>truncated string</returns>
        public static string Truncate(this string text, int maxLength)
        {
            // suffixes the truncated string with "..."
            const string suffix = "...";
            string truncatedString = text;
    
            if (maxLength <= 0) return truncatedString;
            int strLength = maxLength - suffix.Length;
    
            if (strLength <= 0) return truncatedString;
    
            if (text == null || text.Length <= maxLength) return truncatedString;
    
            truncatedString = text.Substring(0, strLength);
            truncatedString = truncatedString.TrimEnd();
            truncatedString += suffix;
            return truncatedString;
        }
    }
}