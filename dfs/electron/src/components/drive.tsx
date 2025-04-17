"use client";

import { useState, useEffect, useCallback } from "react";
import { FileGrid } from "./file-grid";
import { FileList } from "./file-list";
import { Header } from "./header";
import { Sidebar } from "./sidebar";
import type { File, Folder } from "../lib/types";
import { backendService } from "../services/BackendService";

export function Drive() {
  const [view, setView] = useState<"grid" | "list">("grid");
  const [currentFolder, setCurrentFolder] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [files, setFiles] = useState<File[]>([]);
  const [folders, setFolders] = useState<Folder[]>([]);
  const [error, setError] = useState<string | null>(null);

  // Polling interval in milliseconds
  const POLLING_INTERVAL = 5000;

  // Function to fetch data
  const fetchData = useCallback(async () => {
    try {
      setError(null);
      const data = await backendService.fetchDriveData();
      setFiles(data.files);
      setFolders(data.folders);
      setIsLoading(false);
    } catch (err) {
      console.error("Error fetching drive data:", err);
      setError("Failed to load drive data. Please try again.");
      setIsLoading(false);
    }
  }, []);

  // Initial data fetch
  useEffect(() => {
    fetchData();

    // Set up polling
    const intervalId = setInterval(fetchData, POLLING_INTERVAL);

    // Clean up interval on component unmount
    return () => clearInterval(intervalId);
  }, [fetchData]);

  // Filter files based on current folder and search query
  const filteredFiles = files.filter((file) => {
    const matchesFolder = currentFolder
      ? file.parentId.find((a) => a === currentFolder) !== undefined
      : file.parentId.length === 0;
    const matchesSearch = file.name
      .toLowerCase()
      .includes(searchQuery.toLowerCase());
    return matchesFolder && (searchQuery === "" || matchesSearch);
  });

  // Filter folders based on parent folder and search query
  const filteredFolders = folders.filter((folder) => {
    const matchesParent = currentFolder
      ? folder.parentId.find((a) => a === currentFolder) !== undefined
      : folder.parentId.length === 0;
    const matchesSearch = folder.name
      .toLowerCase()
      .includes(searchQuery.toLowerCase());
    return matchesParent && (searchQuery === "" || matchesSearch);
  });

  // Get current folder name
  const currentFolderName = currentFolder
    ? folders.find((folder) => folder.id === currentFolder)?.name ||
      "Unknown Folder"
    : "My Drive";

  // Navigate to a folder
  const navigateToFolder = (folderId: string | null) => {
    setCurrentFolder(folderId);
  };

  // Navigate to parent folder
  const navigateToParent = () => {
    if (!currentFolder) return;
    const parentFolder =
      folders.find((folder) => folder.id === currentFolder)?.parentId.at(0) ||
      null;
    setCurrentFolder(parentFolder);
  };

  // Handle container download completion
  const handleContainerDownloaded = useCallback(() => {
    console.log("Container downloaded, refreshing data...");
    fetchData();
  }, [fetchData]);

  // Handle container publish completion
  const handleContainerPublished = useCallback(() => {
    console.log("Container published, refreshing data...");
    fetchData();
  }, [fetchData]);

  // Handle download from tracker search
  const handleDownloadContainer = useCallback(
    (containerGuid: string, trackerUri: string) => {
      console.log(
        `Downloading container ${containerGuid} from tracker ${trackerUri}`,
      );
      // Open the download dialog in the sidebar with pre-filled values
      // This is a workaround since we don't have direct access to the sidebar's state
      // In a real app, you might use a state management library or context for this
      const event = new CustomEvent("openDownloadDialog", {
        detail: { containerGuid, trackerUri },
      });
      window.dispatchEvent(event);
    },
    [],
  );
  return (
    <div className="flex h-screen flex-col">
      <Header
        view={view}
        setView={setView}
        searchQuery={searchQuery}
        setSearchQuery={setSearchQuery}
        onRefresh={fetchData}
        onDownloadContainer={handleDownloadContainer}
      />
      <div className="flex flex-1 overflow-hidden">
        <Sidebar
          currentFolder={currentFolder}
          navigateToFolder={navigateToFolder}
          folders={folders}
          onContainerDownloaded={handleContainerDownloaded}
          onContainerPublished={handleContainerPublished}
        />
        <main className="flex-1 overflow-auto p-4">
          {error && (
            <div className="mb-4 p-4 bg-red-50 text-red-600 rounded-md border border-red-200">
              {error}
              <button className="ml-2 underline" onClick={fetchData}>
                Retry
              </button>
            </div>
          )}

          {isLoading ? (
            <div className="flex flex-col items-center justify-center h-64">
              <div className="w-12 h-12 border-4 border-blue-600 border-t-transparent rounded-full animate-spin"></div>
              <p className="mt-4 text-muted-foreground">
                Loading drive data...
              </p>
            </div>
          ) : view === "grid" ? (
            <FileGrid
              files={filteredFiles}
              folders={filteredFolders}
              currentFolder={currentFolder}
              currentFolderName={currentFolderName}
              navigateToFolder={navigateToFolder}
              navigateToParent={navigateToParent}
            />
          ) : (
            <FileList
              files={filteredFiles}
              folders={filteredFolders}
              currentFolder={currentFolder}
              currentFolderName={currentFolderName}
              navigateToFolder={navigateToFolder}
              navigateToParent={navigateToParent}
            />
          )}
        </main>
      </div>
    </div>
  );
}
