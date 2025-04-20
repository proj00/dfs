// --------------
// Wrapper for NodeServiceClient (utility functions / transformations to UI internal structs)
// --------------

import { GetNodeService, INodeService } from "@/IpcService/NodeServiceClient";
import { FromObjectWithHash, type File, type Folder } from "../lib/types";

// Define interfaces for the backend services
export interface BackendServiceInterface {
  publishToTracker: (
    containerId: string,
    trackerUri: string,
  ) => Promise<boolean>;

  // Downloads a container (filesystem tree) and returns a list of download IDs (one per file)
  downloadContainer: (
    containerGuid: string,
    trackerUri: string,
    destination?: string,
  ) => Promise<string[]>;

  fetchDriveData: () => Promise<DriveData>;

  // Backend endpoints for data usage and download progress
  GetDataUsage: () => Promise<DataUsage>;
  GetDownloadProgress: (fileId: string) => Promise<DownloadProgress>;
}

// Define the data structure
export interface DriveData {
  files: File[];
  folders: Folder[];
}

// Define data usage interface
export interface DataUsage {
  totalBytesSent: number;
  totalBytesReceived: number;
}

// Define download progress interface for a single file
export interface DownloadProgress {
  fileId: string;
  receivedBytes: number;
  totalBytes: number;
  status: "active" | "completed" | "failed";
  fileName: string;
}

class BackendService implements BackendServiceInterface {
  private trackerUris: string[] = [];

  async GetDataUsage(): Promise<DataUsage> {
    const service = await GetNodeService();
    let usage: DataUsage = {
      totalBytesSent: 0,
      totalBytesReceived: 0,
    };

    for (const uri of this.trackerUris) {
      const stats = await service.GetDataUsage(uri);
      usage.totalBytesReceived += Number(stats.download);
      usage.totalBytesSent += Number(stats.upload);
    }

    return usage;
  }

  async GetDownloadProgress(fileId: string): Promise<DownloadProgress> {
    const service = await GetNodeService();
    const progress = await service.GetDownloadProgress(fileId);

    return {
      fileId,
      receivedBytes: Number(progress.current),
      totalBytes: Number(progress.total),
      status: progress.total == progress.current ? "completed" : "active",
      fileName: fileId,
    };
  }

  async publishToTracker(
    containerId: string,
    trackerUri: string,
  ): Promise<boolean> {
    console.log(`Publishing container ${containerId} to tracker ${trackerUri}`);
    const service = await GetNodeService();
    await service.PublishToTracker(containerId, trackerUri);
    return true;
  }

  async downloadContainer(
    containerGuid: string,
    trackerUri: string,
    destination?: string,
  ): Promise<string[]> {
    this.trackerUris.push(trackerUri);
    destination = destination ?? "";
    console.log(
      `Downloading container ${containerGuid} from tracker ${trackerUri} into ${destination}`,
    );

    const service = await GetNodeService();

    // fire and forget?
    service
      .DownloadContainer(containerGuid, trackerUri, destination, 20)
      .catch((e) => console.log(e));

    const IDs = (
      await this.getContainerObjects(service, containerGuid)
    ).files.map((file) => file.id);

    return IDs;
  }

  async fetchDriveData(): Promise<DriveData> {
    let contents: DriveData = {
      folders: [],
      files: [],
    };

    const service = await GetNodeService();
    const containers = await service.GetAllContainers();

    for (const container of containers) {
      let containerObjects: DriveData = await this.getContainerObjects(
        service,
        container,
      );

      contents.files.push(...containerObjects.files);
      contents.folders.push(...containerObjects.folders);
    }

    return contents;
  }

  private async getContainerObjects(service: INodeService, container: string) {
    const objects = (await service.GetContainerObjects(container)).data;
    const internalObjects = objects.map((object) =>
      FromObjectWithHash(object, objects, container),
    );

    let containerObjects: DriveData = { folders: [], files: [] };
    for (let i = 0; i < internalObjects.length; i++) {
      if ((internalObjects[i] as any).size != null) {
        containerObjects.files.push(internalObjects[i] as File);
      } else {
        containerObjects.folders.push(internalObjects[i] as Folder);
      }
    }
    return containerObjects;
  }
}

// Export a singleton instance
export const backendService: BackendServiceInterface = new BackendService();
