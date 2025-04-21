import { GetNodeService } from "@/IpcService/GetNodeService";
import type { File, Folder } from "./types";
import { hashFromBase64 } from "./utils";

/**
 * Shared handler functions for file and folder operations
 * These are used by both the grid and list views
 */

export const handleFileOpen = async (file: File) => {
  console.log(`Opening file: ${file.name} (${file.type})`);
  const service = await GetNodeService();
  await service.RevealObjectInExplorer(hashFromBase64(file.id));
};

export const handleRename = async (item: File | Folder) => {
  console.log(`Renaming: ${item.name} (${item.id})`);
  // Simulate async rename operation
  await new Promise((resolve) => setTimeout(resolve, 800));
  console.log(`${item.name} renamed successfully`);
};

export const handleMove = async (item: File | Folder) => {
  console.log(`Moving: ${item.name} to a new location`);
  // Simulate async move operation
  await new Promise((resolve) => setTimeout(resolve, 1200));
  console.log(`${item.name} moved successfully`);
};

export const handleDelete = async (item: File | Folder) => {
  console.log(`Deleting: ${item.name} (${item.id})`);
  // Simulate async delete operation
  await new Promise((resolve) => setTimeout(resolve, 700));
  console.log(`${item.name} deleted successfully`);
};

export const handleShare = async (folder: Folder) => {
  console.log(`Sharing folder: ${folder.name} (${folder.id})`);
  // Simulate async share operation
  await new Promise((resolve) => setTimeout(resolve, 900));
  console.log(`Folder ${folder.name} shared successfully`);
};
