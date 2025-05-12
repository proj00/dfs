"use client";

import { ChevronLeft } from "lucide-react";
import { Button } from "./ui/button";
import type { File, Folder } from "../lib/types";
import { FileActionMenu } from "./menus/file-action-menu";
import { FolderActionMenu } from "./menus/folder-action-menu";
import {
  handleFileOpen,
  handleMove as moveHandler,
  handleCopy as copyHandler,
} from "@/lib/file-handlers";
import { useState } from "react";
import { MoveDialog } from "./move-dialog";
import { RenameDialog } from "./rename-dialog";
import { DeleteDialog } from "./delete-dialog";
import { CopyDialog } from "./copy-dialog";
import { handleRename, handleDelete } from "../lib/file-handlers";

interface FileGridProps {
  readonly files: File[];
  readonly folders: Folder[];
  readonly currentFolder: string | null;
  readonly currentFolderName: string;
  readonly navigateToFolder: (folderId: string | null) => void;
  readonly navigateToParent: () => void;
}

export function FileGrid({
  files,
  folders,
  currentFolder,
  currentFolderName,
  navigateToFolder,
  navigateToParent,
}: FileGridProps) {
  const [moveDialogOpen, setMoveDialogOpen] = useState(false);
  const [renameDialogOpen, setRenameDialogOpen] = useState(false);
  const [copyDialogOpen, setCopyDialogOpen] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [itemToMove, setItemToMove] = useState<File | Folder | null>(null);
  const [itemToRename, setItemToRename] = useState<File | Folder | null>(null);
  const [itemToDelete, setItemToDelete] = useState<File | Folder | null>(null);
  const [itemToCopy, setItemToCopy] = useState<File | Folder | null>(null);

  const existingNames = [
    ...files.map((file) => file.name),
    ...folders.map((folder) => folder.name),
  ];

  const handleMove = async (
    item: File | Folder,
    destinationFolderId: string | null,
  ): Promise<void> => {
    if (destinationFolderId === null) {
      // Handle root folder case
      await moveHandler(item, {
        id: "",
        name: "Root",
        parentId: [],
        hasChildren: false,
        containerGuid: "",
        createdAt: new Date().toISOString(),
        modifiedAt: new Date().toISOString(),
      });
    } else {
      const destinationFolder = folders.find(
        (f) => f.id === destinationFolderId,
      );
      if (destinationFolder) {
        await moveHandler(item, destinationFolder);
      }
    }
  };

  const handleCopy = async (
    item: File | Folder,
    destinationFolderId: string | null,
  ): Promise<boolean> => {
    if (destinationFolderId === null) {
      // Handle root folder case
      return await copyHandler(item, {
        id: "",
        name: "Root",
        parentId: [],
        hasChildren: false,
        containerGuid: "",
        createdAt: new Date().toISOString(),
        modifiedAt: new Date().toISOString(),
      });
    } else {
      const destinationFolder = folders.find(
        (f) => f.id === destinationFolderId,
      );
      if (destinationFolder) {
        return await copyHandler(item, destinationFolder);
      }
    }
    return false;
  };

  const handleMoveClick = (item: File | Folder) => {
    setItemToMove(item);
    setMoveDialogOpen(true);
  };

  const handleRenameClick = (item: File | Folder) => {
    setItemToRename(item);
    setRenameDialogOpen(true);
  };

  const handleDeleteClick = (item: File | Folder) => {
    setItemToDelete(item);
    setDeleteDialogOpen(true);
  };

  const handleCopyClick = (item: File | Folder) => {
    setItemToCopy(item);
    setCopyDialogOpen(true);
  };
  return (
    <div className="space-y-4">
      <MoveDialog
        open={moveDialogOpen}
        onOpenChange={setMoveDialogOpen}
        item={itemToMove}
        folders={folders}
        onMove={handleMove}
      />
      <RenameDialog
        open={renameDialogOpen}
        onOpenChange={setRenameDialogOpen}
        item={itemToRename}
        onRename={handleRename}
        existingNames={existingNames}
      />
      <CopyDialog
        open={copyDialogOpen}
        onOpenChange={setCopyDialogOpen}
        item={itemToCopy}
        folders={folders}
        onCopy={handleCopy}
        existingNames={existingNames}
      />
      <DeleteDialog
        open={deleteDialogOpen}
        onOpenChange={setDeleteDialogOpen}
        item={itemToDelete}
        onDelete={handleDelete}
      />
      <div className="flex items-center">
        {currentFolder && (
          <Button
            variant="ghost"
            size="sm"
            onClick={navigateToParent}
            className="mr-2"
          >
            <ChevronLeft className="h-4 w-4 mr-1" />
            Back
          </Button>
        )}
        <h2 className="text-xl font-semibold">{currentFolderName}</h2>
      </div>

      {folders.length === 0 && files.length === 0 ? (
        <div className="flex flex-col items-center justify-center h-64 border rounded-lg p-8">
          <div className="text-muted-foreground mb-4">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              width="48"
              height="48"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="1"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="mx-auto mb-2"
            >
              <path d="M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z" />
            </svg>
            <p className="text-center">This folder is empty</p>
          </div>
          <p className="text-sm text-muted-foreground text-center">
            Files and folders you add to this location will appear here
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
          {folders.map((folder) => (
            <div key={folder.id} className="group relative">
              <div
                className="flex flex-col items-center p-4 rounded-lg border bg-background hover:bg-muted/50 cursor-pointer"
                onClick={() => navigateToFolder(folder.id)}
              >
                <div className="h-16 w-16 flex items-center justify-center text-blue-500">
                  <svg
                    xmlns="http://www.w3.org/2000/svg"
                    width="24"
                    height="24"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    className="h-12 w-12"
                  >
                    <path d="M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z" />
                  </svg>
                </div>
                <div className="mt-2 w-full text-center truncate">
                  {folder.name}
                </div>
              </div>
              <div className="absolute top-2 right-2 opacity-0 group-hover:opacity-100">
                <FolderActionMenu
                  folder={folder}
                  onRenameClick={() => handleRenameClick(folder)}
                  onMoveClick={() => handleMoveClick(folder)}
                  onCopyClick={() => handleCopyClick(folder)}
                  onDeleteClick={() => handleDeleteClick(folder)}
                />
              </div>
            </div>
          ))}

          {files.map((file) => (
            <div key={file.id} className="group relative">
              <div className="flex flex-col items-center p-4 rounded-lg border bg-background hover:bg-muted/50 cursor-pointer">
                <div className="h-16 w-16 flex items-center justify-center">
                  {file.type === "image" ? (
                    <div className="h-16 w-16 bg-muted rounded overflow-hidden">
                      <img
                        src={
                          file.thumbnail ??
                          "/placeholder.svg?height=64&width=64"
                        }
                        alt={file.name}
                        className="h-full w-full object-cover"
                      />
                    </div>
                  ) : file.type === "document" ? (
                    <svg
                      xmlns="http://www.w3.org/2000/svg"
                      width="24"
                      height="24"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="2"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      className="h-12 w-12 text-blue-500"
                    >
                      <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z" />
                      <polyline points="14 2 14 8 20 8" />
                    </svg>
                  ) : file.type === "spreadsheet" ? (
                    <svg
                      xmlns="http://www.w3.org/2000/svg"
                      width="24"
                      height="24"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="2"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      className="h-12 w-12 text-green-500"
                    >
                      <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z" />
                      <polyline points="14 2 14 8 20 8" />
                      <path d="M8 13h8" />
                      <path d="M8 17h8" />
                      <path d="M8 9h1" />
                    </svg>
                  ) : (
                    <svg
                      xmlns="http://www.w3.org/2000/svg"
                      width="24"
                      height="24"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="2"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      className="h-12 w-12 text-gray-500"
                    >
                      <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z" />
                      <polyline points="14 2 14 8 20 8" />
                    </svg>
                  )}
                </div>
                <div className="mt-2 w-full text-center truncate">
                  {file.name}
                </div>
              </div>
              <div className="absolute top-2 right-2 opacity-0 group-hover:opacity-100">
                <FileActionMenu
                  file={file}
                  onOpenClick={handleFileOpen}
                  onRenameClick={() => handleRenameClick(file)}
                  onMoveClick={() => handleMoveClick(file)}
                  onCopyClick={() => handleCopyClick(file)}
                  onDeleteClick={() => handleDeleteClick(file)}
                />
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
