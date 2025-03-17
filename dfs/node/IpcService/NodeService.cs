using CefSharp;
using common;
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
        private UiService? service;
        private NodeState state;

        public NodeService(NodeState state)
        {
            this.state = state;
        }

        public void RegisterUiService(dynamic service)
        {
            this.service = new UiService(service);
        }

        public string PickObjectPath(bool folder)
        {
            if (folder)
            {
                using var dialog = new FolderBrowserDialog();
                dialog.Multiselect = false;
                var result = dialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrEmpty(dialog.SelectedPath) && Directory.Exists(dialog.SelectedPath))
                {
                    return dialog.SelectedPath;
                }
            }
            else
            {
                using var dialog = new OpenFileDialog();
                dialog.Multiselect = false;
                var result = dialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrEmpty(dialog.FileName) && File.Exists(dialog.FileName))
                {
                    return dialog.FileName;
                }
            }

            throw new Exception("Dialog failed");
        }

        public void ImportObjectFromDisk(string path, int chunkSize)
        {
            if (chunkSize <= 0 || chunkSize > Constants.maxChunkSize)
            {
                throw new Exception("Invalid chunk size");
            }

            if (File.Exists(path))
            {
                var obj = FilesystemUtils.GetFileObject(path, chunkSize);
                var hash = HashUtils.GetHash(obj);

                state.pathByHash[hash] = path;
                state.objectByHash[hash] = obj;

                return;
            }

            if (Directory.Exists(path))
            {
                var entries = FilesystemUtils.GetRecursiveDirectoryObject(path, chunkSize, state.pathByHash);
                foreach (var entry in entries)
                {
                    state.objectByHash[entry.Key] = entry.Value;
                }

                return;
            }

            throw new Exception("Invalid path");
        }
    }
}
