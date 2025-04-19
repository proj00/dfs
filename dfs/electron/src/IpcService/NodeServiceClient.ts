import { fromBase64, toBase64 } from "@/lib/utils";
import { ObjectList } from "@/types/fs/filesystem";
import { Progress } from "@/types/rpc/uiservice";
import { UiClient } from "@/types/rpc/uiservice.client";
import { DataUsage, Hash, SearchResponse } from "@/types/rpc_common";
import { GrpcWebFetchTransport } from "@protobuf-ts/grpcweb-transport";

const serviceUrl: string = "http://127.0.0.1:42069";

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
  Shutdown: () => Promise<void>;
}

class NodeServiceClient implements INodeService {
  private client: UiClient;
  constructor() {
    this.client = new UiClient(
      new GrpcWebFetchTransport({ baseUrl: serviceUrl }),
    );
    this.client;
  }
  async GetObjectPath(base64Hash: string): Promise<string> {
    return (await this.client.getObjectPath(getHash(base64Hash))).response.path;
  }
  async RevealObjectInExplorer(base64Hash: string): Promise<void> {
    await this.client.revealObjectInExplorer(getHash(base64Hash));
  }
  async GetAllContainers(): Promise<string[]> {
    return (await this.client.getAllContainers({})).response.guid;
  }
  async GetDownloadProgress(base64Hash: string): Promise<Progress> {
    return (await this.client.getDownloadProgress(getHash(base64Hash)))
      .response;
  }
  async GetContainerObjects(container: string): Promise<ObjectList> {
    return (await this.client.getContainerObjects({ guid: container }))
      .response;
  }
  async GetContainerRootHash(container: string): Promise<string> {
    return toBase64(
      (await this.client.getContainerRootHash({ guid: container })).response
        .data,
    );
  }
  async ImportObjectFromDisk(path: string, chunkSize: number): Promise<string> {
    return (await this.client.importObjectFromDisk({ path, chunkSize }))
      .response.guid;
  }
  async PublishToTracker(container: string, trackerUri: string): Promise<void> {
    await this.client.publishToTracker({
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
    await this.client.downloadContainer({
      containerGuid: container,
      trackerUri,
      destinationDir,
      maxConcurrentChunks,
    });
  }

  async PauseContainerDownload(container: string): Promise<void> {
    await this.client.pauseContainerDownload({ guid: container });
  }
  async ResumeContainerDownload(container: string): Promise<void> {
    await this.client.resumeContainerDownload({ guid: container });
  }
  async SearchForObjects(
    query: string,
    trackerUri: string,
  ): Promise<SearchResponse[]> {
    const call = this.client.searchForObjects({ trackerUri, query });

    let response: SearchResponse[] = [];
    for await (let r of call.responses) {
      response.push(r);
    }

    return response;
  }
  async GetDataUsage(trackerUri: string): Promise<DataUsage> {
    return (await this.client.getDataUsage({ trackerUri })).response;
  }
  async Shutdown(): Promise<void> {
    await this.client.shutdown({});
  }
}

const _client = new NodeServiceClient();

export const GetNodeService = async (): Promise<INodeService> => {
  return _client;
};
