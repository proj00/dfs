"use client";

import {
  ChevronRight,
  Clock,
  Computer,
  HardDrive,
  Share,
  Star,
} from "lucide-react";
import { Button } from "./ui/button";
import { IStoredContents } from "../lib/getData";
import { use } from "react";

interface SidebarProps {
  currentFolder: string | null;
  navigateToFolder: (folderId: string | null) => void;
  contentsPromise: Promise<IStoredContents>;
}

export function Sidebar({
  currentFolder,
  navigateToFolder,
  contentsPromise,
}: SidebarProps) {
  const contents = use(contentsPromise);
  // Get root folders
  const rootFolders = contents.folders.filter(
    (folder) => folder.parentId === null || folder.parentId.length === 0,
  );

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
          <Computer className="mr-2 h-4 w-4" />
          Computers
        </Button>
        <Button variant="ghost" className="w-full justify-start">
          <Share className="mr-2 h-4 w-4" />
          Shared with me
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
    </div>
  );
}
