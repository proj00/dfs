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
  folders: Folder[]; // Add folders prop
}

export function Sidebar({
  currentFolder,
  navigateToFolder,
  folders,
}: SidebarProps) {
  // Get root folders from the passed folders prop
  const rootFolders = folders.filter((folder) => folder.parentId === null);

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

  // Handle publish to tracker
  const handlePublish = async () => {
    if (!selectedContainer || !publishTrackerUri) return;

    setIsPublishing(true);
    try {
      await backendService.publishToTracker(
        selectedContainer,
        publishTrackerUri,
      );
      setPublishDialogOpen(false);
    } catch (error) {
      console.error("Failed to publish:", error);
    } finally {
      setIsPublishing(false);
    }
  };

  // Update the handleDownload function to include the destination

  // Handle download container
  const handleDownload = async () => {
    if (!downloadContainerGuid || !downloadTrackerUri) return;

    setIsDownloading(true);
    try {
      await backendService.downloadContainer(
        downloadContainerGuid,
        downloadTrackerUri,
        downloadDestination || undefined,
      );
      setDownloadDialogOpen(false);
    } catch (error) {
      console.error("Failed to download:", error);
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
      <Dialog open={publishDialogOpen} onOpenChange={setPublishDialogOpen}>
        <DialogContent className="sm:max-w-[425px]">
          <DialogHeader>
            <DialogTitle>Publish to Tracker</DialogTitle>
            <DialogDescription>
              Select a container and enter a tracker URI to publish your
              container.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-4 items-center gap-4">
              <div className="text-right text-sm font-medium">Container</div>
              <select
                value={selectedContainer}
                onChange={(e) => setSelectedContainer(e.target.value)}
                className="col-span-3 flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
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
              {isPublishing ? "Publishing..." : "Publish"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Download Container Dialog */}
      <Dialog open={downloadDialogOpen} onOpenChange={setDownloadDialogOpen}>
        <DialogContent className="sm:max-w-[425px]">
          <DialogHeader>
            <DialogTitle>Download Container</DialogTitle>
            <DialogDescription>
              Enter a container GUID and tracker URI to download a container.
            </DialogDescription>
          </DialogHeader>
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
              />
            </div>
            <div className="grid grid-cols-4 items-center gap-4">
              <div className="text-right text-sm font-medium">Tracker URI</div>
              <Input
                value={downloadTrackerUri}
                onChange={(e) => setDownloadTrackerUri(e.target.value)}
                placeholder="Enter tracker URI"
                className="col-span-3"
              />
            </div>
            <div className="grid grid-cols-4 items-center gap-4">
              <div className="text-right text-sm font-medium">Destination</div>
              <div className="col-span-3 flex gap-2">
                <Input
                  value={downloadDestination || "Default location"}
                  readOnly
                  className="flex-1"
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
                  }}
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
              {isDownloading ? "Downloading..." : "Download"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
