// --------------
// Wrapper for NodeServiceClient (utility functions / transformations to UI internal structs)
// --------------

import { GetNodeService } from "./GetNodeService";
import { FromObjectWithHash, type File, type Folder } from "../lib/types";
import { hashFromBase64 } from "@/lib/utils";
import { NodeServiceClient } from "@/types/wrap/NodeServiceClient";
import log from "electron-log/renderer";

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

  pauseDownload: (fileId: string) => Promise<void>;
  resumeDownload: (fileId: string) => Promise<void>;
  cancelDownload: (fileId: string) => Promise<void>; // Not implemented
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
    //const service = await GetNodeService();
    let usage: DataUsage = {
      totalBytesSent: 0,
      totalBytesReceived: 0,
    };

    // for (const uri of this.trackerUris) {
    //   const stats = await service.GetDataUsage({ trackerUri: uri });
    //   usage.totalBytesReceived += Number(stats.download);
    //   usage.totalBytesSent += Number(stats.upload);
    // }

    return usage;
  }

  async GetDownloadProgress(fileId: string): Promise<DownloadProgress> {
    const service = await GetNodeService();
    const progress = await service.GetDownloadProgress(hashFromBase64(fileId));

    const result: DownloadProgress = {
      fileId,
      receivedBytes: Number(progress.current),
      totalBytes: Number(progress.total),
      status: progress.total == progress.current ? "completed" : "active",
      fileName: fileId,
    };

    const now = Date.now();
    const existing = this.activeDownloads.get(fileId);

    if (existing) {
      const deltaBytes = result.receivedBytes - existing.progress.receivedBytes;
      const lastUpdate =
        (existing as any).lastUpdate ?? existing.startTime.getTime();
      const deltaTime = (now - lastUpdate) / 1000;

      const speed = deltaTime > 0 ? deltaBytes / deltaTime : 0;

      this.activeDownloads.set(fileId, {
        ...existing,
        progress: result,
        speed,
        lastUpdate: now, // <- naujas laukas (nebūtina tipizuoti – tik vidinei reikšmei)
      });
    }

    return result;
  }

  async pauseDownload(fileId: string): Promise<void> {
    const service = await GetNodeService();
    await service.PauseFileDownload(hashFromBase64(fileId));
  }

  async resumeDownload(fileId: string): Promise<void> {
    const service = await GetNodeService();
    await service.ResumeFileDownload(hashFromBase64(fileId));
  }

  async cancelDownload(fileId: string): Promise<void> {
    // Neturi CancelFileDownload, bet galima čia log'ą palikt ar simuliuot
    console.warn("Cancel not implemented — fileId:", fileId);
  }

  async publishToTracker(
    containerId: string,
    trackerUri: string,
  ): Promise<boolean> {
    log.info(`Publishing container ${containerId} to tracker ${trackerUri}`);
    const service = await GetNodeService();
    await service.PublishToTracker({ containerGuid: containerId, trackerUri });
    return true;
  }

  async downloadContainer(
    containerGuid: string,
    trackerUri: string,
    destination?: string,
  ): Promise<string[]> {
    this.trackerUris.push(trackerUri);
    destination = destination ?? "";
    log.info(
      `Downloading container ${containerGuid} from tracker ${trackerUri} into ${destination}`,
    );

    const service = await GetNodeService();

    // fire and forget?
    service
      .DownloadContainer({
        containerGuid,
        trackerUri,
        destinationDir: destination,
        maxConcurrentChunks: 20,
      })
      .catch((e) => log.info(e));

    const IDs = (
      await this.getContainerObjects(service, containerGuid)
    ).files.map((file) => file.id);
    const now = new Date();
    for (const id of IDs) {
      this.activeDownloads.set(id, {
        containerGuid,
        startTime: now,
        speed: 0,
        progress: {
          fileId: id,
          receivedBytes: 0,
          totalBytes: 0,
          status: "active",
          fileName: id,
        },
      });
    }
    return IDs;
  }

  async fetchDriveData(): Promise<DriveData> {
    let contents: DriveData = {
      folders: [],
      files: [],
    };

    const service = await GetNodeService();
    const containers = (await service.GetAllContainers()).guid;

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

  private async getContainerObjects(
    service: NodeServiceClient,
    container: string,
  ) {
    const objects = (await service.GetContainerObjects({ guid: container }))
      .data;
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
  getAllActiveDownloads() {
    return this.activeDownloads;
  }

  private activeDownloads = new Map<
    string,
    {
      containerGuid: string;
      startTime: Date;
      speed: number;
      progress: DownloadProgress;
      lastUpdate?: number; // <- pridėtas čia
    }
  >();
}

// Export a singleton instance
export const backendService: BackendServiceInterface = new BackendService();
