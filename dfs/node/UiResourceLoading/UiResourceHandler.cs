using CefSharp;
using System.Net;

namespace node.UiResourceLoading
{
    public class UiResourceHandler : ResourceHandler
    {
        public override CefReturnValue ProcessRequestAsync(IRequest request, ICallback callback)
        {
            Task.Run(() =>
            {
                using (callback)
                {
                    var uri = new Uri(request.Url);

                    var contents = GetContents(uri.AbsolutePath[1..]);
                    if (contents == null)
                    {
                        StatusCode = (int)HttpStatusCode.NotFound;
                        Console.WriteLine($"{uri.AbsolutePath} {uri.AbsolutePath[1..]} {StatusCode}");

                        callback.Continue();

                        return;
                    }

                    var extension = Path.GetExtension(uri.AbsolutePath);
                    var stream = new MemoryStream(contents);

                    ResponseLength = stream.Length;
                    MimeType = Cef.GetMimeType(extension);
                    StatusCode = (int)HttpStatusCode.OK;
                    Stream = stream;
                    Headers["Access-Control-Allow-Origin"] = "*";

                    Console.WriteLine($"{uri.AbsolutePath} {uri.AbsolutePath[1..]} {StatusCode}");
                    callback.Continue();
                }
            });

            return CefReturnValue.ContinueAsync;
        }

        private static byte[]? GetContents(string resourceName)
        {
            object? contents = UiResources.ResourceManager.GetObject(resourceName, UiResources.Culture);
            return contents == null ? null : (byte[])contents;
        }
    }
}
