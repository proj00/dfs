import { HostObjectAsyncProxy } from "../types/webView2";

export interface INodeService extends HostObjectAsyncProxy {
  RegisterUiService: (service: any) => Promise<void>;
  Hi: () => Promise<string>;
}

let serviceInstance: INodeService | null = null;

export const GetNodeService = (): INodeService => {
  if (serviceInstance == null) {
    serviceInstance = window.chrome.webview.hostObjects
      .service as INodeService | null;
  }
  if (serviceInstance == null) {
    throw new Error("NodeService host object not found");
  }
  return serviceInstance;
};
