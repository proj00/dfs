"use client";

import type React from "react";

import { MoreHorizontal } from "lucide-react";
import { Button } from "../ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";
import type { Folder } from "../../lib/types";

interface FolderActionMenuProps {
  folder: Folder;
  onRenameClick?: (folder: Folder, e: React.MouseEvent) => void;
  onMoveClick?: (folder: Folder, e: React.MouseEvent) => void;
  onDeleteClick?: (folder: Folder, e: React.MouseEvent) => void;
  onShareClick?: (folder: Folder, e: React.MouseEvent) => void;
}

export function FolderActionMenu({
  folder,
  onRenameClick,
  onMoveClick,
  onDeleteClick,
  onShareClick,
}: FolderActionMenuProps) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" className="h-8 w-8">
          <MoreHorizontal className="h-4 w-4" />
          <span className="sr-only">Folder actions</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {onShareClick && (
          <DropdownMenuItem
            onClick={(e) => {
              e.stopPropagation();
              onShareClick(folder, e);
            }}
          >
            Share
          </DropdownMenuItem>
        )}
        <DropdownMenuItem
          onClick={(e) => {
            e.stopPropagation();
            navigator.clipboard.writeText(folder.id);
            console.log(`Copied folder GUID: ${folder.id}`);
          }}
        >
          Copy container GUID
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={(e) => {
            e.stopPropagation();
            onRenameClick?.(folder, e);
          }}
        >
          Rename
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={(e) => {
            e.stopPropagation();
            onMoveClick?.(folder, e);
          }}
        >
          Move to
        </DropdownMenuItem>
        <DropdownMenuItem
          className="text-destructive"
          onClick={(e) => {
            e.stopPropagation();
            onDeleteClick?.(folder, e);
          }}
        >
          Delete
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
