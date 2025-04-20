import { fromBase64, toBase64 } from "@/lib/utils";
import { ObjectList } from "@/types/fs/filesystem";
import { Progress } from "@/types/rpc/uiservice";
import { DataUsage, Hash, SearchResponse } from "@/types/rpc_common";

function getHash(b64: string): Hash {
  return { data: fromBase64(b64) };
}

export interface INodeService {
  GetObjectPath: (base64Hash: string) => Promise<string>;
  RevealObjectInExplorer: (base64Hash: string) => Promise<void>;
  GetAllContainers: () => Promise<string[]>;
  GetDownloadProgress: (base64Hash: string) => Promise<Progress>;
  GetContainerObjects: (container: string) => Promise<ObjectList>;
  GetContainerRootHash: (container: string) => Promise<string>;
  ImportObjectFromDisk: (path: string, chunkSize: number) => Promise<string>;
  PublishToTracker: (container: string, trackerUri: string) => Promise<void>;
  DownloadContainer: (
    container: string,
    trackerUri: string,
    destinationDir: string,
    maxConcurrentChunks: number,
  ) => Promise<void>;
  PauseContainerDownload: (container: string) => Promise<void>;
  ResumeContainerDownload: (container: string) => Promise<void>;
  SearchForObjects: (
    query: string,
    trackerUri: string,
  ) => Promise<SearchResponse[]>;
  GetDataUsage: (trackerUri: string) => Promise<DataUsage>;
}

class NodeServiceClient implements INodeService {
  async GetObjectPath(base64Hash: string): Promise<string> {
    return (
      await window.electronAPI.callGrpc("getObjectPath", getHash(base64Hash))
    ).path;
  }
  async RevealObjectInExplorer(base64Hash: string): Promise<void> {
    await window.electronAPI.callGrpc(
      "revealObjectInExplorer",
      getHash(base64Hash),
    );
  }
  async GetAllContainers(): Promise<string[]> {
    return (await window.electronAPI.callGrpc("getAllContainers", {})).guid;
  }
  async GetDownloadProgress(base64Hash: string): Promise<Progress> {
    return await window.electronAPI.callGrpc(
      "getDownloadProgress",
      getHash(base64Hash),
    );
  }
  async GetContainerObjects(container: string): Promise<ObjectList> {
    return await window.electronAPI.callGrpc("getContainerObjects", {
      guid: container,
    });
  }
  async GetContainerRootHash(container: string): Promise<string> {
    return toBase64(
      (
        await window.electronAPI.callGrpc("getContainerRootHash", {
          guid: container,
        })
      ).data,
    );
  }
  async ImportObjectFromDisk(path: string, chunkSize: number): Promise<string> {
    return (
      await window.electronAPI.callGrpc("importObjectFromDisk", {
        path,
        chunkSize,
      })
    ).guid;
  }
  async PublishToTracker(container: string, trackerUri: string): Promise<void> {
    await window.electronAPI.callGrpc("publishToTracker", {
      containerGuid: container,
      trackerUri,
    });
  }
  async DownloadContainer(
    container: string,
    trackerUri: string,
    destinationDir: string,
    maxConcurrentChunks: number,
  ): Promise<void> {
    await window.electronAPI.callGrpc("downloadContainer", {
      containerGuid: container,
      trackerUri,
      destinationDir,
      maxConcurrentChunks,
    });
  }

  async PauseContainerDownload(container: string): Promise<void> {
    await window.electronAPI.callGrpc("pauseContainerDownload", {
      guid: container,
    });
  }
  async ResumeContainerDownload(container: string): Promise<void> {
    await window.electronAPI.callGrpc("resumeContainerDownload", {
      guid: container,
    });
  }
  async SearchForObjects(
    query: string,
    trackerUri: string,
  ): Promise<SearchResponse[]> {
    return (
      await window.electronAPI.callGrpc("searchForObjects", {
        trackerUri,
        query,
      })
    ).results;
  }
  async GetDataUsage(trackerUri: string): Promise<DataUsage> {
    return await window.electronAPI.callGrpc("getDataUsage", { trackerUri });
  }
}

let _client: INodeService | undefined = undefined;

export const GetNodeService = async (): Promise<INodeService> => {
  if (_client === undefined) {
    _client = new NodeServiceClient();
  }
  return _client;
};
