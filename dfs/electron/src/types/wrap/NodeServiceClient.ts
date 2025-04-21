// @ts-ignore i hate typescript
import { Hash, Empty, Guid, DataUsage, GuidList } from "@/types/rpc_common";
// @ts-ignore i hate typescript
import {
  Path,
  ObjectFromDiskOptions,
  PublishingOptions,
  SearchRequest,
  SearchResponseList,
  UsageRequest,
  DownloadContainerOptions,
  Progress,
  BlockListRequest,
  BlockListResponse,
  String$,
  FsOperation,
} from "@/types/rpc/uiservice";
// @ts-ignore i hate typescript
import { ObjectList } from "@/types/fs/filesystem";

export class NodeServiceClient {
  async GetObjectPath(req: Hash): Promise<Path> {
    return Path.fromBinary(
      await window.electronAPI.callGrpc("getObjectPath", req),
    );
  }

  async RevealObjectInExplorer(req: Hash): Promise<void> {
    await window.electronAPI.callGrpc("revealObjectInExplorer", req);
  }

  async ImportObjectFromDisk(req: ObjectFromDiskOptions): Promise<Guid> {
    return Guid.fromBinary(
      await window.electronAPI.callGrpc("importObjectFromDisk", req),
    );
  }

  async PublishToTracker(req: PublishingOptions): Promise<void> {
    await window.electronAPI.callGrpc("publishToTracker", req);
  }

  async SearchForObjects(req: SearchRequest): Promise<SearchResponseList> {
    return SearchResponseList.fromBinary(
      await window.electronAPI.callGrpc("searchForObjects", req),
    );
  }

  async GetDataUsage(req: UsageRequest): Promise<DataUsage> {
    return DataUsage.fromBinary(
      await window.electronAPI.callGrpc("getDataUsage", req),
    );
  }

  async GetAllContainers(): Promise<GuidList> {
    return GuidList.fromBinary(
      await window.electronAPI.callGrpc("getAllContainers", {}),
    );
  }

  async GetContainerObjects(req: Guid): Promise<ObjectList> {
    return ObjectList.fromBinary(
      await window.electronAPI.callGrpc("getContainerObjects", req),
    );
  }

  async GetContainerRootHash(req: Guid): Promise<Hash> {
    return Hash.fromBinary(
      await window.electronAPI.callGrpc("getContainerRootHash", req),
    );
  }

  async DownloadContainer(req: DownloadContainerOptions): Promise<void> {
    await window.electronAPI.callGrpc("downloadContainer", req);
  }

  async PauseContainerDownload(req: Guid): Promise<void> {
    await window.electronAPI.callGrpc("pauseContainerDownload", req);
  }

  async ResumeContainerDownload(req: Guid): Promise<void> {
    await window.electronAPI.callGrpc("resumeContainerDownload", req);
  }

  async CancelContainerDownload(req: Guid): Promise<void> {
    await window.electronAPI.callGrpc("cancelContainerDownload", req);
  }

  async GetDownloadProgress(req: Hash): Promise<Progress> {
    return Progress.fromBinary(
      await window.electronAPI.callGrpc("getDownloadProgress", req),
    );
  }

  async ModifyBlockListEntry(req: BlockListRequest): Promise<void> {
    await window.electronAPI.callGrpc("modifyBlockListEntry", req);
  }

  async GetBlockList(): Promise<BlockListResponse> {
    return BlockListResponse.fromBinary(
      await window.electronAPI.callGrpc("getBlockList", {}),
    );
  }

  async LogMessage(req: String$): Promise<void> {
    await window.electronAPI.callGrpc("logMessage", req);
  }

  async GetLogFilePath(): Promise<Path> {
    return Path.fromBinary(
      await window.electronAPI.callGrpc("getLogFilePath", {}),
    );
  }

  async Shutdown(): Promise<void> {
    await window.electronAPI.callGrpc("shutdown", {});
  }

  async ApplyFsOperation(req: FsOperation): Promise<void> {
    await window.electronAPI.callGrpc("applyFsOperation", req);
  }
}
