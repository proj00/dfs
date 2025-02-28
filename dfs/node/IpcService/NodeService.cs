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
        private ExpandoObject? service;

        public NodeService()
        {
            value = -10;
        }

        public void RegisterUiService(object service)
        {
            // this actually gets a COMException instead of an object :(
            //Console.WriteLine(service);
        }

        public string Hi()
        {
            return $"hi {value}";
        }
    }
}
