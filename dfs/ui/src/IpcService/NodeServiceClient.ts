import { fromBase64, toBase64 } from "@/lib/utils";
import { fs } from "@/types/fs/filesystem";
import { Ui } from "@/types/rpc/uiservice";
import { rpc_common } from "@/types/rpc_common";
import * as grpc from "@grpc/grpc-js";
import { promisify } from "util";

const serviceUrl: string = "localhost:42069";

interface GrpcUnaryServiceInterface<P, R> {
  (
    message: P,
    metadata: grpc.Metadata,
    options: grpc.CallOptions,
    callback: grpc.requestCallback<R>,
  ): grpc.ClientUnaryCall;
  (
    message: P,
    metadata: grpc.Metadata,
    callback: grpc.requestCallback<R>,
  ): grpc.ClientUnaryCall;
  (
    message: P,
    options: grpc.CallOptions,
    callback: grpc.requestCallback<R>,
  ): grpc.ClientUnaryCall;
  (message: P, callback: grpc.requestCallback<R>): grpc.ClientUnaryCall;
}

function getHash(b64: string): rpc_common.Hash {
  return new rpc_common.Hash({ data: fromBase64(b64) });
}

function getGuid(guid: string): rpc_common.Guid {
  return new rpc_common.Guid({ guid });
}

export interface INodeService {
  PickObjectPath: (pickFolder: boolean) => Promise<string>;
  GetObjectPath: (base64Hash: string) => Promise<string>;
  RevealObjectInExplorer: (base64Hash: string) => Promise<void>;
  GetAllContainers: () => Promise<string[]>;
  GetDownloadProgress: (base64Hash: string) => Promise<Ui.Progress>;
  GetContainerObjects: (container: string) => Promise<fs.ObjectList>;
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
  CopyToClipboard: (str: string) => Promise<void>;
}

class NodeServiceClient implements INodeService {
  private client: Ui.UiClient;
  constructor(channel: grpc.Channel) {
    this.client = new Ui.UiClient(
      serviceUrl,
      grpc.credentials.createInsecure(),
      { channelOverride: channel },
    );
  }

  private async callGrpc<_in, _out>(
    method: GrpcUnaryServiceInterface<_in, _out>,
    arg: _in,
  ): Promise<_out> {
    const promised = promisify(method);
    const response = await promised(arg);
    if (response == null) {
      throw new Error("undefined response");
    }
    return response;
  }

  async PickObjectPath(pickFolder: boolean): Promise<string> {
    return (
      await this.callGrpc(
        this.client.PickObjectPath,
        new Ui.ObjectOptions({ pickFolder }),
      )
    ).path;
  }
  async GetObjectPath(base64Hash: string): Promise<string> {
    return (await this.callGrpc(this.client.GetObjectPath, getHash(base64Hash)))
      .path;
  }
  async RevealObjectInExplorer(base64Hash: string): Promise<void> {
    await this.callGrpc(
      this.client.RevealObjectInExplorer,
      getHash(base64Hash),
    );
  }
  async GetAllContainers(): Promise<string[]> {
    return (
      await this.callGrpc(this.client.GetAllContainers, new rpc_common.Empty())
    ).guid;
  }
  async GetDownloadProgress(base64Hash: string): Promise<Ui.Progress> {
    return this.callGrpc(this.client.GetDownloadProgress, getHash(base64Hash));
  }
  async GetContainerObjects(container: string): Promise<fs.ObjectList> {
    return this.callGrpc(this.client.GetContainerObjects, getGuid(container));
  }
  async GetContainerRootHash(container: string): Promise<string> {
    return toBase64(
      (
        await this.callGrpc(
          this.client.GetContainerRootHash,
          getGuid(container),
        )
      ).data,
    );
  }
  async ImportObjectFromDisk(path: string, chunkSize: number): Promise<string> {
    return (
      await this.callGrpc(
        this.client.ImportObjectFromDisk,
        new Ui.ObjectFromDiskOptions({ path, chunkSize }),
      )
    ).guid;
  }
  async PublishToTracker(container: string, trackerUri: string): Promise<void> {
    await this.callGrpc(
      this.client.PublishToTracker,
      new Ui.PublishingOptions({
        containerGuid: container,
        trackerUri,
      }),
    );
  }
  async DownloadContainer(
    container: string,
    trackerUri: string,
    destinationDir: string,
    maxConcurrentChunks: number,
  ): Promise<void> {
    await this.callGrpc(
      this.client.DownloadContainer,
      new Ui.DownloadContainerOptions({
        containerGuid: container,
        trackerUri,
        destinationDir,
        maxConcurrentChunks,
      }),
    );
  }
  async CopyToClipboard(str: string): Promise<void> {
    await this.callGrpc(
      this.client.CopyToClipboard,
      new Ui.String({ value: str }),
    );
  }

  async PauseContainerDownload(container: string): Promise<void> {
    await this.callGrpc(this.client.PauseContainerDownload, getGuid(container));
  }
  async ResumeContainerDownload(container: string): Promise<void> {
    await this.callGrpc(
      this.client.ResumeContainerDownload,
      getGuid(container),
    );
  }
}

const channel = new grpc.Channel(
  serviceUrl,
  grpc.credentials.createInsecure(),
  {},
);

export const GetNodeService = async (): Promise<INodeService> => {
  return new NodeServiceClient(channel);
};
