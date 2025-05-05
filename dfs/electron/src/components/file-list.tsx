"use client";

import { ChevronLeft } from "lucide-react";
import { Button } from "./ui/button";
import type { File, Folder } from "../lib/types";
import { formatFileSize, formatDate } from "../lib/utils";
import { FileActionMenu } from "./menus/file-action-menu";
import { FolderActionMenu } from "./menus/folder-action-menu";
import {
  handleFileOpen,
  handleMove,
} from "@/lib/file-handlers";
import { useState } from "react"
import { MoveDialog } from "./move-dialog"
import { RenameDialog } from "./rename-dialog"
import { DeleteDialog } from "./delete-dialog"
import { handleRename, handleDelete } from "../lib/file-handlers"

interface FileListProps {
  readonly files: File[];
  readonly folders: Folder[];
  readonly currentFolder: string | null;
  readonly currentFolderName: string;
  readonly navigateToFolder: (folderId: string | null) => void;
  readonly navigateToParent: () => void;
}

export function FileList({
  files,
  folders,
  currentFolder,
  currentFolderName,
  navigateToFolder,
  navigateToParent,
}: FileListProps) {
  const [moveDialogOpen, setMoveDialogOpen] = useState(false)
  const [renameDialogOpen, setRenameDialogOpen] = useState(false)
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false)
  const [itemToMove, setItemToMove] = useState<File | Folder | null>(null)
  const [itemToRename, setItemToRename] = useState<File | Folder | null>(null)
  const [itemToDelete, setItemToDelete] = useState<File | Folder | null>(null)

  const existingNames = [...files.map((file) => file.name), ...folders.map((folder) => folder.name)]

  const handleMoveClick = (item: File | Folder) => {
    setItemToMove(item)
    setMoveDialogOpen(true)
  }

  const handleRenameClick = (item: File | Folder) => {
    setItemToRename(item)
    setRenameDialogOpen(true)
  }

  const handleDeleteClick = (item: File | Folder) => {
    setItemToDelete(item)
    setDeleteDialogOpen(true)
  }
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

      <div className="rounded-lg border">
        <div className="grid grid-cols-12 gap-4 p-3 text-sm font-medium text-muted-foreground border-b">
          <div className="col-span-6">Name</div>
          <div className="col-span-2">Owner</div>
          <div className="col-span-3">Last modified</div>
          <div className="col-span-1">Size</div>
        </div>

        {folders.length === 0 && files.length === 0 && (
          <div className="p-8 text-center">
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
              <p>This folder is empty</p>
            </div>
            <p className="text-sm text-muted-foreground">
              Files and folders you add to this location will appear here
            </p>
          </div>
        )}

        {folders.map((folder) => (
          <div
            key={folder.id}
            className="grid grid-cols-12 gap-4 p-3 items-center hover:bg-muted/50 cursor-pointer group"
            onClick={() => navigateToFolder(folder.id)}
          >
            <div className="col-span-6 flex items-center">
              <div className="h-10 w-10 flex items-center justify-center text-blue-500 mr-3">
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
                  className="h-6 w-6"
                >
                  <path d="M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z" />
                </svg>
              </div>
              <span className="truncate">{folder.name}</span>
            </div>
            <div className="col-span-2">Me</div>
            <div className="col-span-3">{formatDate(folder.modifiedAt)}</div>
            <div className="col-span-1 flex items-center justify-between">
              <span>—</span>
              <div className="opacity-0 group-hover:opacity-100">
                <FolderActionMenu
                  folder={folder}
                  onRenameClick={() => handleRenameClick(folder)}
                  onMoveClick={() => handleMoveClick(folder)}
                  onDeleteClick={() => handleDeleteClick(folder)}
                />
              </div>
            </div>
          </div>
        ))}

        {files.map((file) => (
          <div
            key={file.id}
            className="grid grid-cols-12 gap-4 p-3 items-center hover:bg-muted/50 cursor-pointer group"
          >
            <div className="col-span-6 flex items-center">
              <div className="h-10 w-10 flex items-center justify-center mr-3">
                {file.type === "image" ? (
                  <div className="h-10 w-10 bg-muted rounded overflow-hidden">
                    <img
                      src={
                        file.thumbnail ?? "/placeholder.svg?height=40&width=40"
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
                    className="h-6 w-6 text-blue-500"
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
                    className="h-6 w-6 text-green-500"
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
                    className="h-6 w-6 text-gray-500"
                  >
                    <path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z" />
                    <polyline points="14 2 14 8 20 8" />
                  </svg>
                )}
              </div>
              <span className="truncate">{file.name}</span>
            </div>
            <div className="col-span-2">Me</div>
            <div className="col-span-3">{formatDate(file.modifiedAt)}</div>
            <div className="col-span-1 flex items-center justify-between">
              <span>{formatFileSize(file.size)}</span>
              <div className="opacity-0 group-hover:opacity-100">
                <FileActionMenu
                  file={file}
                  onOpenClick={handleFileOpen}
                  onRenameClick={() => handleRenameClick(file)}
                  onMoveClick={() => handleMoveClick(file)}
                  onDeleteClick={() => handleDeleteClick(file)}
                />
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
