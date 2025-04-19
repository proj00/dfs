// See the Electron documentation for details on how to use preload scripts:
// https://www.electronjs.org/docs/latest/tutorial/process-model#preload-scripts

import { contextBridge, ipcRenderer } from "electron";

contextBridge.exposeInMainWorld("electronAPI", {
  onAppQuit: (callback: () => void) => {
    ipcRenderer.on("app-is-quitting", callback);
  },
  confirmQuit: () => {
    ipcRenderer.send("quit-confirmed");
  },
});
