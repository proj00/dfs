using CefSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace node.IpcService
{
    internal class UiService
    {
        private dynamic service;

        public UiService(dynamic service)
        {
            this.service = service;
        }

        public async Task<int> getValue()
        {
            return await IpcUtilities.Execute(service.getValue, (object[])[]);
        }

        public async Task setValue(int number)
        {
            await IpcUtilities.Execute(service.setValue, (object[])[number]);
        }

        public async Task callExample()
        {
            await IpcUtilities.Execute(service.callExample, (object[])[]);
        }
    }
}
