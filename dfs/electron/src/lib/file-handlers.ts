import { GetNodeService } from "@/IpcService/GetNodeService";
import type { File, Folder } from "./types";
import { hashFromBase64 } from "./utils";
import log from "electron-log/renderer";
import { OperationType } from "@/types/rpc/uiservice";

/**
 * Shared handler functions for file and folder operations
 * These are used by both the grid and list views
 */

export const handleFileOpen = async (file: File) => {
  log.info(`Opening file: ${file.name} (${file.type})`);
  const service = await GetNodeService();
  await service.RevealObjectInExplorer(hashFromBase64(file.id));
};

export const handleRename = async (item: File | Folder, newName: string) => {
  log.info(`Renaming: ${item.name} (${item.id})`);
  const service = await GetNodeService();
  const parentId = item.parentId.length > 0 ? item.parentId[0] : "";
  await service.ApplyFsOperation({
    info: {
      oneofKind: "newName",
      newName: newName,
    },
    containerGuid: item.containerGuid,
    type: OperationType.Rename,
    target: hashFromBase64(item.id),
    parent: hashFromBase64(parentId),
  });
};

export const handleMove = async (
  item: File | Folder,
  destinationFolderId: Folder,
) => {
  console.log(`Moving: ${item.name} to ${destinationFolderId}`);
  // Simulate async move operation
  const service = await GetNodeService();
  const parentId = item.parentId.length > 0 ? item.parentId[0] : "";
  await service.ApplyFsOperation({
    info: {
      oneofKind: "newParent",
      newParent: hashFromBase64(destinationFolderId.id),
    },
    containerGuid: item.containerGuid,
    type: OperationType.Move,
    target: hashFromBase64(item.id),
    parent: hashFromBase64(parentId),
  });
  console.log(`${item.name} moved successfully`);
};

export const handleDelete = async (item: File | Folder) => {
  log.info(`Deleting: ${item.name} (${item.id})`);
  // Simulate async delete operation
  const service = await GetNodeService();
  const parentId = item.parentId.length > 0 ? item.parentId[0] : "";
  await service.ApplyFsOperation({
    info: {
      oneofKind: "empty",
      empty: {},
    },
    containerGuid: item.containerGuid,
    type: OperationType.Delete,
    target: hashFromBase64(item.id),
    parent: hashFromBase64(parentId),
  });
  log.info(`${item.name} deleted successfully`);
};

export async function handleCopy(
  item: File | Folder,
  destinationFolderId: Folder,
): Promise<boolean> {
  try {
    // Copy logic here
    const service = await GetNodeService();
    const parentId = item.parentId.length > 0 ? item.parentId[0] : "";
    await service.ApplyFsOperation({
      info: {
        oneofKind: "newParent",
        newParent: hashFromBase64(destinationFolderId.id),
      },
      containerGuid: item.containerGuid,
      type: OperationType.Move,
      target: hashFromBase64(item.id),
      parent: hashFromBase64(parentId),
    });
    return true; // Return true if successful
  } catch (error) {
    log.error("Error copying item:", error);
    return false; // Return false if an error occurs
  }
}
