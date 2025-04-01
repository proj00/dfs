// Define interfaces for the backend services
export interface BackendServiceInterface {
  publishToTracker: (
    containerId: string,
    trackerUri: string,
  ) => Promise<boolean>;
  downloadContainer: (
    containerGuid: string,
    trackerUri: string,
  ) => Promise<boolean>;
  fetchDriveData: () => Promise<DriveData>;
}

// Define the data structure
export interface DriveData {
  files: File[];
  folders: Folder[];
}

import { GetNodeService } from "@/IpcService/INodeService";
// Import the types and mock data
import { FromObjectWithHash, type File, type Folder } from "../lib/types";
import { fs } from "@/types/filesystem";

// Mock implementation for development
class BackendService implements BackendServiceInterface {
  async publishToTracker(
    containerId: string,
    trackerUri: string,
  ): Promise<boolean> {
    console.log(
      `[MOCK] Publishing container ${containerId} to tracker ${trackerUri}`,
    );
    // Simulate network delay
    await new Promise((resolve) => setTimeout(resolve, 1000));
    return true;
  }

  async downloadContainer(
    containerGuid: string,
    trackerUri: string,
  ): Promise<boolean> {
    console.log(
      `[MOCK] Downloading container ${containerGuid} from tracker ${trackerUri}`,
    );
    // Simulate network delay
    await new Promise((resolve) => setTimeout(resolve, 1000));
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
      const bytes = await service.GetContainerObjects(container);
      const objects = fs.ObjectArray.deserializeBinary(bytes);
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
