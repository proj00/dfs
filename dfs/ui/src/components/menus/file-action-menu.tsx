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
import type { File } from "../../lib/types";

interface FileActionMenuProps {
  file: File;
  onOpenClick?: (file: File, e: React.MouseEvent) => void;
  onDownloadClick?: (file: File, e: React.MouseEvent) => void;
  onRenameClick?: (file: File, e: React.MouseEvent) => void;
  onMoveClick?: (file: File, e: React.MouseEvent) => void;
  onDeleteClick?: (file: File, e: React.MouseEvent) => void;
}

export function FileActionMenu({
  file,
  onOpenClick,
  onDownloadClick,
  onRenameClick,
  onMoveClick,
  onDeleteClick,
}: FileActionMenuProps) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" className="h-8 w-8">
          <MoreHorizontal className="h-4 w-4" />
          <span className="sr-only">File actions</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem
          onClick={(e) => {
            e.stopPropagation();
            onOpenClick?.(file, e);
          }}
        >
          Open
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={(e) => {
            e.stopPropagation();
            onDownloadClick?.(file, e);
          }}
        >
          Download
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={(e) => {
            e.stopPropagation();
            navigator.clipboard.writeText(file.id);
            console.log(`Copied file GUID: ${file.id}`);
          }}
        >
          Copy container GUID
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={(e) => {
            e.stopPropagation();
            onRenameClick?.(file, e);
          }}
        >
          Rename
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={(e) => {
            e.stopPropagation();
            onMoveClick?.(file, e);
          }}
        >
          Move to
        </DropdownMenuItem>
        <DropdownMenuItem
          className="text-destructive"
          onClick={(e) => {
            e.stopPropagation();
            onDeleteClick?.(file, e);
          }}
        >
          Delete
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
