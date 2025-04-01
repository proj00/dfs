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
import { mockFolders } from "../lib/mock-data";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "./ui/dialog";
import { Label } from "./ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "./ui/select";

interface SidebarProps {
  currentFolder: string | null;
  navigateToFolder: (folderId: string | null) => void;
}

export function Sidebar({ currentFolder, navigateToFolder }: SidebarProps) {
  // Get root folders
  const rootFolders = mockFolders.filter((folder) => folder.parentId === null);

  // State for publish dialog
  const [publishDialogOpen, setPublishDialogOpen] = useState(false);
  const [selectedContainer, setSelectedContainer] = useState<string>("");
  const [publishTrackerUri, setPublishTrackerUri] = useState("");

  // State for download dialog
  const [downloadDialogOpen, setDownloadDialogOpen] = useState(false);
  const [downloadContainerGuid, setDownloadContainerGuid] = useState("");
  const [downloadTrackerUri, setDownloadTrackerUri] = useState("");

  // Handle publish to tracker
  const handlePublish = () => {
    console.log(
      `Publishing container ${selectedContainer} to tracker ${publishTrackerUri}`,
    );
    // Here you would call your backend service to publish the container
    // For example: backendService.publishToTracker([selectedContainer], publishTrackerUri)
    setPublishDialogOpen(false);
  };

  // Handle download container
  const handleDownload = () => {
    console.log(
      `Downloading container ${downloadContainerGuid} from tracker ${downloadTrackerUri}`,
    );
    // Here you would call your backend service to download the container
    // For example: backendService.downloadContainer(downloadContainerGuid, downloadTrackerUri)
    setDownloadDialogOpen(false);
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
              <Label htmlFor="container" className="text-right">
                Container
              </Label>
              <Select
                value={selectedContainer}
                onValueChange={setSelectedContainer}
              >
                <SelectTrigger className="col-span-3">
                  <SelectValue placeholder="Select a container" />
                </SelectTrigger>
                <SelectContent>
                  {rootFolders.map((folder) => (
                    <SelectItem key={folder.id} value={folder.id}>
                      {folder.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid grid-cols-4 items-center gap-4">
              <Label htmlFor="trackerUri" className="text-right">
                Tracker URI
              </Label>
              <Input
                id="trackerUri"
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
              disabled={!selectedContainer || !publishTrackerUri}
            >
              Publish
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
              <Label htmlFor="containerGuid" className="text-right">
                Container GUID
              </Label>
              <Input
                id="containerGuid"
                value={downloadContainerGuid}
                onChange={(e) => setDownloadContainerGuid(e.target.value)}
                placeholder="Enter container GUID"
                className="col-span-3"
              />
            </div>
            <div className="grid grid-cols-4 items-center gap-4">
              <Label htmlFor="downloadTrackerUri" className="text-right">
                Tracker URI
              </Label>
              <Input
                id="downloadTrackerUri"
                value={downloadTrackerUri}
                onChange={(e) => setDownloadTrackerUri(e.target.value)}
                placeholder="Enter tracker URI"
                className="col-span-3"
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              type="submit"
              onClick={handleDownload}
              disabled={!downloadContainerGuid || !downloadTrackerUri}
            >
              Download
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
