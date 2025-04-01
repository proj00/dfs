"use client";

import { useState } from "react";
import { FileGrid } from "./file-grid";
import { FileList } from "./file-list";
import { Header } from "./header";
import { Sidebar } from "./sidebar";
import { mockFiles, mockFolders } from "@/lib/mock-data";

export function Drive() {
  const [view, setView] = useState<"grid" | "list">("grid");
  const [currentFolder, setCurrentFolder] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState("");

  // Filter files based on current folder and search query
  const filteredFiles = mockFiles.filter((file) => {
    const matchesFolder = currentFolder
      ? file.folderId === currentFolder
      : file.folderId === null;
    const matchesSearch = file.name
      .toLowerCase()
      .includes(searchQuery.toLowerCase());
    return matchesFolder && (searchQuery === "" || matchesSearch);
  });

  // Filter folders based on parent folder and search query
  const filteredFolders = mockFolders.filter((folder) => {
    const matchesParent = currentFolder
      ? folder.parentId === currentFolder
      : folder.parentId === null;
    const matchesSearch = folder.name
      .toLowerCase()
      .includes(searchQuery.toLowerCase());
    return matchesParent && (searchQuery === "" || matchesSearch);
  });

  // Get current folder name
  const currentFolderName = currentFolder
    ? mockFolders.find((folder) => folder.id === currentFolder)?.name ||
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
      mockFolders.find((folder) => folder.id === currentFolder)?.parentId ||
      null;
    setCurrentFolder(parentFolder);
  };

  return (
    <div className="flex h-screen flex-col">
      <Header
        view={view}
        setView={setView}
        searchQuery={searchQuery}
        setSearchQuery={setSearchQuery}
      />
      <div className="flex flex-1 overflow-hidden">
        <Sidebar
          currentFolder={currentFolder}
          navigateToFolder={navigateToFolder}
        />
        <main className="flex-1 overflow-auto p-4">
          {view === "grid" ? (
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
