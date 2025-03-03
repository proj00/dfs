using CefSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace node.IpcService
{
    public class IpcUtilities
    {
        public static async Task<object> Execute(IJavascriptCallback callback, object[] args)
        {
            var response = await callback.ExecuteAsync(args);
            if (response == null)
            {
                throw new NullReferenceException();
            }
            if (!response.Success)
            {
                throw new Exception(response.Message);
            }

            return response.Result;
        }
    }
}
