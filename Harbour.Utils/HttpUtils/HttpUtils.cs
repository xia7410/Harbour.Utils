﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Harbour.Utils
{
    /// <summary>
    /// 
    /// </summary>
    public class HttpUtils
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static Stream GetStream(string url)
        {
            return RequestStream(new HttpParam()
            {
                Url = url,
                Method = "GET"
            });
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="getParam"></param>
        /// <returns></returns>
        public static Stream GetStream(string url, object getParam)
        {
            return RequestStream(new HttpParam()
            {
                Url = url,
                Method = "GET",
                GetParam = getParam
            });
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static Stream PostStream(string url)
        {
            return RequestStream(new HttpParam()
            {
                Url = url,
                Method = "POST"
            });
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postParam"></param>
        /// <returns></returns>
        public static Stream PostStream(string url, object postParam)
        {
            return RequestStream(new HttpParam()
            {
                Url = url,
                Method = "POST",
                GetParam = postParam
            });
        }
        /// <summary>
        /// 文件上传至远程服务器
        /// 传入：Url、CookieContainer、PostParam、PostedFile
        /// </summary>
        public static string PostFile(HttpParam param)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(param.Url);
            request.CookieContainer = param.CookieContainer;
            request.Method = "POST";
            request.Timeout = 20000;
            request.Credentials = CredentialCache.DefaultCredentials;
            request.KeepAlive = true;
            string boundary = "----------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundaryBytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");
            request.ContentType = "multipart/form-data; boundary=" + boundary;
            string formdataTemplate = "\r\n--" + boundary + "\r\nContent-Disposition: form-data; name=\"{0}\";\r\n\r\n{1}";
            byte[] buffer = buffer = new byte[param.FileStream.Length];
            param.FileStream.Read(buffer, 0, Convert.ToInt32(param.FileStream.Length));
            var strHeader = "Content-Disposition:application/x-www-form-urlencoded; name=\"{0}\";filename=\"{1}\"\r\nContent-Type:{2}\r\n\r\n";
            strHeader = string.Format(strHeader, "filedata", param.FileStream.Name, "application/octet-stream");
            var byteHeader = Encoding.ASCII.GetBytes(strHeader);
            try
            {
                using (Stream stream = request.GetRequestStream())
                {
                    if (param.PostParam != null)
                    {
                        var postParamString = "";
                        if (param.PostParam is string)
                        {
                            postParamString = param.PostParam.ToString();
                        }
                        else
                        {
                            postParamString = JsonConvert.SerializeObject(param.PostParam);
                        }
                        byte[] bs = param.Encoding.GetBytes(postParamString);
                        stream.Write(bs, 0, bs.Length);
                    }
                    stream.Write(boundaryBytes, 0, boundaryBytes.Length);
                    stream.Write(byteHeader, 0, byteHeader.Length);
                    stream.Write(buffer, 0, buffer.Length);
                    byte[] trailer = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
                    stream.Write(trailer, 0, trailer.Length);
                    stream.Close();
                }
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                var result = "";
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    result = reader.ReadToEnd();
                }
                response.Close();
                return result;
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
        /// <summary>
        /// 获取响应流
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        public static Stream RequestStream(HttpParam param)
        {
            #region 处理地址栏参数
            var getParamSb = new StringBuilder();
            if (param.GetParam != null)
            {
                if (param.GetParam is string)
                {
                    getParamSb.Append(param.GetParam.ToString());
                }
                else
                {
                    param.GetParam.GetType().GetProperties().ToList().ForEach(d =>
                    {
                        getParamSb.AppendFormat("{0}={1}&", d.Name, d.GetValue(param.GetParam, null));
                    });
                }
            }
            if (!string.IsNullOrWhiteSpace(getParamSb.ToString().TrimEnd('&')))
            {
                param.Url = string.Format("{0}?{1}", param.Url, getParamSb.ToString().TrimEnd('&'));
            }
            #endregion
            HttpWebRequest httpWebRequest = WebRequest.Create(param.Url) as HttpWebRequest;
            if (!string.IsNullOrWhiteSpace(param.CertPath) && !string.IsNullOrWhiteSpace(param.CertPwd))
            {
                ServicePointManager.ServerCertificateValidationCallback = CheckValidationResult;
                var cer = new X509Certificate2(param.CertPath, param.CertPwd, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet);
                httpWebRequest.ClientCertificates.Add(cer);
                #region 暂时不要的
                //ServicePointManager.Expect100Continue = true;
                //ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3;
                //req.ProtocolVersion = HttpVersion.Version11;
                //req.UserAgent = SUserAgent;
                //req.KeepAlive = false;
                //var cookieContainer = new CookieContainer();
                //req.CookieContainer = cookieContainer;
                //req.Timeout = 1000 * 60;
                //req.Headers.Add("x-requested-with", "XMLHttpRequest");
                #endregion
            }
            httpWebRequest.Timeout = param.TimeOut * 1000;
            httpWebRequest.UserAgent = param.UserAgent;
            httpWebRequest.Method = param.Method ?? "POST";
            httpWebRequest.Referer = param.Referer;
            httpWebRequest.CookieContainer = param.CookieContainer;
            httpWebRequest.ContentType = param.ContentType;
            if (param.PostParam != null)
            {
                var postParamString = "";
                if (param.PostParam is string)
                {
                    postParamString = param.PostParam.ToString();
                }
                else if (param.ParamType == HttpParamType.Form)
                {
                    var dicParam = JsonConvert.DeserializeObject<Dictionary<string, string>>(JsonConvert.SerializeObject(param.PostParam));
                    postParamString = dicParam.Aggregate(postParamString, (current, dic) => current + (dic.Key + "=" + dic.Value + "&")).TrimEnd('&');
                }
                else
                {
                    postParamString = JsonConvert.SerializeObject(param.PostParam);
                }
                byte[] bs = param.Encoding.GetBytes(postParamString);
                httpWebRequest.ContentLength = bs.Length;
                using (Stream rs = httpWebRequest.GetRequestStream())
                {
                    rs.Write(bs, 0, bs.Length);
                }
            }
            return httpWebRequest.GetResponse().GetResponseStream();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="param"></param>
        public static string RequestString(HttpParam param)
        {
            var result = "";
            using (var reader = new StreamReader(RequestStream(param), param.Encoding))
            {
                result = reader.ReadToEnd();
            }
            return result;
        }

        private static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            return true;
        }


        #region Get请求
        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="getParam"></param>
        /// <returns></returns>
        public static string Get(string url, object getParam = null)
        {
            var param = new HttpParam
            {
                Url = url,
                Method = "GET",
                GetParam = getParam
            };
            return Get(param);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        public static string Get(HttpParam param)
        {
            param.Method = "GET";
            return RequestString(param);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="getParam"></param>
        /// <returns></returns>
        public static T Get<T>(string url, object getParam = null)
        {
            var str = Get(url, getParam);
            return JsonConvert.DeserializeObject<T>(str);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="param"></param>
        /// <returns></returns>
        public static T Get<T>(HttpParam param)
        {
            var str = Get(param);
            return JsonConvert.DeserializeObject<T>(str);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="getParam"></param>
        /// <returns></returns>
        public static JsonResponse<T> GetJR<T>(string url, object getParam = null)
        {
            return Get<JsonResponse<T>>(url, getParam);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="param"></param>
        /// <returns></returns>
        public static JsonResponse<T> GetJR<T>(HttpParam param)
        {
            return Get<JsonResponse<T>>(param);
        }
        #endregion

        #region Post 请求
        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postParam"></param>
        /// <returns></returns>
        public static string Post(string url, object postParam = null)
        {
            var param = new HttpParam
            {
                Url = url,
                Method = "POST",
                PostParam = postParam
            };
            var str = Post(param);
            return str;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        public static string Post(HttpParam param)
        {
            param.Method = "POST";
            var str = RequestString(param);
            return str;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postParam"></param>
        /// <returns></returns>
        public static T Post<T>(string url, object postParam = null)
        {
            var str = Post(url, postParam);
            return JsonConvert.DeserializeObject<T>(str);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="param"></param>
        /// <returns></returns>
        public static T Post<T>(HttpParam param)
        {
            param.Method = "POST";
            var str = Post(param);
            return JsonConvert.DeserializeObject<T>(str);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postParam"></param>
        /// <returns></returns>
        public static JsonResponse<T> PostJR<T>(string url, object postParam = null)
        {
            return Post<JsonResponse<T>>(url, postParam);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="param"></param>
        /// <returns></returns>
        public static JsonResponse<T> PostJR<T>(HttpParam param)
        {
            return Post<JsonResponse<T>>(param);
        }

        #endregion

    }
}