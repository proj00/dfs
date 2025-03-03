using CefSharp;

namespace node.UiResourceLoading
{
    public class UiResourceHandlerFactory : ISchemeHandlerFactory
    {
        public IResourceHandler Create(IBrowser browser, IFrame frame, string schemeName, IRequest request)
        {
            return new UiResourceHandler();
        }
    }
}
