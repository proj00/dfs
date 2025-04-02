import { ICefSharp } from "../types/ICefSharp";

// make TS shut up (UI::browser makes this available)
declare let CefSharp: ICefSharp;

export interface INodeService {
  RegisterUiService: (service: any) => Promise<void>;
  PickObjectPath: (pickFolder: boolean) => Promise<string>;
  GetObjectPath: (base64Hash: string) => Promise<string>;
  RevealObjectInExplorer: (base64Hash: string) => Promise<void>;
  GetAllContainers: () => Promise<string[]>;
  GetDownloadProgress: (base64Hash: string) => Promise<any>;
  GetContainerObjects: (container: string) => Promise<Uint8Array>;
  GetContainerRootHash: (container: string) => Promise<string>;
  ImportObjectFromDisk: (path: string, chunkSize: number) => Promise<string>;
  PublishToTracker: (container: string, trackerUri: string) => Promise<void>;
  DownloadContainer: (
    container: string,
    trackerUri: string,
    destinationDir: string,
    maxConcurrentChunks: number,
  ) => Promise<void>;
}

// make TS shut up (again; UI::browser makes this available after BindObjectAsync)
declare let nodeService: INodeService;

export const GetNodeService = async (): Promise<INodeService> => {
  await CefSharp.BindObjectAsync("nodeService");
  return nodeService;
};
