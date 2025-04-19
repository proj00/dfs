// See the Electron documentation for details on how to use preload scripts:
// https://www.electronjs.org/docs/latest/tutorial/process-model#preload-scripts

import { clipboard, contextBridge, ipcRenderer } from "electron";

const portPromise = new Promise<number>((resolve) => {
  ipcRenderer.once("backend-port", (_, port: number) => {
    resolve(port);
  });
});

contextBridge.exposeInMainWorld("electronAPI", {
  selectFile: () => ipcRenderer.invoke("select-file"),
  selectFolder: () => ipcRenderer.invoke("select-folder"),
  writeClipboard: (text: string) => clipboard.writeText(text),
  readClipboard: () => clipboard.readText(),
  getPort: () => portPromise,
});
