import { SearchResponseList } from "@/types/rpc/uiservice";
import { jest } from "@jest/globals";

export function getMockClient() {
  return {
    GetObjectPath: jest.fn(),
    RevealObjectInExplorer: jest.fn(),
    ImportObjectFromDisk: jest.fn(),
    ImportObjectToContainer: jest.fn(),
    PublishToTracker: jest.fn(),
    SearchForObjects: jest.fn<() => Promise<SearchResponseList>>(),
    GetDataUsage: jest.fn(),
    GetAllContainers: jest.fn(),
    GetContainerObjects: jest.fn(),
    GetContainerRootHash: jest.fn(),
    DownloadContainer: jest.fn(),
    PauseFileDownload: jest.fn(),
    ResumeFileDownload: jest.fn(),
    GetDownloadProgress: jest.fn(),
    ModifyBlockListEntry: jest.fn(),
    GetBlockList: jest.fn(),
    LogMessage: jest.fn(),
    RevealLogFile: jest.fn(),
    Shutdown: jest.fn(),
    ApplyFsOperation: jest.fn(),
  };
}
