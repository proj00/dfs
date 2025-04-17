"use client";

import { useState } from "react";
import { Search, Copy, Download, Info } from "lucide-react";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "./ui/dialog";
import {
  ToolTip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "./ui/tooltip";
import { GetNodeService } from "@/IpcService/NodeServiceClient";
import { File } from "@/lib/types";
import { toBase64 } from "@/lib/utils";
import { backendService } from "@/services/BackendService";

interface TrackerSearchDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onDownloadContainer?: (containerGuid: string, trackerUri: string) => void;
}

export function TrackerSearchDialog({
  open,
  onOpenChange,
  onDownloadContainer,
}: TrackerSearchDialogProps) {
  const [trackerUri, setTrackerUri] = useState("");
  const [searchQuery, setSearchQuery] = useState("");
  const [isSearching, setIsSearching] = useState(false);
  const [searchResults, setSearchResults] = useState<File[]>([]);
  const [error, setError] = useState<string | null>(null);

  const handleSearch = async () => {
    if (!trackerUri || !searchQuery) return;

    setIsSearching(true);
    setError(null);
    setSearchResults([]);

    try {
      const nodeService = await GetNodeService();
      const results = await nodeService.SearchForObjects(
        searchQuery,
        trackerUri,
      );
      const hashes = new Set(
        results
          .map((h) => h.hash)
          .flat(1)
          .map((h) => toBase64(h)),
      );
      const guids = new Set(results.map((h) => h.guid));
      const files = (await backendService.fetchDriveData()).files.filter(
        (f) => hashes.has(f.id) && guids.has(f.containerGuid),
      );

      setSearchResults(files);
    } catch (err) {
      console.error("Search failed:", err);
      setError(
        "Failed to search tracker. Please check the tracker URI and try again.",
      );
    } finally {
      setIsSearching(false);
    }
  };

  const handleCopyGuid = async (guid: string) => {
    try {
      const nodeService = await GetNodeService();
      await nodeService.CopyToClipboard(guid);
      console.log(`Copied GUID: ${guid}`);
    } catch (err) {
      console.error("Failed to copy GUID:", err);
    }
  };

  const handleDownload = (containerGuid: string) => {
    if (onDownloadContainer) {
      onDownloadContainer(containerGuid, trackerUri);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[600px] max-h-[80vh] overflow-hidden flex flex-col">
        <DialogHeader>
          <DialogTitle>Search Tracker</DialogTitle>
          <DialogDescription>
            Search for objects on a tracker by entering a tracker URI and search
            query.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-4">
          <div className="grid grid-cols-4 items-center gap-4">
            <div className="text-right text-sm font-medium">Tracker URI</div>
            <Input
              value={trackerUri}
              onChange={(e) => setTrackerUri(e.target.value)}
              placeholder="Enter tracker URI"
              className="col-span-3"
              disabled={isSearching}
            />
          </div>
          <div className="grid grid-cols-4 items-center gap-4">
            <div className="text-right text-sm font-medium">Search Query</div>
            <div className="col-span-3 flex gap-2">
              <Input
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Enter search terms"
                className="flex-1"
                disabled={isSearching}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    handleSearch();
                  }
                }}
              />
              <Button
                onClick={handleSearch}
                disabled={!trackerUri || !searchQuery || isSearching}
              >
                {isSearching ? (
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
                    Searching...
                  </span>
                ) : (
                  <span className="flex items-center">
                    <Search className="mr-2 h-4 w-4" />
                    Search
                  </span>
                )}
              </Button>
            </div>
          </div>
        </div>

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

        <div className="flex-1 overflow-auto">
          {searchResults.length === 0 && !isSearching && !error ? (
            <div className="flex flex-col items-center justify-center h-64 text-muted-foreground">
              <Search className="h-12 w-12 mb-4 opacity-20" />
              <p>No search results to display</p>
              <p className="text-sm mt-2">
                Enter a tracker URI and search query to find objects
              </p>
            </div>
          ) : (
            <div className="space-y-4">
              {searchResults.map((result, index) => (
                <div key={index} className="border rounded-md p-4 space-y-2">
                  <div className="flex justify-between items-start">
                    <div>
                      <h3 className="font-medium">
                        {result.name || "Unnamed Container"}
                      </h3>
                      <p className="text-sm text-muted-foreground truncate">
                        {result.containerGuid}
                      </p>
                    </div>
                    <TooltipProvider>
                      <ToolTip>
                        <TooltipTrigger asChild>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => handleCopyGuid(result.containerGuid)}
                          >
                            <Copy className="h-4 w-4" />
                          </Button>
                        </TooltipTrigger>
                        <TooltipContent>
                          <p>Copy GUID</p>
                        </TooltipContent>
                      </ToolTip>
                    </TooltipProvider>
                  </div>

                  <div className="flex justify-between items-center pt-2">
                    <div className="flex items-center text-sm text-muted-foreground">
                      <Info className="h-4 w-4 mr-1" />
                      <span>
                        Size:{" "}
                        {result.size
                          ? formatFileSize(Number(result.size))
                          : "Unknown"}
                      </span>
                    </div>
                    <Button
                      size="sm"
                      onClick={() => handleDownload(result.containerGuid)}
                      className="flex items-center"
                    >
                      <Download className="h-4 w-4 mr-2" />
                      Download
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}

// Helper function to format file size
function formatFileSize(bytes: number): string {
  if (bytes === 0) return "0 Bytes";

  const k = 1024;
  const sizes = ["Bytes", "KB", "MB", "GB", "TB"];

  const i = Math.floor(Math.log(bytes) / Math.log(k));

  return (
    Number.parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + " " + sizes[i]
  );
}
