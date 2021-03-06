﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MediaBrowser.Common.Extensions
{
    /// <summary>
    /// Class BaseExtensions
    /// </summary>
    public static class BaseExtensions
    {
        /// <summary>
        /// Tries the add.
        /// </summary>
        /// <typeparam name="TKey">The type of the T key.</typeparam>
        /// <typeparam name="TValue">The type of the T value.</typeparam>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary.ContainsKey(key))
            {
                return false;
            }

            dictionary.Add(key, value);
            return true;
        }

        /// <summary>
        /// Provides an additional overload for string.split
        /// </summary>
        /// <param name="val">The val.</param>
        /// <param name="separator">The separator.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String[][].</returns>
        public static string[] Split(this string val, char separator, StringSplitOptions options)
        {
            return val.Split(new[] { separator }, options);
        }

        /// <summary>
        /// Strips the HTML.
        /// </summary>
        /// <param name="htmlString">The HTML string.</param>
        /// <returns>System.String.</returns>
        public static string StripHtml(this string htmlString)
        {
            // http://stackoverflow.com/questions/1349023/how-can-i-strip-html-from-text-in-net
            const string pattern = @"<(.|\n)*?>";
            return Regex.Replace(htmlString, pattern, string.Empty);
        }
        
        /// <summary>
        /// Shuffles an IEnumerable
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list">The list.</param>
        /// <returns>IEnumerable{``0}.</returns>
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> list)
        {
            return list.OrderBy(x => Guid.NewGuid());
        }

        /// <summary>
        /// Gets the M d5.
        /// </summary>
        /// <param name="str">The STR.</param>
        /// <returns>Guid.</returns>
        public static Guid GetMD5(this string str)
        {
            using (var provider = MD5.Create())
            {
                return new Guid(provider.ComputeHash(Encoding.Unicode.GetBytes(str)));
            }
        }

        /// <summary>
        /// Gets the MB id.
        /// </summary>
        /// <param name="str">The STR.</param>
        /// <param name="aType">A type.</param>
        /// <returns>Guid.</returns>
        /// <exception cref="System.ArgumentNullException">aType</exception>
        public static Guid GetMBId(this string str, Type aType)
        {
            if (aType == null)
            {
                throw new ArgumentNullException("aType");
            }
            
            return (aType.FullName + str.ToLower()).GetMD5();
        }

        /// <summary>
        /// Helper method for Dictionaries since they throw on not-found keys
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="key">The key.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>``1.</returns>
        public static U GetValueOrDefault<T, U>(this Dictionary<T, U> dictionary, T key, U defaultValue)
        {
            U val;
            if (!dictionary.TryGetValue(key, out val))
            {
                val = defaultValue;
            }
            return val;

        }

        /// <summary>
        /// Gets the attribute value.
        /// </summary>
        /// <param name="str">The STR.</param>
        /// <param name="attrib">The attrib.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">attrib</exception>
        public static string GetAttributeValue(this string str, string attrib)
        {
            if (attrib == null)
            {
                throw new ArgumentNullException("attrib");
            }
            
            string srch = "[" + attrib + "=";
            int start = str.IndexOf(srch, StringComparison.OrdinalIgnoreCase);
            if (start > -1)
            {
                start += srch.Length;
                int end = str.IndexOf(']', start);
                return str.Substring(start, end - start);
            }
            return null;
        }
    }
}
