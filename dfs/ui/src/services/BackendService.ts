// Define interfaces for the backend services
export interface BackendServiceInterface {
  publishToTracker: (
    containerId: string,
    trackerUri: string,
  ) => Promise<boolean>;
  downloadContainer: (
    containerGuid: string,
    trackerUri: string,
    destination?: string,
  ) => Promise<boolean>;
  fetchDriveData: () => Promise<DriveData>;
}

// Define the data structure
export interface DriveData {
  files: File[];
  folders: Folder[];
}

import { GetNodeService } from "@/IpcService/NodeServiceClient";
import { FromObjectWithHash, type File, type Folder } from "../lib/types";

class BackendService implements BackendServiceInterface {
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
  ): Promise<boolean> {
    destination = destination ?? "";
    console.log(
      `Downloading container ${containerGuid} from tracker ${trackerUri} into ${destination}`,
    );

    const service = await GetNodeService();
    await service.DownloadContainer(containerGuid, trackerUri, destination, 20);
    return true;
  }

  async fetchDriveData(): Promise<DriveData> {
    let contents: DriveData = {
      folders: [],
      files: [],
    };

    const service = await GetNodeService();
    const containers = await service.GetAllContainers();

    for (const container of containers) {
      const objects = await service.GetContainerObjects(container);
      const internalObjects = objects.data.map((object) =>
        FromObjectWithHash(object, objects.data, container),
      );

      for (let i = 0; i < internalObjects.length; i++) {
        if ((internalObjects[i] as any).size != null) {
          contents.files.push(internalObjects[i] as File);
        } else {
          contents.folders.push(internalObjects[i] as Folder);
        }
      }
    }

    return contents;
  }
}

// Export a singleton instance
export const backendService: BackendServiceInterface = new BackendService();
