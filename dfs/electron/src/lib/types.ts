import * as fs from "@/types/fs/filesystem";
import { toBase64 } from "./utils";

interface FsObject {
  id: string;
  containerGuid: string;
  name: string;
  parentId: string[];
  createdAt: string;
  modifiedAt: string;
}

export interface File extends FsObject {
  type: "document" | "spreadsheet" | "image" | "other";
  size: number;
  thumbnail?: string;
}

export interface Folder extends FsObject {
  hasChildren: boolean;
}

export interface DownloadItem {
  id: string;
  fileId: string;
  containerGuid: string;
  fileName: string;
  startTime: Date;
  downloadedBytes: number;
  totalBytes: number;
  status: "active" | "completed" | "failed";
  speed: number; // bytes per second
  elapsedTime: number; // milliseconds
}

export function FromObjectWithHash(
  object: fs.ObjectWithHash,
  containerObjects: fs.ObjectWithHash[],
  containerGuid: string,
): File | Folder {
  const fsObj = object.object;
  if (fsObj == null) {
    throw new Error("ObjectWithHash had a null object");
  }

  const base: FsObject = {
    id: toBase64(object.hash),
    name: fsObj?.name,
    createdAt: Date.now().toString(),
    modifiedAt: Date.now().toString(),
    containerGuid,
    parentId: containerObjects
      .filter((o) => {
        const obj = o?.object;
        if (obj == null || obj.type.oneofKind !== "directory") {
          return false;
        }

        return (
          obj.type.directory.entries.find((hash) => {
            return toBase64(hash) === toBase64(object.hash);
          }) !== undefined
        );
      })
      .map((o) => toBase64(o.hash)),
  };

  switch (fsObj.type.oneofKind) {
    case "file":
      return {
        ...base,
        type: "other",
        size: Number(fsObj.type.file.size),
        thumbnail: undefined,
      };
    case "directory":
      return {
        ...base,
        hasChildren: base.parentId != null && base.parentId.length > 0,
      };
    default:
      throw new Error("unsupported object type");
  }
}
