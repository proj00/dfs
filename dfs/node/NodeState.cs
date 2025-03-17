using Fs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace node
{
    public class NodeState
    {
        public Dictionary<string, string> pathByHash { get; }
        public Dictionary<string, Fs.FileSystemObject> objectByHash { get; }

        public NodeState()
        {
            this.objectByHash = [];
            this.pathByHash = [];
        }
    }
}
