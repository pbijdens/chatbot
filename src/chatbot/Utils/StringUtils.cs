using System.Collections.Generic;

namespace chatbot.Utils
{
    public static class StringUtils
    {
        public static IEnumerable<string> SplitArgs(string argstr)
        {
            List<string> result = new List<string>();

            string token = "";
            bool isOpen = false;

            argstr = argstr?.Trim() ?? string.Empty;
            for (int index = 0; index < argstr.Length; index++)
            {
                char c = argstr[index];
                if (c == '"') // if it's a quote, toggle isopen to either start or stop the mode in which we consume anything
                {
                    isOpen = !isOpen;
                }
                else if (c == '\\') // if escaped just consume the next char without processing it
                {
                    if (index + 1 < argstr.Length) token += argstr[index + 1];
                    index++; // skip
                }
                else if (!isOpen && char.IsWhiteSpace(c)) // whitespace outside quoted expression
                {
                    // ignore
                    if (!string.IsNullOrWhiteSpace(token)) result.Add(token);
                    token = "";
                }
                else // inside quoted expression, or not whitespace
                {
                    token += c;
                }
            }
            if (!string.IsNullOrWhiteSpace(token)) result.Add(token);
            return result;
        }
    }
}
