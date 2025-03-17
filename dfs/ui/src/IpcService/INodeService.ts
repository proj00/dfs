import { ICefSharp } from "../types/ICefSharp";

// make TS shut up (UI::browser makes this available)
declare let CefSharp: ICefSharp;

export interface INodeService {
  RegisterUiService: (service: any) => Promise<void>;
  PickObjectPath: (folder: boolean) => Promise<string>;
  ImportObjectFromDisk: (path: string, chunkSize: number) => Promise<void>;
  PublishToTracker: (hashes: string[], trackerUri: string) => Promise<void>;
}

// make TS shut up (again; UI::browser makes this available after BindObjectAsync)
declare let nodeService: INodeService;

export const GetNodeService = async (): Promise<INodeService> => {
  await CefSharp.BindObjectAsync("nodeService");
  return nodeService;
};
