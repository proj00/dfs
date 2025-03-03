using CefSharp;
using Microsoft.Extensions.ObjectPool;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace node.IpcService
{
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class NodeService
    {
        private int value;
        private UiService? service;

        public NodeService()
        {
            value = -10;
        }

        public void RegisterUiService(dynamic service)
        {
            this.service = new UiService(service);
        }

        public async Task<string> Hi()
        {
            return $"hi {await service.getValue()}";
        }
    }
}
