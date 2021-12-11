using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace PaygatePaySubs.Services
{
    public class Payment : IPayment
    {
        #region Utilities
        /** Encode dictionary to Url string */
        public string ToUrlEncodedString(Dictionary<string, string> request)
        {
            StringBuilder builder = new StringBuilder();

            foreach (string key in request.Keys)
            {
                builder.Append("&");
                builder.Append(key);
                builder.Append("=");
                builder.Append(HttpUtility.UrlEncode(request[key]));
            }

            string result = builder.ToString().TrimStart('&');

            return result;
        }

        /** Convert query string to dictionary */
        public Dictionary<string, string> ToDictionary(string response)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            string[] valuePairs = response.Split('&');
            foreach (string valuePair in valuePairs)
            {
                if (valuePair.Contains('='))
                {
                    string[] values = valuePair.Split('=');
                    result.Add(values[0], HttpUtility.UrlDecode(values[1]));
                }
            }

            return result;
        }
        #endregion Utility

        #region MD5 Hashing
        // Adapted from
        // https://msdn.microsoft.com/en-us/library/system.security.cryptography.md5(v=vs.110).aspx

        public string GetMd5Hash(Dictionary<string, string> data, string encryptionKey)
        {
            using (MD5 md5Hash = MD5.Create())
            {
                StringBuilder input = new StringBuilder();
                foreach (string value in data.Values)
                {
                    input.Append(value + "|");
                }

                input.Append(encryptionKey);

                byte[] hash = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input.ToString()));

                StringBuilder sBuilder = new StringBuilder();

                for (int i = 0; i < hash.Length; i++)
                {
                    sBuilder.Append(hash[i].ToString("x2"));
                }
                return sBuilder.ToString();
            }
        }

        public bool VerifyMd5Hash(Dictionary<string, string> data, string encryptionKey, string hash)
        {
            Dictionary<string, string> hashDict = new Dictionary<string, string>();

            foreach (string key in data.Keys)
            {
                if (key != "CHECKSUM")
                {
                    hashDict.Add(key, data[key]);
                }
            }

            string hashOfInput = GetMd5Hash(hashDict, encryptionKey);

            StringComparer comparer = StringComparer.OrdinalIgnoreCase;

            if (0 == comparer.Compare(hashOfInput, hash))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion MD5 Hash

    }
}