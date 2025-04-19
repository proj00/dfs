/**
 * This file will automatically be loaded by webpack and run in the "renderer" context.
 * To learn more about the differences between the "main" and the "renderer" context in
 * Electron, visit:
 *
 * https://electronjs.org/docs/latest/tutorial/process-model
 *
 * By default, Node.js integration in this file is disabled. When enabling Node.js integration
 * in a renderer process, please be aware of potential security implications. You can read
 * more about security risks here:
 *
 * https://electronjs.org/docs/tutorial/security
 *
 * To enable Node.js integration in this file, open up `main.js` and enable the `nodeIntegration`
 * flag:
 *
 * ```
 *  // Create the browser window.
 *  mainWindow = new BrowserWindow({
 *    width: 800,
 *    height: 600,
 *    webPreferences: {
 *      nodeIntegration: true
 *    }
 *  });
 * ```
 */

import "./index.css";

console.log(
  '👋 This message is being logged by "renderer.js", included via webpack',
);

import "./App";
import { GetNodeService } from "./IpcService/NodeServiceClient";

(window as any).electronAPI.onAppQuit(async () => {
  try {
    const client = await GetNodeService();
    await client.Shutdown();
    console.log("Shutdown notification sent to server");
  } catch (err) {
    console.error("Failed to notify server on shutdown", err);
  } finally {
    // ✅ Notify main process it's safe to quit
    (window as any).electronAPI.confirmQuit();
  }
});
