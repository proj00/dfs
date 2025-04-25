// See the Electron documentation for details on how to use preload scripts:
// https://www.electronjs.org/docs/latest/tutorial/process-model#preload-scripts

import { contextBridge, ipcRenderer } from "electron";
import { ipcService } from "./types/wrap/IpcRendererService";

contextBridge.exposeInMainWorld("electronAPI", {
  selectFile: () => ipcRenderer.invoke("select-file"),
  selectFolder: () => ipcRenderer.invoke("select-folder"),
  writeClipboard: (text: string) => ipcRenderer.invoke("write-clipboard", text),
  readClipboard: () => console.log("???"),
});

contextBridge.exposeInMainWorld("ipcService", ipcService);
