"use client";

import type React from "react";

import { useState, useEffect } from "react";
import { FolderOpen, ChevronRight } from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "./ui/dialog";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import type { File, Folder } from "../lib/types";

interface FileOperationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  item: File | Folder | null;
  folders: Folder[];
  onAction: (
    item: File | Folder,
    destinationFolderId: string | null,
    ...args: any[]
  ) => Promise<any>;
  title: React.ReactNode;
  description: React.ReactNode;
  actionButtonText: string;
  actionButtonIcon?: React.ReactNode;
  extraContent?: React.ReactNode;
  isValidDestination?: (folder: Folder, currentItemId: string) => boolean;
  extraActionParams?: any[];
}

export function FileOperationDialog({
  open,
  onOpenChange,
  item,
  folders,
  onAction,
  title,
  description,
  actionButtonText,
  actionButtonIcon,
  extraContent,
  isValidDestination,
  extraActionParams = [],
}: FileOperationDialogProps) {
  const [selectedFolderId, setSelectedFolderId] = useState<string | null>(null);
  const [customPath, setCustomPath] = useState("");
  const [isProcessing, setIsProcessing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Reset state when dialog opens
  useEffect(() => {
    if (open) {
      setSelectedFolderId(null);
      setCustomPath("");
      setError(null);
    }
  }, [open]);

  // Get root folders (folders with empty parentId array)
  const rootFolders = folders.filter((folder) => folder.parentId.length === 0);

  // Get folder name by ID
  const getFolderName = (folderId: string | null) => {
    if (folderId === null) return "My Drive";
    return (
      folders.find((folder) => folder.id === folderId)?.name || "Unknown Folder"
    );
  };

  // Handle action
  const handleAction = async () => {
    if (!item) return;

    // Use custom path if provided, otherwise use selected folder
    const destinationId = customPath ? null : selectedFolderId;

    setIsProcessing(true);
    setError(null);

    try {
      // In a real app, you would validate the custom path here
      await onAction(item, destinationId, ...extraActionParams);
      onOpenChange(false);
    } catch (err) {
      console.error("Operation failed:", err);
      setError(
        `Failed to ${actionButtonText.toLowerCase()} item. Please try again.`,
      );
    } finally {
      setIsProcessing(false);
    }
  };

  // Get current location of the item
  const getCurrentLocation = () => {
    if (!item) return "";

    // If the item has no parents, it's in the root
    if (item.parentId.length === 0) return "My Drive";

    // Otherwise, get the name of the first parent folder
    const parentId = item.parentId[0];
    return getFolderName(parentId);
  };

  const currentLocation = getCurrentLocation();
  const itemType = item
    ? item.hasOwnProperty("size")
      ? "file"
      : "folder"
    : "";

  return (
    <Dialog
      open={open}
      onOpenChange={(open) => !isProcessing && onOpenChange(open)}
    >
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        {error && (
          <div className="bg-red-50 text-red-700 p-3 rounded-md mb-4 flex items-center">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              className="h-5 w-5 mr-2"
              viewBox="0 0 20 20"
              fill="currentColor"
            >
              <path
                fillRule="evenodd"
                d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
                clipRule="evenodd"
              />
            </svg>
            {error}
          </div>
        )}

        <div className="space-y-4 py-2">
          <div className="text-sm">
            <span className="font-medium">Current location:</span>{" "}
            {currentLocation}
          </div>

          <div className="space-y-2">
            <div className="font-medium text-sm">
              Select destination folder:
            </div>
            <div className="border rounded-md h-60 overflow-y-auto p-2">
              <div
                className={`flex items-center p-2 rounded-md cursor-pointer ${
                  selectedFolderId === null ? "bg-muted" : "hover:bg-muted/50"
                }`}
                onClick={() => setSelectedFolderId(null)}
              >
                <FolderOpen className="h-5 w-5 mr-2 text-blue-500" />
                <span>My Drive (root)</span>
              </div>

              {rootFolders.map((folder) => (
                <FolderItem
                  key={folder.id}
                  folder={folder}
                  folders={folders}
                  selectedFolderId={selectedFolderId}
                  setSelectedFolderId={setSelectedFolderId}
                  level={0}
                  currentItemId={item?.id || ""}
                  isValidDestination={isValidDestination}
                />
              ))}
            </div>
          </div>

          <div className="space-y-2">
            <div className="font-medium text-sm">Or enter a custom path:</div>
            <Input
              placeholder="/path/to/destination"
              value={customPath}
              onChange={(e) => setCustomPath(e.target.value)}
              disabled={isProcessing}
            />
            <p className="text-xs text-muted-foreground">
              Enter a path like "/Work Documents/Projects" to{" "}
              {itemType === "file" ? "place" : "move"} to a specific location.
            </p>
          </div>

          {extraContent}
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={isProcessing}
          >
            Cancel
          </Button>
          <Button
            onClick={handleAction}
            disabled={isProcessing || (!selectedFolderId && !customPath)}
          >
            {isProcessing ? (
              <span className="flex items-center">
                <svg
                  className="animate-spin -ml-1 mr-2 h-4 w-4 text-white"
                  xmlns="http://www.w3.org/2000/svg"
                  fill="none"
                  viewBox="0 0 24 24"
                >
                  <circle
                    className="opacity-25"
                    cx="12"
                    cy="12"
                    r="10"
                    stroke="currentColor"
                    strokeWidth="4"
                  ></circle>
                  <path
                    className="opacity-75"
                    fill="currentColor"
                    d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                  ></path>
                </svg>
                {`${actionButtonText}ing...`}
              </span>
            ) : (
              <>
                {actionButtonIcon}
                {actionButtonText}
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// Recursive folder item component
interface FolderItemProps {
  folder: Folder;
  folders: Folder[];
  selectedFolderId: string | null;
  setSelectedFolderId: (id: string | null) => void;
  level: number;
  currentItemId: string;
  isValidDestination?: (folder: Folder, currentItemId: string) => boolean;
}

function FolderItem({
  folder,
  folders,
  selectedFolderId,
  setSelectedFolderId,
  level,
  currentItemId,
  isValidDestination,
}: FolderItemProps) {
  const [expanded, setExpanded] = useState(false);

  // Get child folders (folders that have this folder's ID in their parentId array)
  const childFolders = folders.filter((f) => f.parentId.includes(folder.id));

  // Check if this folder is a parent of the current item (to prevent moving a folder into itself)
  const isParentOfCurrentItem = folder.id === currentItemId;

  // Default validation if no custom validation is provided
  const defaultIsValidDestination = (
    folder: Folder,
    currentItemId: string,
  ): boolean => {
    // Check if this folder is in the current item's parent chain
    // This prevents moving a folder into any of its descendants
    const isDescendantOfCurrentItem = (
      folderId: string,
      itemId: string,
    ): boolean => {
      const folder = folders.find((f) => f.id === folderId);
      if (!folder) return false;

      // If this folder has the item as a parent, it's a descendant
      if (folder.parentId.includes(itemId)) return true;

      // Check if any of this folder's parents are descendants of the item
      return folder.parentId.some((parentId) =>
        isDescendantOfCurrentItem(parentId, itemId),
      );
    };

    return !(
      isParentOfCurrentItem ||
      (currentItemId &&
        folder.hasOwnProperty("hasChildren") &&
        isDescendantOfCurrentItem(folder.id, currentItemId))
    );
  };

  const isValid = isValidDestination
    ? isValidDestination(folder, currentItemId)
    : defaultIsValidDestination(folder, currentItemId);

  return (
    <div className="ml-2">
      <div
        className={`flex items-center p-2 rounded-md cursor-pointer ${
          !isValid
            ? "opacity-50 cursor-not-allowed"
            : selectedFolderId === folder.id
              ? "bg-muted"
              : "hover:bg-muted/50"
        }`}
        onClick={() => isValid && setSelectedFolderId(folder.id)}
      >
        {folder.hasChildren && (
          <ChevronRight
            className={`h-4 w-4 mr-1 transition-transform ${expanded ? "rotate-90" : ""}`}
            onClick={(e) => {
              e.stopPropagation();
              setExpanded(!expanded);
            }}
          />
        )}
        {!folder.hasChildren && <div className="w-5" />}
        <FolderOpen className="h-5 w-5 mr-2 text-blue-500" />
        <span className={!isValid ? "text-muted-foreground" : ""}>
          {folder.name}
          {isParentOfCurrentItem && " (current)"}
        </span>
      </div>

      {expanded && childFolders.length > 0 && (
        <div className="ml-4">
          {childFolders.map((childFolder) => (
            <FolderItem
              key={childFolder.id}
              folder={childFolder}
              folders={folders}
              selectedFolderId={selectedFolderId}
              setSelectedFolderId={setSelectedFolderId}
              level={level + 1}
              currentItemId={currentItemId}
              isValidDestination={isValidDestination}
            />
          ))}
        </div>
      )}
    </div>
  );
}
