"use client";

import { useState } from "react";
import {
  ChevronRight,
  Clock,
  Download,
  HardDrive,
  Star,
  Upload,
} from "lucide-react";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "./ui/dialog";
import { backendService } from "../services/BackendService";
import type { Folder } from "../lib/types";
import { GetNodeService } from "@/IpcService/INodeService";

interface SidebarProps {
  currentFolder: string | null;
  navigateToFolder: (folderId: string | null) => void;
  folders: Folder[];
  onContainerDownloaded?: () => void; // Add callback for container download
  onContainerPublished?: () => void; // Add callback for container publish
}

export function Sidebar({
  currentFolder,
  navigateToFolder,
  folders,
  onContainerDownloaded,
  onContainerPublished,
}: SidebarProps) {
  // Get root folders from the passed folders prop
  const rootFolders = folders.filter((folder) => folder.parentId.length === 0);

  // State for publish dialog
  const [publishDialogOpen, setPublishDialogOpen] = useState(false);
  const [selectedContainer, setSelectedContainer] = useState<string>("");
  const [publishTrackerUri, setPublishTrackerUri] = useState("");
  const [isPublishing, setIsPublishing] = useState(false);

  // State for download dialog
  const [downloadDialogOpen, setDownloadDialogOpen] = useState(false);
  const [downloadContainerGuid, setDownloadContainerGuid] = useState("");
  const [downloadTrackerUri, setDownloadTrackerUri] = useState("");
  const [isDownloading, setIsDownloading] = useState(false);
  const [downloadDestination, setDownloadDestination] = useState<string>("");
  const [downloadSuccess, setDownloadSuccess] = useState<boolean | null>(null);

  // Add state for publish success/error
  const [publishSuccess, setPublishSuccess] = useState<boolean | null>(null);

  // Handle publish to tracker
  const handlePublish = async () => {
    if (!selectedContainer || !publishTrackerUri) return;

    setIsPublishing(true);
    setPublishSuccess(null);

    try {
      await backendService.publishToTracker(
        selectedContainer,
        publishTrackerUri,
      );
      setPublishSuccess(true);

      // Close dialog after a short delay to show success state
      setTimeout(() => {
        setPublishDialogOpen(false);
        setSelectedContainer("");
        setPublishTrackerUri("");
        setPublishSuccess(null);

        // Trigger refresh of container list
        if (onContainerPublished) {
          onContainerPublished();
        }
      }, 1500);
    } catch (error) {
      console.error("Failed to publish:", error);
      setPublishSuccess(false);
    } finally {
      setIsPublishing(false);
    }
  };

  // Handle download container
  const handleDownload = async () => {
    if (!downloadContainerGuid || !downloadTrackerUri) return;

    setIsDownloading(true);
    setDownloadSuccess(null);

    try {
      await backendService.downloadContainer(
        downloadContainerGuid,
        downloadTrackerUri,
        downloadDestination || undefined,
      );

      setDownloadSuccess(true);

      // Close dialog after a short delay to show success state
      setTimeout(() => {
        setDownloadDialogOpen(false);
        setDownloadContainerGuid("");
        setDownloadTrackerUri("");
        setDownloadDestination("");
        setDownloadSuccess(null);

        // Trigger refresh of container list
        if (onContainerDownloaded) {
          onContainerDownloaded();
        }
      }, 1500);
    } catch (error) {
      console.error("Failed to download:", error);
      setDownloadSuccess(false);
    } finally {
      setIsDownloading(false);
    }
  };

  return (
    <div className="w-64 border-r bg-background p-4 hidden md:block">
      <div className="space-y-1">
        <Button
          variant="ghost"
          className={`w-full justify-start ${
            currentFolder === null ? "bg-muted" : ""
          }`}
          onClick={() => navigateToFolder(null)}
        >
          <HardDrive className="mr-2 h-4 w-4" />
          My Drive
        </Button>
        <Button variant="ghost" className="w-full justify-start">
          <Clock className="mr-2 h-4 w-4" />
          Recent
        </Button>
        <Button variant="ghost" className="w-full justify-start">
          <Star className="mr-2 h-4 w-4" />
          Starred
        </Button>
      </div>

      <div className="mt-6 space-y-2">
        <Button
          variant="outline"
          className="w-full justify-start"
          onClick={() => setPublishDialogOpen(true)}
        >
          <Upload className="mr-2 h-4 w-4" />
          Publish to Tracker
        </Button>

        <Button
          variant="outline"
          className="w-full justify-start"
          onClick={() => setDownloadDialogOpen(true)}
        >
          <Download className="mr-2 h-4 w-4" />
          Download Container
        </Button>
      </div>

      <div className="mt-6">
        <h3 className="mb-2 text-sm font-medium">My folders</h3>
        {rootFolders.length === 0 ? (
          <div className="py-2 px-3 text-sm text-muted-foreground italic">
            No folders available
          </div>
        ) : (
          <div className="space-y-1">
            {rootFolders.map((folder) => (
              <Button
                key={folder.id}
                variant="ghost"
                className={`w-full justify-start ${
                  currentFolder === folder.id ? "bg-muted" : ""
                }`}
                onClick={() => navigateToFolder(folder.id)}
              >
                {folder.hasChildren ? (
                  <ChevronRight className="mr-2 h-4 w-4" />
                ) : (
                  <span className="mr-2 w-4" />
                )}
                {folder.name}
              </Button>
            ))}
          </div>
        )}
      </div>

      {/* Publish to Tracker Dialog */}
      <Dialog
        open={publishDialogOpen}
        onOpenChange={(open) => {
          if (!isPublishing) {
            setPublishDialogOpen(open);
            if (!open) {
              setPublishSuccess(null);
            }
          }
        }}
      >
        <DialogContent className="sm:max-w-[425px]">
          <DialogHeader>
            <DialogTitle>Publish to Tracker</DialogTitle>
            <DialogDescription>
              Select a container and enter a tracker URI to publish your
              container.
            </DialogDescription>
          </DialogHeader>

          {publishSuccess === true && (
            <div className="bg-green-50 text-green-700 p-3 rounded-md mb-4 flex items-center">
              <svg
                xmlns="http://www.w3.org/2000/svg"
                className="h-5 w-5 mr-2"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path
                  fillRule="evenodd"
                  d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
                  clipRule="evenodd"
                />
              </svg>
              Container published successfully!
            </div>
          )}

          {publishSuccess === false && (
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
              Failed to publish container. Please try again.
            </div>
          )}

          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-4 items-center gap-4">
              <div className="text-right text-sm font-medium">Container</div>
              <select
                value={selectedContainer}
                onChange={(e) => setSelectedContainer(e.target.value)}
                className="col-span-3 flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                disabled={isPublishing}
              >
                <option value="">Select a container</option>
                {rootFolders.map((folder) => (
                  <option
                    key={folder.containerGuid}
                    value={folder.containerGuid}
                  >
                    {folder.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="grid grid-cols-4 items-center gap-4">
              <div className="text-right text-sm font-medium">Tracker URI</div>
              <Input
                value={publishTrackerUri}
                onChange={(e) => setPublishTrackerUri(e.target.value)}
                placeholder="Enter tracker URI"
                className="col-span-3"
                disabled={isPublishing}
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              type="submit"
              onClick={handlePublish}
              disabled={
                !selectedContainer || !publishTrackerUri || isPublishing
              }
            >
              {isPublishing ? (
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
                  Publishing...
                </span>
              ) : (
                "Publish"
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Download Container Dialog */}
      <Dialog
        open={downloadDialogOpen}
        onOpenChange={(open) => {
          if (!isDownloading) {
            setDownloadDialogOpen(open);
            if (!open) {
              setDownloadSuccess(null);
            }
          }
        }}
      >
        <DialogContent className="sm:max-w-[425px]">
          <DialogHeader>
            <DialogTitle>Download Container</DialogTitle>
            <DialogDescription>
              Enter a container GUID and tracker URI to download a container.
            </DialogDescription>
          </DialogHeader>

          {downloadSuccess === true && (
            <div className="bg-green-50 text-green-700 p-3 rounded-md mb-4 flex items-center">
              <svg
                xmlns="http://www.w3.org/2000/svg"
                className="h-5 w-5 mr-2"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path
                  fillRule="evenodd"
                  d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
                  clipRule="evenodd"
                />
              </svg>
              Container downloaded successfully!
            </div>
          )}

          {downloadSuccess === false && (
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
              Failed to download container. Please try again.
            </div>
          )}

          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-4 items-center gap-4">
              <div className="text-right text-sm font-medium">
                Container GUID
              </div>
              <Input
                value={downloadContainerGuid}
                onChange={(e) => setDownloadContainerGuid(e.target.value)}
                placeholder="Enter container GUID"
                className="col-span-3"
                disabled={isDownloading}
              />
            </div>
            <div className="grid grid-cols-4 items-center gap-4">
              <div className="text-right text-sm font-medium">Tracker URI</div>
              <Input
                value={downloadTrackerUri}
                onChange={(e) => setDownloadTrackerUri(e.target.value)}
                placeholder="Enter tracker URI"
                className="col-span-3"
                disabled={isDownloading}
              />
            </div>
            <div className="grid grid-cols-4 items-center gap-4">
              <div className="text-right text-sm font-medium">Destination</div>
              <div className="col-span-3 flex gap-2">
                <Input
                  value={downloadDestination || "Default location"}
                  readOnly
                  className="flex-1"
                  disabled={isDownloading}
                />
                <Button
                  variant="outline"
                  size="sm"
                  onClick={async () => {
                    // In a real app, this would open a directory picker
                    console.log("Opening directory picker...");
                    const service = await GetNodeService();
                    const path = await service.PickObjectPath(true);
                    setDownloadDestination(path);
                    console.log(`picked: ${path}`);
                  }}
                  disabled={isDownloading}
                >
                  Browse...
                </Button>
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button
              type="submit"
              onClick={handleDownload}
              disabled={
                !downloadContainerGuid || !downloadTrackerUri || isDownloading
              }
            >
              {isDownloading ? (
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
                  Downloading...
                </span>
              ) : (
                "Download"
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
