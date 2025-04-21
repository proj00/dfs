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

export class NodeServiceSerializer {
  static getObjectPath(req: Path): Uint8Array {
    return Path.toBinary(req);
  }

  static revealObjectInExplorer(req: Empty): Uint8Array {
    return Empty.toBinary(req);
  }

  static importObjectFromDisk(req: Guid): Uint8Array {
    return Guid.toBinary(req);
  }

  static publishToTracker(req: Empty): Uint8Array {
    return Empty.toBinary(req);
  }

  static searchForObjects(req: SearchResponseList): Uint8Array {
    return SearchResponseList.toBinary(req);
  }

  static getDataUsage(req: DataUsage): Uint8Array {
    return DataUsage.toBinary(req);
  }

  static getAllContainers(req: GuidList): Uint8Array {
    return GuidList.toBinary(req);
  }

  static getContainerObjects(req: ObjectList): Uint8Array {
    return ObjectList.toBinary(req);
  }

  static getContainerRootHash(req: Hash): Uint8Array {
    return Hash.toBinary(req);
  }

  static downloadContainer(req: Empty): Uint8Array {
    return Empty.toBinary(req);
  }

  static pauseContainerDownload(req: Empty): Uint8Array {
    return Empty.toBinary(req);
  }

  static resumeContainerDownload(req: Empty): Uint8Array {
    return Empty.toBinary(req);
  }

  static cancelContainerDownload(req: Empty): Uint8Array {
    return Empty.toBinary(req);
  }

  static getDownloadProgress(req: Progress): Uint8Array {
    return Progress.toBinary(req);
  }

  static modifyBlockListEntry(req: Empty): Uint8Array {
    return Empty.toBinary(req);
  }

  static getBlockList(req: BlockListResponse): Uint8Array {
    return BlockListResponse.toBinary(req);
  }

  static logMessage(req: Empty): Uint8Array {
    return Empty.toBinary(req);
  }

  static revealLogFile(req: Empty): Uint8Array {
    return Empty.toBinary(req);
  }

  static shutdown(req: Empty): Uint8Array {
    return Empty.toBinary(req);
  }

  static applyFsOperation(req: Empty): Uint8Array {
    return Empty.toBinary(req);
  }
}
