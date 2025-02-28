using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace node.IpcService
{
    [ComVisible(true)]
    public interface IUiService
    {
        public int getValue();
        public void setValue(int value);
    }
}
