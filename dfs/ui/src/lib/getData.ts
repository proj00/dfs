import { GetNodeService } from "@/IpcService/INodeService";
import { FromObjectWithHash, type File, type Folder } from "./types";

export interface IStoredContents {
  folders: Folder[];
  files: File[];
}

export async function getContents(): Promise<IStoredContents> {
  let contents: IStoredContents = {
    folders: [],
    files: [],
  };

  const service = await GetNodeService();
  const containers = await service.GetAllContainers();

  for (const container of containers) {
    const objects = await service.GetContainerObjects(container);
    const internalObjects = objects.map((object) =>
      FromObjectWithHash(object, objects, container),
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
